#nullable enable

using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.SceneSystem
{
    /// <summary>
    /// T-01: 並行 AddScene の親共有競合の再現テスト（21-scene-streaming.md §5 H-1）。
    ///
    /// SceneStreaming では WorldStreamingController が複数セルの AddScene を
    /// await せず並行に発行する。現行実装は「一度に1遷移」しか想定しておらず、
    /// 以下の競合経路が存在する:
    ///
    ///   (a) 親(World)が Loading 中に後発 AddScene が LoadUnityScene(World) へ突入し、
    ///       Loading → Loading の二重状態遷移で InvalidOperationException
    ///   (b) 親が PreLoading 中の場合、LoadSceneBase の既存シーン分岐（IsNone のみ判定）が
    ///       PreLoad 完了を待たずに素通りし、(a) と同じ二重遷移へ到達する
    ///   (c) 同一 identity の並行 AddScene は先頭ガードで即 return するため、
    ///       後発の awaiter は Stable 到達前に完了してしまう
    ///
    /// 本フィクスチャは「修正後にあるべき挙動」を主張する。
    /// T-02（識別子ごとの in-flight タスク共有）が入るまで全テストはレッドである。
    /// </summary>
    [TestFixture]
    public class SceneDirectorConcurrentAddSceneTests : SceneDirectorTestBase
    {
        private const string World = "World";
        private const string CellA = "Cell_0_0";
        private const string CellB = "Cell_0_1";

        /// <summary>
        /// ストリーミング相当のツリーを構築する:
        /// World（親）＋ OnDemand の子セル2つ。
        /// セルは OnDemand なので World ロード時に自動ロードされず、
        /// AddScene(cell) が親 World を暗黙にロードする経路を通る。
        /// </summary>
        private TestableSceneDirector SetupWorldWithTwoCells()
        {
            var worldRes = SceneTestHelper.CreateSceneResource(World);
            var cellARes = SceneTestHelper.CreateSceneResource(CellA, LoadType.OnDemand, worldRes);
            var cellBRes = SceneTestHelper.CreateSceneResource(CellB, LoadType.OnDemand, worldRes);
            SceneTestHelper.AddChild(worldRes, cellARes);
            SceneTestHelper.AddChild(worldRes, cellBRes);

            CreatedSOs.Add(worldRes);
            CreatedSOs.Add(cellARes);
            CreatedSOs.Add(cellBRes);

            Map = SceneTestHelper.CreateSceneResourceMap(worldRes, cellARes, cellBRes);
            CreatedSOs.Add(Map);

            Director = new TestableSceneDirector(Factory, UICommon, Map, AssetManagement);
            return Director;
        }

        // ═══════════════════════════════════════════
        //  ハーネス健全性（並行なし）: 現行実装でもグリーンであるべき
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Sequential_AddTwoCells_SharedParentLoadsOnce() => UniTask.ToCoroutine(async () =>
        {
            var director = SetupWorldWithTwoCells();

            await director.AddScene(CellA, null, CancellationToken.None);
            await director.AddScene(CellB, null, CancellationToken.None);

            Assert.AreEqual(SceneState.Stable, director.GetSceneState(World));
            Assert.AreEqual(SceneState.Stable, director.GetSceneState(CellA));
            Assert.AreEqual(SceneState.Stable, director.GetSceneState(CellB));
            Assert.AreEqual(1, director.UnitySceneLoadCallCounts[World],
                "順次 AddScene でも共有親の Unity Scene ロードは1回であるべき");
        });

        // ═══════════════════════════════════════════
        //  経路 (a): 親の Unity Scene ロード中に後発 AddScene が突入
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Concurrent_AddTwoCells_WhileParentUnitySceneLoading_BothReachStable()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupWorldWithTwoCells();

            // World の Unity Scene ロードだけを保留し、「親が Loading 中」を固定する
            var worldGate = new UniTaskCompletionSource();
            director.SceneLoadGates[World] = worldGate;

            var taskA = director.AddScene(CellA, null, CancellationToken.None);
            // この時点で taskA は LoadUnityScene(World) のゲートで停止している（World = Loading）
            var taskB = director.AddScene(CellB, null, CancellationToken.None);

            worldGate.TrySetResult();

            // 現行実装: taskB が Loading → Loading の二重遷移で
            // InvalidOperationException を投げ、ここで失敗する
            await UniTask.WhenAll(taskA, taskB);

            Assert.AreEqual(SceneState.Stable, director.GetSceneState(World));
            Assert.AreEqual(SceneState.Stable, director.GetSceneState(CellA));
            Assert.AreEqual(SceneState.Stable, director.GetSceneState(CellB));
        });

        [UnityTest]
        public IEnumerator Concurrent_AddTwoCells_SharedParentUnitySceneLoadsExactlyOnce()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupWorldWithTwoCells();

            var worldGate = new UniTaskCompletionSource();
            director.SceneLoadGates[World] = worldGate;

            var taskA = director.AddScene(CellA, null, CancellationToken.None);
            var taskB = director.AddScene(CellB, null, CancellationToken.None);

            worldGate.TrySetResult();
            await UniTask.WhenAll(taskA, taskB);

            Assert.AreEqual(1, director.UnitySceneLoadCallCounts[World],
                "共有親の PerformUnitySceneLoad は並行 AddScene でも1回であるべき" +
                "（後発は in-flight タスクを await して合流する）");
            Assert.AreEqual(1, director.UnitySceneLoadCallCounts[CellA]);
            Assert.AreEqual(1, director.UnitySceneLoadCallCounts[CellB]);
        });

        // ═══════════════════════════════════════════
        //  経路 (b): 親の PreLoad 中に後発 AddScene が素通りする
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Concurrent_AddTwoCells_WhileParentPreLoading_WaitsAndPreLoadsOnce()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupWorldWithTwoCells();

            // World の OnPreLoadedImpl を保留し、「親が PreLoading 中」を固定する
            var worldPreLoadGate = new UniTaskCompletionSource();
            var worldPreLoadCount = 0;
            Factory.OnCreated = scene =>
            {
                if (scene.SceneResource.Identity == World)
                {
                    scene.PreLoadAction = async ct =>
                    {
                        worldPreLoadCount++;
                        await worldPreLoadGate.Task;
                    };
                }
            };

            var taskA = director.AddScene(CellA, null, CancellationToken.None);
            // この時点で taskA は World の PreLoad ゲートで停止している（World = PreLoading）
            var taskB = director.AddScene(CellB, null, CancellationToken.None);

            worldPreLoadGate.TrySetResult();

            // 現行実装: taskB は World の PreLoad 完了を待たずに素通りし、
            // PreLoading → Loading の無効遷移で InvalidOperationException を投げる
            await UniTask.WhenAll(taskA, taskB);

            Assert.AreEqual(1, worldPreLoadCount,
                "共有親の OnPreLoadedImpl は並行 AddScene でも1回であるべき");
            Assert.AreEqual(SceneState.Stable, director.GetSceneState(World));
            Assert.AreEqual(SceneState.Stable, director.GetSceneState(CellA));
            Assert.AreEqual(SceneState.Stable, director.GetSceneState(CellB));
        });

        // ═══════════════════════════════════════════
        //  経路 (c): 同一 identity の並行 AddScene
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Concurrent_AddSameCellTwice_SecondAwaiterCompletesOnlyAfterStable()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupWorldWithTwoCells();

            var worldGate = new UniTaskCompletionSource();
            director.SceneLoadGates[World] = worldGate;

            var taskA = director.AddScene(CellA, null, CancellationToken.None);
            var taskB = director.AddScene(CellA, null, CancellationToken.None);

            // 現行実装: 先頭ガードが「ロード中ならスキップ」で即 return するため、
            // taskB は CellA が Stable に到達する前に完了してしまう。
            // ストリーミングでは「AddScene の完了 = セル利用可能」を信頼できる必要がある。
            // アサートはゲート解放後に行い、失敗時も director の後始末を安全にする。
            var statusBeforeGateOpen = taskB.Status;

            worldGate.TrySetResult();
            await UniTask.WhenAll(taskA, taskB);

            Assert.AreEqual(UniTaskStatus.Pending, statusBeforeGateOpen,
                "同一 identity の後発 AddScene は in-flight ロードの完了を待つべき");

            Assert.AreEqual(SceneState.Stable, director.GetSceneState(CellA));
            Assert.AreEqual(1, director.UnitySceneLoadCallCounts[CellA],
                "同一 identity の並行 AddScene で Unity Scene ロードは1回であるべき");
        });

        // ═══════════════════════════════════════════
        //  並行ロード中の UnloadScene 収束（ストリーミングの高速通過相当）
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Concurrent_AddTwoCells_UnloadOneDuringParentLoad_Converges()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupWorldWithTwoCells();

            var worldGate = new UniTaskCompletionSource();
            director.SceneLoadGates[World] = worldGate;

            var taskA = director.AddScene(CellA, null, CancellationToken.None);
            var taskB = director.AddScene(CellB, null, CancellationToken.None);

            // 高速通過: CellB は PoNR 通過済み（LoadCts=null）なので保留アンロードに登録される
            await director.UnloadScene(CellB);

            worldGate.TrySetResult();
            await UniTask.WhenAll(taskA, taskB);

            Assert.AreEqual(SceneState.Stable, director.GetSceneState(CellA),
                "アンロード対象でない CellA は Stable へ到達すべき");
            Assert.IsFalse(director.ContainsScene(CellB),
                "ロード中に要求された CellB のアンロードは Stable 到達後に収束すべき");
            Assert.IsFalse(director.HasPendingUnload(CellB),
                "保留アンロードは実行後にクリアされるべき");
            Assert.AreEqual(SceneState.Stable, director.GetSceneState(World),
                "子セルのアンロードで共有親 World は消えてはならない");
        });
    }
}
