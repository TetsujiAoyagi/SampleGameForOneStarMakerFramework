#nullable enable

using System;
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
    /// <summary>SwitchScene / GoBack と遷移履歴のテスト。</summary>
    [TestFixture]
    public sealed class SceneDirectorTransitionTests : SceneDirectorTestBase
    {
        [UnityTest]
        public IEnumerator SwitchScene_ReplacesScene_AndRecordsHistory() => UniTask.ToCoroutine(async () =>
        {
            var (director, loadingDisplay) = SetupSiblingScenes();
            await director.AddScene("A", null, CancellationToken.None);

            await director.SwitchScene("A", "B", CancellationToken.None);

            Assert.IsFalse(director.ContainsScene("A"));
            Assert.AreEqual(SceneState.Stable, director.GetSceneState("B"));
            Assert.AreEqual(1, director.SceneHistoryCount);
            Assert.IsTrue(director.CanGoBack);
            Assert.AreEqual(1, loadingDisplay.ShowCallCount);
            Assert.AreEqual(1, loadingDisplay.HideCallCount);
        });

        [UnityTest]
        public IEnumerator GoBack_RestoresPreviousScene_AndConsumesHistory() => UniTask.ToCoroutine(async () =>
        {
            var (director, _) = SetupSiblingScenes();
            await director.AddScene("A", null, CancellationToken.None);
            await director.SwitchScene("A", "B", CancellationToken.None);

            await director.GoBack(CancellationToken.None);

            Assert.AreEqual(SceneState.Stable, director.GetSceneState("A"));
            Assert.IsFalse(director.ContainsScene("B"));
            Assert.AreEqual(0, director.SceneHistoryCount);
            Assert.IsFalse(director.CanGoBack);
        });

        [UnityTest]
        public IEnumerator GoBack_WithEmptyHistory_ThrowsInvalidOperationException() => UniTask.ToCoroutine(async () =>
        {
            var (director, _) = SetupSiblingScenes();

            try
            {
                await director.GoBack(CancellationToken.None);
                Assert.Fail("履歴が空の GoBack は InvalidOperationException を投げるべき");
            }
            catch (InvalidOperationException)
            {
                // 期待どおり
            }
        });

        [UnityTest]
        public IEnumerator SwitchScene_WithoutSource_DoesNotRecordHistory() => UniTask.ToCoroutine(async () =>
        {
            var (director, _) = SetupSiblingScenes();

            await director.SwitchScene(null, "B", CancellationToken.None);

            Assert.AreEqual(SceneState.Stable, director.GetSceneState("B"));
            Assert.AreEqual(0, director.SceneHistoryCount);
            Assert.IsFalse(director.CanGoBack);
        });

        [UnityTest]
        public IEnumerator ClearHistory_MakesGoBackUnavailable() => UniTask.ToCoroutine(async () =>
        {
            var (director, _) = SetupSiblingScenes();
            await director.AddScene("A", null, CancellationToken.None);
            await director.SwitchScene("A", "B", CancellationToken.None);

            director.ClearHistory();

            Assert.AreEqual(0, director.SceneHistoryCount);
            Assert.IsFalse(director.CanGoBack);
        });

        [UnityTest]
        public IEnumerator SwitchScene_CanceledWhileShowing_DoesNotChangeScenesOrHistory()
            => UniTask.ToCoroutine(async () =>
        {
            var (director, loadingDisplay) = SetupSiblingScenes();
            await director.AddScene("A", null, CancellationToken.None);

            var showGate = new UniTaskCompletionSource();
            loadingDisplay.ShowGate = showGate;
            using var cts = new CancellationTokenSource();
            var switchTask = director.SwitchScene("A", "B", cts.Token);

            await UniTask.Yield();
            cts.Cancel();

            try
            {
                await switchTask;
                Assert.Fail("Show 中のキャンセルは OperationCanceledException を投げるべき");
            }
            catch (OperationCanceledException)
            {
                // 期待どおり
            }

            Assert.AreEqual(SceneState.Stable, director.GetSceneState("A"));
            Assert.IsFalse(director.ContainsScene("B"));
            Assert.AreEqual(0, director.SceneHistoryCount);
            Assert.AreEqual(0, loadingDisplay.HideCallCount);
        });

        [UnityTest]
        public IEnumerator SwitchScene_CanceledAfterPointOfNoReturn_CompletesTransition()
            => UniTask.ToCoroutine(async () =>
        {
            var (director, _) = SetupSiblingScenes();
            await director.AddScene("A", null, CancellationToken.None);

            var loadGate = new UniTaskCompletionSource();
            director.SceneLoadGates["B"] = loadGate;
            using var cts = new CancellationTokenSource();
            var switchTask = director.SwitchScene("A", "B", cts.Token);

            await UniTask.WaitUntil(() => director.UnitySceneLoadCallCounts.ContainsKey("B"));
            cts.Cancel();
            loadGate.TrySetResult();

            await switchTask;

            Assert.IsFalse(director.ContainsScene("A"));
            Assert.AreEqual(SceneState.Stable, director.GetSceneState("B"));
            Assert.AreEqual(1, director.SceneHistoryCount);
        });

        private (TestableSceneDirector Director, FakeLoadingDisplay LoadingDisplay) SetupSiblingScenes()
        {
            var sceneA = SceneTestHelper.CreateSceneResource("A", LoadType.OnDemand);
            var sceneB = SceneTestHelper.CreateSceneResource("B", LoadType.OnDemand);
            CreatedSOs.Add(sceneA);
            CreatedSOs.Add(sceneB);

            Map = SceneTestHelper.CreateSceneResourceMap(sceneA, sceneB);
            CreatedSOs.Add(Map);

            var loadingDisplay = new FakeLoadingDisplay();
            Director = new TestableSceneDirector(
                Factory,
                UICommon,
                Map,
                AssetManagement,
                loadingDisplay);
            return (Director, loadingDisplay);
        }
    }
}
