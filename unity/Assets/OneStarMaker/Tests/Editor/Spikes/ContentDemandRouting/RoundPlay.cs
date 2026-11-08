#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// round の Play 中の操作。全ての操作の結果は record に残し、例外も record に移して Play を最後まで進める。
    /// 1 つの操作の失敗（例: directory の Scene が Editor Play で読めない）は機構の観測であり、harness の失敗ではない。
    /// 判定担当は `play.step.failed` の行を、その cell の FAIL（機構の負の実測）か UNTESTED（測定の欠測）かに分けて読む。
    /// </summary>
    internal static class RoundPlay
    {
        private const int FrameLimit = 600;

        /// <summary>この Play の harness。Play の停止は Domain Reload を起こさないので、Edit の検証から読める。</summary>
        internal static DirectorHarness? Current;

        internal static IEnumerator Run() => UniTask.ToCoroutine(RunAsync);

        private static async UniTask RunAsync()
        {
            var state = HarnessState.Load();
            var record = state.OpenRecord();
            var cell = state.Cell;
            PlayEnvironment.CaptureLogs(record, cell);
            PlayObserver.Hook(record, cell);
            var survivors = new List<object?>();
            foreach (var routing in RoutingBackend.Unclosed) survivors.Add(routing.Describe());
            // Domain Reload 無効では、前の Play の static な状態がここまで残りうる（N4 の経路 (e)、AC8）。
            record.Write(cell, "play.started", ("epm", EnterPlayModeMatrix.Describe()), ("startScene", PlayEnvironment.CurrentStartScene()),
                ("scenes", PlayObserver.LoadedScenes()), ("unclosedRoutingFromEarlierPlay", survivors));
            try
            {
                var harness = await SetUpAsync(state, record, cell);
                Current = harness;
                await ScenarioAsync(state, harness, record, cell);
                var ownership = new Dictionary<string, object?>();
                foreach (var identity in state.mapIdentities) ownership[identity] = harness.Ownership(identity);
                record.Write(cell, "play.endState", ("scenes", PlayObserver.LoadedScenes()), ("ownership", ownership));
            }
            catch (Exception ex)
            {
                record.Write(cell, "play.step.failed", ("step", "round"), ("error", ex), ("detail", ex.ToString()),
                    ("scenes", PlayObserver.LoadedScenes()));
            }
        }

        private static async UniTask<DirectorHarness> SetUpAsync(HarnessState state, ExperimentRecord record, Cell cell)
        {
            switch (state.Round.mechanism)
            {
                case MechanismAlreadyLoaded.Name: return await MechanismAlreadyLoaded.SetUpPlayAsync(state, record, cell);
                case MechanismTempDirectory.Name: return await MechanismTempDirectory.SetUpPlayAsync(state, record, cell);
            }
            // 基準: snapshot を作らない Unity 標準の Play。名前の再利用で何が取り込まれるかを観測する（AC1、E3）。
            RoutingBackend? routing = null;
            if (state.fixtureDirectoryReady)
            {
                routing = new RoutingBackend(record, cell);
                routing.Add("fixture", "fixture", state.fixtureDirectory);
            }
            var harness = DirectorHarness.Create(state.mapIdentities, state.SceneGuids(), routing, record, cell);
            await harness.RegisterAlreadyLoadedScenesAsync();
            return harness;
        }

        private static async UniTask ScenarioAsync(HarnessState state, DirectorHarness harness, ExperimentRecord record, Cell cell)
        {
            switch (state.Round.scenario)
            {
                case Scenarios.Main:
                case Scenarios.NewUnsaved:
                case Scenarios.AdditiveTwo:
                case Scenarios.PrefabOverride:
                case Scenarios.RefPrefabChanged:
                case Scenarios.StaleRemnant:
                    await MixedSessionAsync(state, harness, record, cell);
                    break;
                case Scenarios.OtherInMap:
                case Scenarios.OtherNotInMap:
                case Scenarios.BaselineOtherNoIsolation:
                case Scenarios.BaselineOtherNotInMapNoIsolation:
                    await UnrequestedSceneAsync(state, harness, record, cell);
                    break;
                case Scenarios.CancelGate:
                    await CancelUnderGateAsync(state, harness, record, cell);
                    break;
                case Scenarios.StopWithoutNext:
                    await RequestSnapshotsAsync(state, harness, record, cell);
                    Observe(record, cell, harness, "ac7a.beforeStop", "Entry", "Next");
                    break;
                case Scenarios.StopDuringGate:
                    await StopUnderGateAsync(state, harness, record, cell);
                    break;
                case Scenarios.Reentry:
                    await RequestSnapshotsAsync(state, harness, record, cell);
                    var entry = PlayObserver.SameName("Entry");
                    record.Write(cell, "ac8.marker", ("expected", state.expectedMarker), ("observed", entry));
                    await Step(record, cell, "addScene:Next", () => harness.AddSceneAsync("Next", "request"));
                    Observe(record, cell, harness, "ac8.afterNext", "Entry", "Next");
                    break;
                case Scenarios.Baseline:
                case Scenarios.DetectorRewriteSave:
                case Scenarios.DetectorClearDirty:
                    await BaselineAsync(harness, record, cell);
                    break;
            }
        }

        /// <summary>
        /// snapshot の Scene と directory の Scene を同じ Play で扱う（E1、AC3、AC4、AC11、AC14、AC2 の各状態）。
        /// snapshot → Next（同時に loaded）→ Next を unload → Next を再ロード → snapshot の GameObject を消す → snapshot を unload。
        /// Next は停止まで loaded のまま残し、Play 停止の経路を通す。
        /// </summary>
        private static async UniTask MixedSessionAsync(HarnessState state, DirectorHarness harness, ExperimentRecord record, Cell cell)
        {
            var targets = await RequestSnapshotsAsync(state, harness, record, cell);
            await Step(record, cell, "addScene:Next", () => harness.AddSceneAsync("Next", "request"));
            var observed = new List<string>(targets) { "Next" };
            Observe(record, cell, harness, "e1.simultaneous", observed.ToArray());
            if (state.Round.scenario == Scenarios.StaleRemnant)
                record.Write(cell, "ac9b.markerAfterReclaim", ("observed", PlayObserver.SameName("Entry")), ("staleMarker", "stale"),
                    ("staleLease", state.staleLeasePath));
            await Step(record, cell, "unloadScene:Next", () => harness.UnloadSceneAsync("Next"));
            Observe(record, cell, harness, "ac14.nextUnloaded", "Next");
            await Step(record, cell, "addScene:Next(retry)", () => harness.AddSceneAsync("Next", "retry"));
            Observe(record, cell, harness, "ac14.nextReloaded", "Next");
            foreach (var identity in targets)
            {
                DestroyOneObject(identity, record, cell);
                await Step(record, cell, "unloadScene:" + identity, () => harness.UnloadSceneAsync(identity));
                Observe(record, cell, harness, "snapshot.unloaded:" + identity, identity);
            }
        }

        /// <summary>未要求の Scene（AC5）。要求するまで SceneDirector の lifecycle に参加しないか、要求で初めて参加するか。</summary>
        private static async UniTask UnrequestedSceneAsync(HarnessState state, DirectorHarness harness, ExperimentRecord record, Cell cell)
        {
            await RequestSnapshotsAsync(state, harness, record, cell);
            // 前提の行。隔離あり（Play の起点が Boot）では Editor の additive な Other が Play に載らず、
            // 「要求前に参加しない」が自明に満たされる。判定担当がその自明な PASS を区別できるよう、Play 中の Other の数を残す。
            var otherLoaded = 0;
            foreach (var scene in FixtureFactory.LoadedScenesNamed("Other")) if (scene.isLoaded) otherLoaded++;
            record.Write(cell, "ac5.premise", ("isolation", state.isolation), ("otherInMap", state.mapIdentities.Contains("Other")),
                ("otherLoadedInPlayBeforeRequest", otherLoaded),
                ("note", "otherLoadedInPlayBeforeRequest = 0 makes non-participation before the request trivial"));
            Observe(record, cell, harness, "ac5.beforeRequest", "Other", "Entry");
            if (Scenarios.IsUnisolatedBaseline(state.Round.scenario)) return;
            await Step(record, cell, "addScene:Other", () => harness.AddSceneAsync("Other", "request"));
            Observe(record, cell, harness, "ac5.afterRequest", "Other");
        }

        /// <summary>
        /// gate で native の完了の通知を止めた状態で取消し、続けて unload し、gate を開ける（AC6）。
        /// 取消は SceneDirector ではなく `IAssetManagement.LoadContentSceneAsync` に直接掛ける。SceneDirector の取消は
        /// PreLoad の窓に限られ、Unity Scene の load には届かないため（S3 の SceneDirector 経由と直接経由の差の入力）。
        /// 機構 (ii) は snapshot の `Entry` でも行う。機構 (i) の snapshot は Play 前に既ロードなので取消は N/A。
        /// </summary>
        private static async UniTask CancelUnderGateAsync(HarnessState state, DirectorHarness harness, ExperimentRecord record, Cell cell)
        {
            var routing = harness.Routing ?? throw new InvalidOperationException("Cancellation needs a routing backend.");
            var identities = state.Round.mechanism == MechanismTempDirectory.Name ? new[] { "Entry", "Next" } : new[] { "Next" };
            if (state.Round.mechanism == MechanismAlreadyLoaded.Name)
                record.Write(cell, "ac6.snapshotCancel", ("applicable", false), ("reason", "the snapshot is loaded before Play in mechanism (i)"));
            foreach (var identity in identities)
            {
                routing.CloseGate(identity);
                using var cts = new CancellationTokenSource();
                var load = harness.Assets.LoadContentSceneAsync(identity, FixtureFactory.Representation,
                    new SceneLoadOptions(LoadSceneMode.Additive, true, 100), cts.Token).Preserve();
                var frames = await WaitFrames(() => routing.HasNativeCompleted(identity));
                record.Write(cell, "ac6.beforeCancel", ("identity", identity), ("nativeCompleted", routing.HasNativeCompleted(identity)),
                    ("framesWaited", frames), ("routing", routing.Describe()));
                cts.Cancel();
                string outcome;
                try { var handle = await load; outcome = "completed:" + handle.Identity; }
                catch (OperationCanceledException) { outcome = "canceled"; }
                catch (Exception ex) { outcome = "failed:" + ex.GetType().Name + ":" + ex.Message; }
                record.Write(cell, "ac6.callerOutcome", ("identity", identity), ("outcome", outcome), ("ledger", harness.AssetLedgerScenes()));
                await Step(record, cell, "assets.unload:" + identity, () => harness.Assets.UnloadSceneAsync(identity));
                routing.OpenGate(identity);
                // 計数が 0 に戻ることを待ちの条件にしない。回収の失敗は未回収として残り続けるので、待ちは operation の終端だけを見て、
                // 未回収と回収失敗の数は下の行で判定担当が読む。
                var drainFrames = await WaitFrames(() => routing.PendingOperations == 0);
                record.Write(cell, "ac6.afterDrain", ("identity", identity), ("framesWaited", drainFrames), ("routing", routing.Describe()),
                    ("sameName", PlayObserver.SameName(identity)), ("ledger", harness.AssetLedgerScenes()));
            }
        }

        /// <summary>gate で Next の完了を止めたまま Play を停止する（AC7 (b)）。停止後の回収は Edit の検証が見る。</summary>
        private static async UniTask StopUnderGateAsync(HarnessState state, DirectorHarness harness, ExperimentRecord record, Cell cell)
        {
            var routing = harness.Routing ?? throw new InvalidOperationException("Stop under gate needs a routing backend.");
            await RequestSnapshotsAsync(state, harness, record, cell);
            routing.CloseGate("Next");
            harness.Assets.LoadContentSceneAsync("Next", FixtureFactory.Representation, new SceneLoadOptions(LoadSceneMode.Additive, true, 100))
                .Forget(ex => record.Write(cell, "ac7b.pendingLoad.failed", ("error", ex)));
            var frames = await WaitFrames(() => routing.HasNativeCompleted("Next"));
            record.Write(cell, "ac7b.stoppingWithGateClosed", ("nativeCompleted", routing.HasNativeCompleted("Next")), ("framesWaited", frames),
                ("routing", routing.Describe()));
        }

        /// <summary>基準（AC1、AC0）: 名前の再利用で取り込んだ元の Scene の Play 中の実体から GameObject を 1 つ消す。</summary>
        private static UniTask BaselineAsync(DirectorHarness harness, ExperimentRecord record, Cell cell)
        {
            Observe(record, cell, harness, "ac1.registered", "Entry", "Other");
            DestroyOneObject("Entry", record, cell);
            Observe(record, cell, harness, "ac1.afterDestroy", "Entry");
            return UniTask.CompletedTask;
        }

        /// <summary>snapshot の identity を要求する。機構 (i) では既ロードの登録で入っているはずなので、入っていない物だけを要求する。</summary>
        private static async UniTask<List<string>> RequestSnapshotsAsync(HarnessState state, DirectorHarness harness, ExperimentRecord record, Cell cell)
        {
            var targets = new List<string>();
            foreach (var snapshot in state.snapshots)
            {
                if (snapshot.refused) continue;
                targets.Add(snapshot.identity);
                var participates = harness.Ownership(snapshot.identity)["directorParticipates"] is true;
                record.Write(cell, "snapshot.request", ("identity", snapshot.identity), ("alreadyParticipating", participates));
                if (!participates) await Step(record, cell, "addScene:" + snapshot.identity, () => harness.AddSceneAsync(snapshot.identity, "request"));
            }
            return targets;
        }

        private static void Observe(ExperimentRecord record, Cell cell, DirectorHarness harness, string label, params string[] identities)
        {
            var scenes = new Dictionary<string, object?>();
            foreach (var identity in identities)
                scenes[identity] = new Dictionary<string, object?> { ["unity"] = PlayObserver.SameName(identity), ["ownership"] = harness.Ownership(identity) };
            record.Write(cell, "observe", ("label", label), ("identities", scenes), ("addressablesCallsAfterSeal", harness.Addressables.TotalCalls),
                ("sealed", harness.Assets.IsAddressablesSealed));
        }

        private static void DestroyOneObject(string identity, ExperimentRecord record, Cell cell)
        {
            foreach (var scene in FixtureFactory.LoadedScenesNamed(identity))
            {
                if (!scene.isLoaded) continue;
                var target = RoundRunner.FindRoot(scene, "Removable") ?? RoundRunner.FindRoot(scene, "Payload");
                if (target == null) continue;
                record.Write(cell, "play.destroyObject", ("identity", identity), ("object", target.name), ("scene", PlayObserver.Describe(scene)));
                UnityEngine.Object.Destroy(target);
                return;
            }
            record.Write(cell, "play.destroyObject.skipped", ("identity", identity), ("reason", "no loaded scene with a removable object"));
        }

        private static async UniTask Step(ExperimentRecord record, Cell cell, string step, Func<UniTask> action)
        {
            try { await action(); }
            catch (Exception ex)
            {
                record.Write(cell, "play.step.failed", ("step", step), ("error", ex), ("detail", ex.ToString()), ("scenes", PlayObserver.LoadedScenes()));
            }
        }

        // 時間ではなく条件を frame ごとに見る（テストで固定時間の待機をしない）。上限に達したら待った frame 数をそのまま返す。
        private static async UniTask<int> WaitFrames(Func<bool> condition)
        {
            var frames = 0;
            while (!condition() && frames < FrameLimit)
            {
                await UniTask.Yield();
                frames++;
            }
            return frames;
        }
    }
}
