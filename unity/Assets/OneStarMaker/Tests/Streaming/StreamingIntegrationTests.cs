#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.Streaming;
using OneStarMaker.Tests.SceneSystem;
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// T-06.5: WorldStreamingController × SceneDirectorStreamingBackend 統合テスト。
    /// FakeBackend では再現できない SceneDirector 実挙動（キャンセル収束・保留アンロード・合流意味論）と
    /// Controller の相互作用を検証する。スケルトン段階では全テストが NotImplementedException でレッド。
    /// </summary>
    [TestFixture]
    public class StreamingIntegrationTests : SceneDirectorTestBase
    {
        private const string World = "World";
        private const float CellSize = 100f;
        private const int ConvergenceMaxTicks = 200;

        // ═══════════════════════════════════════════
        //  セットアップ / ヘルパー
        // ═══════════════════════════════════════════

        private static CellGridConfig CreateGrid() =>
            new(Vector3.zero, CellSize, height: 10f);

        private static Vector3 CellCenter(int x, int y, in CellGridConfig grid) =>
            grid.Origin + new Vector3(
                (x + 0.5f) * grid.CellSize,
                0f,
                (y + 0.5f) * grid.CellSize);

        private static float XzDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static HashSet<string> ComputeCellsWithinRadius(
            Vector3 focus,
            StreamingConfig config,
            float radius)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var grid = config.Grid;

            for (var x = 0; x < config.GridWidth; x++)
            {
                for (var y = 0; y < config.GridHeight; y++)
                {
                    var center = CellCenter(x, y, grid);
                    if (XzDistance(focus, center) <= radius)
                    {
                        result.Add(CellIdentity.Format(x, y));
                    }
                }
            }

            return result;
        }

        private (TestableSceneDirector Director, WorldStreamingController Controller, SceneDirectorStreamingBackend Backend, StreamingConfig Config)
            CreateHarness(
                int gridWidth,
                int gridHeight,
                float loadRadius,
                float unloadRadius,
                int maxInFlight = 4)
        {
            var director = SetupWorldWithCellGrid(gridWidth, gridHeight, World);
            var grid = CreateGrid();
            var config = new StreamingConfig(
                grid, gridWidth, gridHeight, loadRadius, unloadRadius, maxInFlight);
            var backend = new SceneDirectorStreamingBackend(director);
            var controller = new WorldStreamingController(config, backend);
            return (director, controller, backend, config);
        }

        private static async UniTask PumpAsync(
            WorldStreamingController controller,
            Vector3 focus,
            int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                controller.Tick(focus);
                await UniTask.Yield();
            }
        }

        private static async UniTask WaitForConvergenceAsync(
            WorldStreamingController controller,
            SceneDirectorStreamingBackend backend,
            StreamingConfig config,
            Vector3 focus,
            int maxTicks = ConvergenceMaxTicks)
        {
            for (var i = 0; i < maxTicks; i++)
            {
                controller.Tick(focus);
                await UniTask.Yield();

                if (IsResidentSetConverged(backend, config, focus))
                {
                    return;
                }
            }

            Assert.Fail($"収束待ちが {maxTicks} Tick を超えました（focus={focus}）。");
        }

        /// <summary>
        /// desired ⊆ resident ⊆ retain が満たされているかを観測する。
        /// retain 帯のセルはロード済みのまま残っていてよい。
        /// </summary>
        private static bool IsResidentSetConverged(
            SceneDirectorStreamingBackend backend,
            StreamingConfig config,
            Vector3 focus)
        {
            var desired = ComputeCellsWithinRadius(focus, config, config.LoadRadius);
            var retain = ComputeCellsWithinRadius(focus, config, config.UnloadRadius);

            foreach (var cellId in desired)
            {
                if (!backend.IsLoaded(cellId))
                {
                    return false;
                }
            }

            for (var x = 0; x < config.GridWidth; x++)
            {
                for (var y = 0; y < config.GridHeight; y++)
                {
                    var cellId = CellIdentity.Format(x, y);
                    if (retain.Contains(cellId))
                    {
                        continue;
                    }

                    if (backend.IsLoaded(cellId))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void AssertResidentSetMatchesPolicy(
            SceneDirectorStreamingBackend backend,
            StreamingConfig config,
            Vector3 focus)
        {
            var desired = ComputeCellsWithinRadius(focus, config, config.LoadRadius);
            var retain = ComputeCellsWithinRadius(focus, config, config.UnloadRadius);

            foreach (var cellId in desired)
            {
                Assert.IsTrue(
                    backend.IsLoaded(cellId),
                    $"desired セル '{cellId}' はロード済みであるべき（focus={focus}）。");
            }

            for (var x = 0; x < config.GridWidth; x++)
            {
                for (var y = 0; y < config.GridHeight; y++)
                {
                    var cellId = CellIdentity.Format(x, y);
                    if (retain.Contains(cellId))
                    {
                        continue;
                    }

                    Assert.IsFalse(
                        backend.IsLoaded(cellId),
                        $"retain 外セル '{cellId}' はアンロード済みであるべき（focus={focus}）。");
                }
            }
        }

        /// <summary>
        /// 最終常駐セル集合が desired と完全一致するかを検証する（A-3 相当の厳密版）。
        /// unloadRadius をセル中心間距離未満に選び、通過済みセルが retain 外に出る場合に使用する。
        /// </summary>
        private static void AssertResidentSetEqualsDesired(
            SceneDirectorStreamingBackend backend,
            StreamingConfig config,
            Vector3 focus)
        {
            var desired = ComputeCellsWithinRadius(focus, config, config.LoadRadius);

            for (var x = 0; x < config.GridWidth; x++)
            {
                for (var y = 0; y < config.GridHeight; y++)
                {
                    var cellId = CellIdentity.Format(x, y);
                    var expectedLoaded = desired.Contains(cellId);
                    Assert.AreEqual(
                        expectedLoaded,
                        backend.IsLoaded(cellId),
                        $"セル '{cellId}' の IsLoaded は desired 所属と一致すべき（focus={focus}）。");
                }
            }
        }

        private static async UniTask WaitForSceneStateAsync(
            TestableSceneDirector director,
            string sceneId,
            SceneState expected,
            int maxYields = 100)
        {
            for (var i = 0; i < maxYields; i++)
            {
                if (director.ContainsScene(sceneId) && director.GetSceneState(sceneId) == expected)
                {
                    return;
                }

                await UniTask.Yield();
            }

            Assert.Fail($"シーン '{sceneId}' が {expected} に到達しませんでした（{maxYields} yield 超過）。");
        }

        private static async UniTask WaitForSceneRemovedAsync(
            TestableSceneDirector director,
            string sceneId,
            int maxYields = 100)
        {
            for (var i = 0; i < maxYields; i++)
            {
                if (!director.ContainsScene(sceneId))
                {
                    return;
                }

                await UniTask.Yield();
            }

            Assert.Fail($"シーン '{sceneId}' の除去が {maxYields} yield を超えても完了しませんでした。");
        }

        private static TelemetryRecord? FindRecord(
            IReadOnlyList<TelemetryRecord> records,
            TelemetryStartType startType)
        {
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i].Name == startType)
                {
                    return records[i];
                }
            }

            return null;
        }

        private sealed class ExceptionLogCounter : IDisposable
        {
            private int _count;

            public ExceptionLogCounter()
            {
                Application.logMessageReceived += OnLog;
            }

            public int Count => _count;

            private void OnLog(string condition, string stackTrace, LogType type)
            {
                if (type is LogType.Exception or LogType.Error)
                {
                    _count++;
                }
            }

            public void Dispose()
            {
                Application.logMessageReceived -= OnLog;
            }
        }

        private static async UniTask AwaitObservingCancellationAsync(UniTask task, string label)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // キャンセル窓内の OCE は観測済み（施行表 §5: 未観測例外禁止）。
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Assert.Fail($"{label} で予期しない例外: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════
        //  T-06.5 統合テスト（A-3 / A-5 / G-6 相当）
        // ═══════════════════════════════════════════

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator Traversal_FocusSweepsGrid_ResidentSetMatchesDesired()
            => UniTask.ToCoroutine(async () =>
        {
            const int gridWidth = 5;
            const int gridHeight = 1;
            const float loadRadius = 60f;
            // セル中心間距離 100 > unloadRadius のため、通過済みセルは最終 focus で retain 外となり
            // desired == retain == 常駐集合 が成立する。
            const float unloadRadius = 90f;

            var (_, controller, backend, config) = CreateHarness(
                gridWidth, gridHeight, loadRadius, unloadRadius, maxInFlight: 4);

            var grid = config.Grid;

            // focus を左端セル → 右端セルへ段階的に移動し、各位置で Tick + ポンプ
            for (var x = 0; x < gridWidth; x++)
            {
                var focus = CellCenter(x, 0, grid);
                await PumpAsync(controller, focus, ticks: 3);
            }

            var finalFocus = CellCenter(gridWidth - 1, 0, grid);
            await WaitForConvergenceAsync(controller, backend, config, finalFocus);

            AssertResidentSetEqualsDesired(backend, config, finalFocus);
        });

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator FastTraversal_CancelDuringLoad_NoExceptionAndConverges()
            => UniTask.ToCoroutine(async () =>
        {
            const string targetCell = "Cell_0_0";
            const string preLoadCell = "Cell_1_0";
            const float loadRadius = 60f;
            const float unloadRadius = 120f;

            var (director, controller, backend, config) = CreateHarness(
                2, 1, loadRadius, unloadRadius, maxInFlight: 4);

            using var logCounter = new ExceptionLogCounter();

            var grid = config.Grid;
            var nearFocus = CellCenter(0, 0, grid);
            var farFocus = CellCenter(1, 0, grid) + new Vector3(200f, 0f, 0f);

            // ── 経路 A: PoNR 通過後（SceneLoadGates）の保留アンロード ──
            var unityLoadGate = new UniTaskCompletionSource();
            director.SceneLoadGates[targetCell] = unityLoadGate;

            await PumpAsync(controller, nearFocus, ticks: 2);
            await WaitForSceneStateAsync(director, targetCell, SceneState.Loading);

            await PumpAsync(controller, farFocus, ticks: 2);

            unityLoadGate.TrySetResult();
            await WaitForConvergenceAsync(controller, backend, config, farFocus);

            await WaitForSceneRemovedAsync(director, targetCell);

            Assert.IsFalse(
                director.ContainsScene(targetCell),
                "unloadRadius 外へ focus 移動後、保留アンロード経路でセルは消えるべき。");
            Assert.IsFalse(
                director.HasPendingUnload(targetCell),
                "保留アンロードは実行後にクリアされるべき。");

            // ── 経路 B: PreLoad キャンセル窓内（LoadCts.Cancel）──
            var preLoadStarted = new UniTaskCompletionSource();
            Factory.OnCreated = scene =>
            {
                if (scene.SceneResource.Identity != preLoadCell)
                {
                    return;
                }

                scene.PreLoadAction = async ct =>
                {
                    preLoadStarted.TrySetResult();
                    await UniTask.WaitUntilCanceled(ct);
                    ct.ThrowIfCancellationRequested();
                };
            };

            var preLoadFocus = CellCenter(1, 0, grid);
            await PumpAsync(controller, preLoadFocus, ticks: 1);
            await preLoadStarted.Task;

            await PumpAsync(controller, farFocus, ticks: 2);
            await WaitForConvergenceAsync(controller, backend, config, farFocus);

            await WaitForSceneRemovedAsync(director, preLoadCell);

            Assert.IsFalse(
                director.ContainsScene(preLoadCell),
                "PreLoad 窓内の UnloadScene（LoadCts.Cancel）でセルはクリーンアップされるべき。");

            Assert.AreEqual(
                0, logCounter.Count,
                "高速通過シナリオ中に例外/エラーログが出力されてはならない。");
        });

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator JoinedAdd_AfterLeaderCanceled_EventuallyLoads()
            => UniTask.ToCoroutine(async () =>
        {
            const string targetCell = "Cell_1_0";
            const float loadRadius = 80f;
            const float unloadRadius = 160f;

            var (director, controller, backend, config) = CreateHarness(
                2, 1, loadRadius, unloadRadius, maxInFlight: 4);

            var grid = config.Grid;
            var focus = CellCenter(1, 0, grid);

            var preLoadGate = new UniTaskCompletionSource();
            var preLoadEntered = new UniTaskCompletionSource();
            var gateFirstLoadOnly = false;

            Factory.OnCreated = scene =>
            {
                if (scene.SceneResource.Identity != targetCell || gateFirstLoadOnly)
                {
                    return;
                }

                scene.PreLoadAction = async ct =>
                {
                    gateFirstLoadOnly = true;
                    preLoadEntered.TrySetResult();
                    await UniTask.WhenAny(preLoadGate.Task, UniTask.WaitUntilCanceled(ct));
                    ct.ThrowIfCancellationRequested();
                };
            };

            using var leaderCts = new CancellationTokenSource();
            var leaderTask = director.AddScene(targetCell, null, leaderCts.Token);

            await preLoadEntered.Task;

            var joinerTask = backend.RequestAdd(targetCell, priority: 0);

            leaderCts.Cancel();

            await AwaitObservingCancellationAsync(leaderTask, "先発 AddScene");
            await AwaitObservingCancellationAsync(joinerTask, "合流 AddScene");

            preLoadGate.TrySetResult();

            Assert.IsFalse(
                backend.IsLoaded(targetCell),
                "先発キャンセル後、合流側はシーン未ロードのまま正常終了し得る（G-6）。");

            await WaitForConvergenceAsync(controller, backend, config, focus);

            Assert.IsTrue(
                backend.IsLoaded(targetCell),
                "Controller の毎 Tick 再照合により、最終的にセルはロードされるべき（G-6）。");
        });

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator WorldUnload_RemovesAllCells_AndControllerRestarts()
            => UniTask.ToCoroutine(async () =>
        {
            const float loadRadius = 80f;
            const float unloadRadius = 160f;

            var (director, controller, backend, config) = CreateHarness(
                3, 1, loadRadius, unloadRadius, maxInFlight: 4);

            var grid = config.Grid;
            var focus = CellCenter(1, 0, grid);

            await WaitForConvergenceAsync(controller, backend, config, focus);

            var desiredBefore = ComputeCellsWithinRadius(focus, config, config.LoadRadius);
            foreach (var cellId in desiredBefore)
            {
                Assert.IsTrue(backend.IsLoaded(cellId), $"World アンロード前: '{cellId}' はロード済みであるべき。");
            }

            await director.UnloadScene(World);

            Assert.IsFalse(director.ContainsScene(World), "World アンロード後、World は管理下から消えるべき。");
            for (var x = 0; x < config.GridWidth; x++)
            {
                var cellId = CellIdentity.Format(x, 0);
                Assert.IsFalse(
                    director.ContainsScene(cellId),
                    $"World アンロード後、子セル '{cellId}' も再帰破棄されるべき。");
            }

            // InGame 退出→再入場相当: Controller を Stop/Start し、新インスタンスで desired を再構築する。
            var restartedController = new WorldStreamingController(config, backend);
            await WaitForConvergenceAsync(restartedController, backend, config, focus);

            AssertResidentSetMatchesPolicy(backend, config, focus);
        });

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator PendingUnload_CellStabilizes_ThenAutoUnloads()
            => UniTask.ToCoroutine(async () =>
        {
            const string targetCell = "Cell_0_0";
            const float loadRadius = 60f;
            const float unloadRadius = 120f;

            var (director, controller, backend, config) = CreateHarness(
                2, 1, loadRadius, unloadRadius, maxInFlight: 4);

            var grid = config.Grid;
            var nearFocus = CellCenter(0, 0, grid);
            var farFocus = CellCenter(1, 0, grid) + new Vector3(200f, 0f, 0f);

            var loadGate = new UniTaskCompletionSource();
            director.SceneLoadGates[targetCell] = loadGate;

            await PumpAsync(controller, nearFocus, ticks: 2);
            await WaitForSceneStateAsync(director, targetCell, SceneState.Loading);

            Assert.IsFalse(
                backend.IsLoaded(targetCell),
                "Stable 未到達（Loading 中）のセルは IsLoaded で false を返すべき。");

            await PumpAsync(controller, farFocus, ticks: 2);

            Assert.IsTrue(
                director.HasPendingUnload(targetCell),
                "PoNR 通過後に desired から外れたセルは保留アンロードへ登録されるべき。");

            loadGate.TrySetResult();
            await WaitForConvergenceAsync(controller, backend, config, farFocus);

            await WaitForSceneRemovedAsync(director, targetCell);

            Assert.IsFalse(
                director.ContainsScene(targetCell),
                "Stable 到達後、保留アンロードは自動実行されるべき。");
            Assert.IsFalse(
                director.HasPendingUnload(targetCell),
                "保留アンロード実行後、ペンディングはクリアされるべき。");
            Assert.IsFalse(
                backend.IsLoaded(targetCell),
                "アンロード完了後、IsLoaded は false であるべき。");
        });

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator Backend_RequestAdd_PropagatesPriorityAndVerboseTelemetry()
            => UniTask.ToCoroutine(async () =>
        {
            const string cellNear = "Cell_0_0";
            const string cellFar = "Cell_1_0";
            // focus=Cell_0_0 中心: 距離 0 / 100 → desired 2 セル（loadRadius >= 100）
            const float loadRadius = 120f;
            const float unloadRadius = 200f;

            var (director, controller, backend, config) = CreateHarness(
                2, 1, loadRadius, unloadRadius, maxInFlight: 4);

            var grid = config.Grid;
            var nearFocus = CellCenter(0, 0, grid);
            var farFocus = nearFocus + new Vector3(500f, 0f, 0f);

            var sink = new FakeTelemetrySink();
            var originalLevel = AppTelemetry.Level;

            try
            {
                AppTelemetry.Level = TelemetryLevel.Verbose;
                AppTelemetry.AddSink(sink);

                await WaitForConvergenceAsync(controller, backend, config, nearFocus);

                Assert.AreEqual(
                    0, director.LastLoadPriorities[cellNear],
                    "距離が近いセルは priority 0 であるべき");
                Assert.AreEqual(
                    1, director.LastLoadPriorities[cellFar],
                    "距離が遠いセルは priority 1 であるべき");

                var loadRecord = FindRecord(sink.Records, TelemetryStartType.SceneLoad);
                Assert.IsNotNull(loadRecord, "SceneLoad スパンのテレメトリレコードが出力されるべき");

                foreach (var record in sink.Records)
                {
                    if (record.Name == TelemetryStartType.SceneLoad)
                    {
                        Assert.AreEqual(
                            TelemetryLevel.Verbose, record.Level,
                            "SceneLoad スパンは Verbose で出力されるべき");
                    }
                }

                await WaitForConvergenceAsync(controller, backend, config, farFocus);

                await WaitForSceneRemovedAsync(director, cellNear);
                await WaitForSceneRemovedAsync(director, cellFar);

                Assert.IsFalse(director.ContainsScene(cellNear));
                Assert.IsFalse(director.ContainsScene(cellFar));

                var unloadRecord = FindRecord(sink.Records, TelemetryStartType.SceneUnload);
                Assert.IsNotNull(unloadRecord, "SceneUnload スパンのテレメトリレコードが出力されるべき");
                Assert.AreEqual(TelemetryLevel.Verbose, unloadRecord!.Value.Level);

                foreach (var record in sink.Records)
                {
                    if (record.Name == TelemetryStartType.SceneUnload)
                    {
                        Assert.AreEqual(
                            TelemetryLevel.Verbose, record.Level,
                            "SceneUnload スパンは Verbose で出力されるべき");
                    }
                }
            }
            finally
            {
                AppTelemetry.RemoveSink(sink);
                AppTelemetry.Level = originalLevel;
            }
        });
    }
}
