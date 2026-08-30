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
using UnityEngine;
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

        [UnityTest]
        public IEnumerator SwitchScene_StreamByDistanceCandidate_RejectsFromAndTo()
            => UniTask.ToCoroutine(async () =>
        {
            var (director, loadingDisplay, _) = SetupSpatialScenes("Valley", streamByDistance: true);

            var toException = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await director.SwitchScene(null, "Valley", CancellationToken.None));
            Assert.That(toException!.Message, Does.Contain("距離政策の候補"));
            Assert.That(toException.Message, Does.Not.Contain("セル identity"));
            Assert.That(toException.Message, Does.Not.Contain("Cell_"));
            Assert.IsFalse(director.ContainsScene("Valley"));
            Assert.AreEqual(0, director.SceneHistoryCount);
            Assert.AreEqual(0, loadingDisplay.ShowCallCount);
            Assert.AreEqual(0, loadingDisplay.HideCallCount);
            Assert.IsFalse(director.UnitySceneLoadCallCounts.ContainsKey("Valley"));

            await director.AddScene("Title", null, CancellationToken.None);
            var titleState = director.GetSceneState("Title");
            var fromException = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await director.SwitchScene("Valley", "Title", CancellationToken.None));

            Assert.That(fromException!.Message, Does.Contain("距離政策の候補"));
            Assert.AreEqual(titleState, director.GetSceneState("Title"));
            Assert.IsFalse(director.ContainsScene("Valley"));
            Assert.AreEqual(0, director.SceneHistoryCount);
            Assert.AreEqual(0, loadingDisplay.ShowCallCount);
            Assert.AreEqual(0, loadingDisplay.HideCallCount);
        });

        [UnityTest]
        public IEnumerator SwitchScene_StreamByDistanceCandidate_RejectsWithEmptyVolume()
            => UniTask.ToCoroutine(async () =>
        {
            var (director, loadingDisplay, valley) = SetupSpatialScenes("ArbitraryIdentity", streamByDistance: true);
            valley.Volume = new Bounds(Vector3.zero, Vector3.zero);

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await director.SwitchScene(null, "ArbitraryIdentity", CancellationToken.None));

            Assert.That(exception!.Message, Does.Contain("距離政策の候補"));
            Assert.IsFalse(director.ContainsScene("ArbitraryIdentity"));
            Assert.AreEqual(0, director.SceneHistoryCount);
            Assert.AreEqual(0, loadingDisplay.ShowCallCount);
            Assert.AreEqual(0, loadingDisplay.HideCallCount);
        });

        [UnityTest]
        public IEnumerator SwitchScene_FlagOffResources_AreAllowed()
            => UniTask.ToCoroutine(async () =>
        {
            var title = SceneTestHelper.CreateSceneResource("Title");
            var player = SceneTestHelper.CreateSceneResource("PlayerScene");
            var cell = SceneTestHelper.CreateSceneResource("Cell_0_0");
            CreatedSOs.Add(title);
            CreatedSOs.Add(player);
            CreatedSOs.Add(cell);
            Map = SceneTestHelper.CreateSceneResourceMap(title, player, cell);
            CreatedSOs.Add(Map);
            Director = new TestableSceneDirector(Factory, UICommon, Map, AssetManagement);

            Assert.IsFalse(title.StreamByDistance);
            Assert.IsFalse(player.StreamByDistance);
            Assert.IsFalse(cell.StreamByDistance);
            await Director.SwitchScene(null, "Title", CancellationToken.None);
            await Director.SwitchScene(null, "PlayerScene", CancellationToken.None);
            await Director.SwitchScene(null, "Cell_0_0", CancellationToken.None);

            Assert.AreEqual(SceneState.Stable, Director.GetSceneState("Title"));
            Assert.AreEqual(SceneState.Stable, Director.GetSceneState("PlayerScene"));
            Assert.AreEqual(SceneState.Stable, Director.GetSceneState("Cell_0_0"));
            Assert.AreEqual(0, Director.SceneHistoryCount);
        });

        [UnityTest]
        public IEnumerator GoBack_StreamByDistanceCandidate_FailsBeforeMutatingHistory()
            => UniTask.ToCoroutine(async () =>
        {
            var (director, loadingDisplay, valley) = SetupSpatialScenes("Valley", streamByDistance: false);
            await director.AddScene("Title", null, CancellationToken.None);
            await director.SwitchScene("Title", "Valley", CancellationToken.None);
            valley.StreamByDistance = true;

            var historyCount = director.SceneHistoryCount;
            var valleyState = director.GetSceneState("Valley");
            var showCount = loadingDisplay.ShowCallCount;
            var hideCount = loadingDisplay.HideCallCount;
            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await director.GoBack(CancellationToken.None));

            Assert.That(exception!.Message, Does.Contain("距離政策の候補"));
            Assert.AreEqual(historyCount, director.SceneHistoryCount);
            Assert.AreEqual(valleyState, director.GetSceneState("Valley"));
            Assert.IsFalse(director.ContainsScene("Title"));
            Assert.AreEqual(showCount, loadingDisplay.ShowCallCount);
            Assert.AreEqual(hideCount, loadingDisplay.HideCallCount);
        });

        [UnityTest]
        public IEnumerator ExecuteTransitionPlan_StreamByDistanceCandidate_UsesCommonGuard()
            => UniTask.ToCoroutine(async () =>
        {
            var planner = SceneTestHelper.CreateSceneResource("Planner");
            var valley = SceneTestHelper.CreateSceneResource("Valley");
            valley.StreamByDistance = true;
            CreatedSOs.Add(planner);
            CreatedSOs.Add(valley);
            Map = SceneTestHelper.CreateSceneResourceMap(planner, valley);
            CreatedSOs.Add(Map);
            var factory = new PlanSceneFactory();
            Director = new TestableSceneDirector(factory, UICommon, Map, AssetManagement);

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await Director.AddScene("Planner", null, CancellationToken.None));

            Assert.That(exception!.Message, Does.Contain("距離政策の候補"));
            Assert.AreEqual(SceneState.Stable, Director.GetSceneState("Planner"));
            Assert.IsFalse(Director.ContainsScene("Valley"));
            Assert.AreEqual(0, Director.SceneHistoryCount);
        });

        [UnityTest]
        public IEnumerator SwitchScene_UnregisteredIdentity_IsNotRejectedByR3()
            => UniTask.ToCoroutine(async () =>
        {
            var (director, _, _) = SetupSpatialScenes("Unused", streamByDistance: false);

            await director.SwitchScene("Unregistered", "Title", CancellationToken.None);

            Assert.AreEqual(SceneState.Stable, director.GetSceneState("Title"));
            Assert.AreEqual(1, director.SceneHistoryCount);
        });

        [UnityTest]
        public IEnumerator SwitchScene_DestroyedResource_IsNotRejectedByR3()
            => UniTask.ToCoroutine(async () =>
        {
            var title = SceneTestHelper.CreateSceneResource("Title");
            var destroyed = SceneTestHelper.CreateSceneResource("Destroyed");
            CreatedSOs.Add(title);
            CreatedSOs.Add(destroyed);
            Map = SceneTestHelper.CreateSceneResourceMap(title, destroyed);
            CreatedSOs.Add(Map);
            Director = new TestableSceneDirector(Factory, UICommon, Map, AssetManagement);
            UnityEngine.Object.DestroyImmediate(destroyed);

            await Director.SwitchScene("Destroyed", "Title", CancellationToken.None);

            Assert.AreEqual(SceneState.Stable, Director.GetSceneState("Title"));
            Assert.AreEqual(1, Director.SceneHistoryCount);
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

        private (TestableSceneDirector Director, FakeLoadingDisplay LoadingDisplay, SceneResource Candidate)
            SetupSpatialScenes(string candidateIdentity, bool streamByDistance)
        {
            var title = SceneTestHelper.CreateSceneResource("Title");
            var candidate = SceneTestHelper.CreateSceneResource(candidateIdentity);
            candidate.StreamByDistance = streamByDistance;
            candidate.Volume = new Bounds(new Vector3(10f, 0f, 10f), new Vector3(2f, 2f, 2f));
            CreatedSOs.Add(title);
            CreatedSOs.Add(candidate);
            Map = SceneTestHelper.CreateSceneResourceMap(title, candidate);
            CreatedSOs.Add(Map);

            var loadingDisplay = new FakeLoadingDisplay();
            Director = new TestableSceneDirector(
                Factory,
                UICommon,
                Map,
                AssetManagement,
                loadingDisplay);
            return (Director, loadingDisplay, candidate);
        }

        private sealed class PlanSceneFactory : ISceneFactory
        {
            public SceneBase? CreateSceneClass(
                SceneResource sceneResource,
                ISceneQuery sceneQuery,
                ISceneController sceneController)
            {
                if (sceneResource.Identity == "Planner")
                {
                    return new PlanSceneBase(sceneResource, sceneQuery, sceneController);
                }

                return new TestSceneBase(sceneResource, sceneQuery, sceneController);
            }
        }

        private sealed class PlanSceneBase : SceneBase
        {
            public PlanSceneBase(
                SceneResource sceneResource,
                ISceneQuery sceneQuery,
                ISceneController sceneController)
                : base(sceneResource, sceneQuery, sceneController)
            {
            }

            public override SceneTransitionPlan CreateTransitionPlan()
                => new() { NextSceneId = "Valley" };
        }
    }
}
