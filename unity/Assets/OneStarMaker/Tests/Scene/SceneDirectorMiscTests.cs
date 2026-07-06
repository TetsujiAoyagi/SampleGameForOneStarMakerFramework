#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>Progress 通知・Dispose・複合シナリオ テスト。</summary>
    [TestFixture]
    public class SceneDirectorMiscTests : SceneDirectorTestBase
    {
        // ═══════════════════════════════════════════
        //  Progress 通知
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator AddScene_Progress_ReportsAllPhases() => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var reports = new List<SceneLoadProgress>();
            // System.Progress<T> は SynchronizationContext 経由で遅延通知されるため、
            // 同期的に記録できるインライン IProgress 実装を使う
            var progress = new InlineProgress<SceneLoadProgress>(p => reports.Add(p));

            await director.AddScene("TestScene", null, CancellationToken.None, progress: progress);

            Assert.IsTrue(reports.Count >= 4,
                $"4フェーズ以上の進捗が報告されるべき: actual={reports.Count}");

            Assert.AreEqual(SceneLoadPhase.PreLoadStarted, reports[0].Phase);
            Assert.IsTrue(reports[0].IsCancelable);

            Assert.AreEqual(SceneLoadPhase.PreLoadCompleted, reports[1].Phase);
            Assert.IsTrue(reports[1].IsCancelable);

            Assert.AreEqual(SceneLoadPhase.UnitySceneLoading, reports[2].Phase);
            Assert.IsFalse(reports[2].IsCancelable);

            Assert.AreEqual(SceneLoadPhase.Completed, reports[3].Phase);
            Assert.IsFalse(reports[3].IsCancelable);
        });

        /// <summary>コールバックを同期実行する IProgress 実装。</summary>
        private sealed class InlineProgress<T> : IProgress<T>
        {
            private readonly Action<T> _handler;
            public InlineProgress(Action<T> handler) => _handler = handler;
            public void Report(T value) => _handler(value);
        }

        // ═══════════════════════════════════════════
        //  Dispose
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Dispose_ReleasesAllScenes() => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            await director.AddScene("TestScene", null, CancellationToken.None);

            Assert.IsTrue(director.ContainsScene("TestScene"));

            director.Dispose();

            Assert.IsFalse(director.ContainsScene("TestScene"));
        });

        // ═══════════════════════════════════════════
        //  複合シナリオ
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator AddScene_LoadAndUnloadAndReload_Works() => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();

            await director.AddScene("TestScene", null, CancellationToken.None);
            Assert.AreEqual(SceneState.Stable, director.GetSceneState("TestScene"));

            await director.UnloadScene("TestScene");
            Assert.IsFalse(director.ContainsScene("TestScene"));

            await director.AddScene("TestScene", null, CancellationToken.None);
            Assert.AreEqual(SceneState.Stable, director.GetSceneState("TestScene"));
        });

        [UnityTest]
        public IEnumerator AddScene_MultipleIndependentScenes_BothLoadable() => UniTask.ToCoroutine(async () =>
        {
            var resA = SceneTestHelper.CreateSceneResource("SceneA");
            var resB = SceneTestHelper.CreateSceneResource("SceneB");
            CreatedSOs.Add(resA);
            CreatedSOs.Add(resB);

            Map = SceneTestHelper.CreateSceneResourceMap(resA, resB);
            CreatedSOs.Add(Map);

            Director = new TestableSceneDirector(Factory, UICommon, Map, AssetManagement);

            await Director.AddScene("SceneA", null, CancellationToken.None);
            await Director.AddScene("SceneB", null, CancellationToken.None);

            Assert.AreEqual(SceneState.Stable, Director.GetSceneState("SceneA"));
            Assert.AreEqual(SceneState.Stable, Director.GetSceneState("SceneB"));

            await Director.UnloadScene("SceneA");
            Assert.IsFalse(Director.ContainsScene("SceneA"));
            Assert.IsTrue(Director.ContainsScene("SceneB"));
        });

        [UnityTest]
        public IEnumerator UnloadScene_ChildOnly_ParentRemains() => UniTask.ToCoroutine(async () =>
        {
            var parentRes = SceneTestHelper.CreateSceneResource("Parent");
            var childRes = SceneTestHelper.CreateSceneResource("Child", LoadType.OnDemand, parentRes);
            SceneTestHelper.AddChild(parentRes, childRes);
            CreatedSOs.Add(parentRes);
            CreatedSOs.Add(childRes);

            Map = SceneTestHelper.CreateSceneResourceMap(parentRes, childRes);
            CreatedSOs.Add(Map);

            Director = new TestableSceneDirector(Factory, UICommon, Map, AssetManagement);

            await Director.AddScene("Parent", null, CancellationToken.None);
            Assert.IsFalse(Director.ContainsScene("Child"));

            await Director.AddScene("Child", null, CancellationToken.None);
            Assert.IsTrue(Director.ContainsScene("Child"));

            await Director.UnloadScene("Child");
            Assert.IsFalse(Director.ContainsScene("Child"));
            Assert.IsTrue(Director.ContainsScene("Parent"),
                "子のアンロードで親は消えてはならない");
        });
    }
}
