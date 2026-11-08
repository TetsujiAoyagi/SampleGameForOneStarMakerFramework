#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// 1 件の test が受け持つ round の列を、Edit の準備 → Play → Edit の検証の順に進める。
    /// 状態は全て <see cref="HarnessState"/> の file にあり、Domain Reload で enumerator の局所変数が消えても、
    /// test の enumerator が先頭から再実行されても、file の phase から続きを決める。
    /// N1〜N5 の破れは test の失敗にせず record に残す。test の失敗は harness 自身の健全さに限る（HANDOFF §5）。
    /// </summary>
    internal static partial class RoundRunner
    {
        private const string PhaseKey = "OsmSpikeCdr.Phase";
        private const string InitialSetupKey = "OsmSpikeCdr.InitialSetup";
        private const int GateDrainFrameLimit = 600;

        internal static IEnumerator Run(string testName, EpmSetting epm, Func<List<RoundSpec>> rounds)
        {
            // Unity のエラーのログは record へ移すので、test の失敗にしない（Domain Reload の後も毎回立て直す）。
            LogAssert.ignoreFailingMessages = true;
            if (HarnessState.TryLoad(testName) == null) Begin(testName, epm, rounds());
            while (true)
            {
                LogAssert.ignoreFailingMessages = true;
                var state = HarnessState.Load();
                if (state.Done) break;
                var phase = SessionState.GetString(PhaseKey, string.Empty);
                if (phase.Length == 0)
                {
                    Prepare(state);
                    phase = HarnessState.Load().roundPlays ? "prepared" : "verified";
                    SessionState.SetString(PhaseKey, phase);
                }
                if (phase == "prepared")
                {
                    if (!EditorApplication.isPlaying) yield return new EnterPlayMode(EnterPlayModeMatrix.CurrentExpectsDomainReload());
                    LogAssert.ignoreFailingMessages = true;
                    yield return RoundPlay.Run();
                    SessionState.SetString(PhaseKey, "played");
                    phase = "played";
                }
                if (phase == "played")
                {
                    if (EditorApplication.isPlaying) yield return new ExitPlayMode();
                    LogAssert.ignoreFailingMessages = true;
                    yield return Verify();
                    SessionState.SetString(PhaseKey, "verified");
                }
                var advanced = HarnessState.Load();
                advanced.index++;
                advanced.Save();
                SessionState.EraseString(PhaseKey);
            }
        }

        // ───────────────────────── test の開始と終了 ─────────────────────────

        private static void Begin(string testName, EpmSetting epm, List<RoundSpec> rounds)
        {
            SessionState.EraseString(PhaseKey);
            var state = HarnessState.Begin(testName, epm, rounds);
            var record = state.OpenRecord();
            record.Write(null, "harness.started", ("test", testName), ("unityVersion", Application.unityVersion),
                ("pid", Process.GetCurrentProcess().Id), ("epmSetting", epm.ToString()), ("epmBefore", EnterPlayModeMatrix.Describe()),
                ("rounds", rounds.Count), ("startSceneBefore", PlayEnvironment.CurrentStartScene()),
                ("activeBuildTarget", EditorUserBuildSettings.activeBuildTarget.ToString()), ("batchMode", Application.isBatchMode));
            SaveInitialSetup();
            state.gameBootstrapEnvKey = PlayEnvironment.QuietGameBootstrap() ?? string.Empty;
            record.Write(null, "environment.gameBootstrap", ("present", state.gameBootstrapEnvKey.Length != 0),
                ("envKey", state.gameBootstrapEnvKey), ("mode", "addressables"),
                ("note", "the project's game initializer starts on every Play; the harness does not use it"));
            record.Write(null, "lease.reclaimedAtStart", ("reclaimed", TempRootLease.ReclaimStale()));
            var lease = TempRootLease.Acquire();
            state.leaseId = lease.LeaseId;
            state.leaseToken = lease.Token;
            record.Write(null, "lease.acquired", ("leaseId", lease.LeaseId), ("assetPath", lease.AssetPath), ("diskPath", lease.DiskPath));
            try
            {
                state.fixture = FixtureFactory.Create(lease);
                state.fixtureReady = true;
                record.Write(null, "fixture.created", ("paths", JsonUtility.ToJson(state.fixture)));
            }
            catch (Exception ex)
            {
                state.fixtureError = ex.ToString();
                record.Write(null, "fixture.failed", ("error", ex), ("detail", ex.ToString()));
            }
            if (state.fixtureReady)
            {
                try
                {
                    FixtureFactory.ValidateBuildTarget();
                    state.fixtureDirectory = FixtureFactory.BuildDirectory("cdr-" + lease.LeaseId + "-fx", Path.Combine(lease.DiskPath, "built", "fixture"),
                        new[] { ("Next", state.fixture.buildNext), ("Other", state.fixture.buildOther), ("Dup", state.fixture.buildDup) });
                    state.fixtureDirectoryReady = true;
                    record.Write(null, "fixtureDirectory.built", ("identity", state.fixtureDirectory.identity), ("path", state.fixtureDirectory.path),
                        ("scenes", state.fixtureDirectory.logicalKeys), ("elapsedMs", state.fixtureDirectory.elapsedMs), ("summary", state.fixtureDirectory.summary));
                }
                catch (Exception ex)
                {
                    state.fixtureDirectoryError = ex.ToString();
                    record.Write(null, "fixtureDirectory.failed", ("error", ex), ("detail", ex.ToString()));
                }
                EditorSceneManager.OpenScene(state.fixture.boot, OpenSceneMode.Single);
            }
            EnterPlayModeMatrix.Apply(epm);
            record.Write(null, "epm.applied", ("setting", epm.ToString()), ("current", EnterPlayModeMatrix.Describe()));
            state.Save();
        }

        /// <summary>test の終了（成功・失敗・中断のどれでも TearDown から呼ぶ）。何度呼んでもよい。</summary>
        internal static FinishResult Finish()
        {
            var result = new FinishResult();
            HarnessState? state = null;
            try { state = HarnessState.CurrentPath() != null ? HarnessState.Load() : null; }
            catch (Exception) { state = null; }
            SnapshotWriter.FailAt = null;
            MechanismTempDirectory.FailAt = null;
            foreach (var leftover in new List<RoutingBackend>(RoutingBackend.Unclosed)) leftover.ReclaimFromPreviousRound();
            result.epmRestored = EnterPlayModeMatrix.Restore();
            result.startSceneRestored = PlayEnvironment.RestoreStartScene();
            PlayEnvironment.RestoreGameBootstrap();
            PlayObserver.Unhook();
            RestoreInitialSetup();
            if (state != null)
            {
                var lease = state.Lease();
                result.leaseReleased = lease == null || lease.Release();
                var record = state.OpenRecord();
                result.recordPath = record.FilePath;
                result.recordLines = record.LineCount;
                result.detectorResults = state.detectorResults;
                result.harnessErrors = state.harnessErrors;
                result.roundsCompleted = state.index;
                result.roundsTotal = state.rounds.Count;
                record.Write(null, "harness.finished", ("epmRestored", result.epmRestored), ("epmNow", EnterPlayModeMatrix.Describe()),
                    ("startSceneRestored", result.startSceneRestored), ("leaseReleased", result.leaseReleased),
                    ("roundsCompleted", state.index), ("roundsTotal", state.rounds.Count), ("harnessErrors", state.harnessErrors));
                state.finished = true;
                state.Save();
                result.recordLines = record.LineCount;
            }
            else result.leaseReleased = true;
            PlayEnvironment.StopCapturingLogs();
            HarnessState.Forget();
            SessionState.EraseString(PhaseKey);
            return result;
        }

        internal sealed class FinishResult
        {
            internal bool epmRestored;
            internal bool startSceneRestored;
            internal bool leaseReleased;
            internal string recordPath = "";
            internal long recordLines;
            internal int roundsCompleted;
            internal int roundsTotal;
            internal List<string> detectorResults = new();
            internal List<string> harnessErrors = new();
        }

        // ───────────────────────── round の準備（Edit） ─────────────────────────

        private static void Prepare(HarnessState state)
        {
            state.ResetRound();
            RoundPlay.Current = null;
            var record = state.OpenRecord();
            var cell = state.Cell;
            PlayEnvironment.CaptureLogs(record, cell);
            record.Write(cell, "round.started", ("index", state.index), ("epmNow", EnterPlayModeMatrix.Describe()));
            CheckLeftovers(state, record, cell);
            var scenario = state.Round.scenario;
            try
            {
                if (!Scenarios.Plays(scenario)) { RunWithoutPlay(state, record, cell); return; }
                if (!state.fixtureReady) { Untested(state, record, cell, "harness", "fixture unavailable: " + state.fixtureError); return; }
                var needsDirectory = state.Round.mechanism != Scenarios.NoMechanism;
                if (needsDirectory && !state.fixtureDirectoryReady)
                {
                    Untested(state, record, cell, "fixture-directory", "fixture directory unavailable: " + state.fixtureDirectoryError);
                    return;
                }
                var lease = state.Lease() ?? throw new InvalidOperationException("Temp root lease was lost.");
                if (scenario == Scenarios.StaleRemnant) SeedAndReclaimStale(state, record, cell);
                var targets = SetUpEditor(state, scenario);
                state.mapIdentities = MapIdentities(scenario);
                state.isolation = scenario is not (Scenarios.Baseline or Scenarios.BaselineOtherNoIsolation);
                var t0 = SceneStateProbe.CaptureSetup();
                state.t0 = JsonUtility.ToJson(t0);
                record.Write(cell, "n1.t0", ("setup", t0.scenes), ("active", t0.activeScene));
                if (state.Round.mechanism != Scenarios.NoMechanism)
                {
                    CreateSnapshots(state, lease, targets, record, cell);
                    if (state.Round.mechanism == MechanismTempDirectory.Name)
                    {
                        state.tempDirectory = MechanismTempDirectory.Build(state, lease, record, cell);
                        state.hasTempDirectory = true;
                    }
                }
                var t1 = SceneStateProbe.CaptureSetup();
                state.t1 = JsonUtility.ToJson(t1);
                record.Write(cell, "n1.t1", ("differencesFromT0", SceneStateProbe.Compare(t0, t1)),
                    ("note", "t0→t1 is the side effect of creating the snapshot, recorded apart from mechanism failures"));
                PlayEnvironment.SetStartScene(state.isolation ? state.fixture.boot : null);
                record.Write(cell, "play.startScene", ("isolation", state.isolation), ("startScene", PlayEnvironment.CurrentStartScene()),
                    ("mapIdentities", state.mapIdentities), ("expectedMarker", state.expectedMarker));
                state.roundPlays = true;
            }
            catch (Exception ex)
            {
                var injected = ex is InjectedFailureException;
                record.Write(cell, injected ? "round.injectedFailure" : "round.prepareFailed", ("error", ex), ("detail", ex.ToString()));
                if (!injected) state.harnessErrors.Add(cell.scenario + "#" + state.index + ": " + ex.Message);
                CleanupRound(state, record, cell);
                state.roundPlays = false;
            }
            finally
            {
                if (!state.roundPlays && Scenarios.Plays(scenario))
                {
                    RestorePrefab(state, record, cell);
                    OpenBoot(state);
                }
                state.Save();
            }
        }

        // ───────────────────────── round の検証（Edit） ─────────────────────────

        private static IEnumerator Verify()
        {
            var state = HarnessState.Load();
            var record = state.OpenRecord();
            var cell = state.Cell;
            PlayEnvironment.CaptureLogs(record, cell);
            var harness = RoundPlay.Current;
            record.Write(cell, "play.exited", ("harnessAvailable", harness != null), ("stoppedByQuitting", harness?.Stopped),
                ("routing", harness?.Routing?.Describe()), ("unclosedRouting", RoutingBackend.Unclosed.Count));
            if (harness != null && !harness.Stopped) harness.StopLikeProduct("verify-fallback");
            if (state.Round.scenario == Scenarios.StopDuringGate && harness?.Routing != null)
            {
                // Play の停止の後で gate を開け、遅れて完了した load が回収されるか、登録の解除がその後に起きるかを見る（AC7 (b)）。
                var routing = harness.Routing;
                record.Write(cell, "ac7b.afterStop.beforeGate", ("routing", routing.Describe()));
                routing.OpenAllGates();
                for (var frame = 0; frame < GateDrainFrameLimit && (routing.PendingOperations != 0 || !routing.IsClosed); frame++)
                    yield return null;
                record.Write(cell, "ac7b.afterStop.afterGate", ("routing", routing.Describe()), ("framesLimit", GateDrainFrameLimit));
            }
            PlayObserver.Unhook();
            var t0 = JsonUtility.FromJson<EditorSetupState>(state.t0);
            var t1 = JsonUtility.FromJson<EditorSetupState>(state.t1);
            if (state.Round.scenario is Scenarios.DetectorRewriteSave or Scenarios.DetectorClearDirty) Tamper(state, record, cell);
            var t2 = SceneStateProbe.CaptureSetup();
            var t0t2 = SceneStateProbe.Compare(t0, t2);
            record.Write(cell, "n1.t2", ("differencesFromT0", t0t2), ("differencesFromT1", SceneStateProbe.Compare(t1, t2)),
                ("setup", t2.scenes), ("active", t2.activeScene));
            if (state.Round.scenario is Scenarios.DetectorRewriteSave or Scenarios.DetectorClearDirty)
            {
                var expected = state.Round.scenario == Scenarios.DetectorRewriteSave ? new[] { "sha256", "hierarchy" } : new[] { "isDirty" };
                var detected = true;
                foreach (var field in expected) detected &= t0t2.Exists(x => x.EndsWith("." + field, StringComparison.Ordinal));
                state.detectorResults.Add(state.Round.scenario + "=" + (detected ? "detected" : "missed"));
                record.Write(cell, "ac0.detector", ("expectedFields", expected), ("detected", detected));
            }
            if (state.prefabT0.Length != 0)
            {
                var prefabT0 = JsonUtility.FromJson<SceneState>(state.prefabT0);
                record.Write(cell, "g22.referencedPrefab", ("prefabChangedDuringRound", SceneStateProbe.Compare(prefabT0, SceneStateProbe.CaptureFile(state.fixture.prefab))),
                    ("note", "the snapshot hash covers the scene asset only, not referenced assets"));
            }
            CleanupRound(state, record, cell);
            RestorePrefab(state, record, cell);
            OpenBoot(state);
            state.Save();
        }

        // 負の対照（AC0）。Play の停止の後、t2 を採る前に、harness が元の Scene を意図的に壊す。
        private static void Tamper(HarnessState state, ExperimentRecord record, Cell cell)
        {
            var entry = SceneStateProbe.FindOpen(state.fixture.entry);
            if (!entry.IsValid()) { record.Write(cell, "ac0.tamper.skipped", ("reason", "entry-not-open")); return; }
            if (state.Round.scenario == Scenarios.DetectorRewriteSave) MovePayload(entry, 7f, true);
            var saved = EditorSceneManager.SaveScene(entry);
            record.Write(cell, "ac0.tamper", ("kind", state.Round.scenario), ("saved", saved));
        }

    }
}
