#nullable enable

using System;
using System.Collections;
using System.Text.RegularExpressions;
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
    /// <summary>通常例外で失敗した Add の回収回帰。</summary>
    [TestFixture]
    public class SceneDirectorFailedLoadCleanupTests : SceneDirectorTestBase
    {
        [UnityTest]
        public IEnumerator AddScene_OnLoadedException_RemovesScene_AndAllowsReAdd()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            Factory.OnCreated = scene =>
            {
                scene.LoadedAction = _ => throw new InvalidOperationException("OnLoaded failure");
            };

            try
            {
                await director.AddScene("TestScene", null, CancellationToken.None);
                Assert.Fail("InvalidOperationException が throw されるべき");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.IsFalse(director.ContainsScene("TestScene"));
            Assert.IsFalse(director.HasPendingUnload("TestScene"));

            Factory.OnCreated = null;
            await director.AddScene("TestScene", null, CancellationToken.None);
            Assert.AreEqual(SceneState.Stable, director.GetSceneState("TestScene"));
        });

        [UnityTest]
        public IEnumerator AddScene_StabledException_KeepsStableScene_WithoutPending()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            Factory.OnCreated = scene =>
            {
                scene.StabledAction = () => throw new InvalidOperationException("Stable failure");
            };

            try
            {
                await director.AddScene("TestScene", null, CancellationToken.None);
                Assert.Fail("InvalidOperationException が throw されるべき");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.AreEqual(SceneState.Stable, director.GetSceneState("TestScene"),
                "Stable 到達後のフック失敗はロード失敗回収の対象にしない");
            Assert.IsFalse(director.HasPendingUnload("TestScene"));
        });

        [UnityTest]
        public IEnumerator AddScene_ChildPreLoadFails_WaitsSibling_ThenCleansBoth()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupParentWithTwoNecessaryChildren();
            var siblingStarted = new UniTaskCompletionSource();
            var siblingRelease = new UniTaskCompletionSource();

            Factory.OnCreated = scene =>
            {
                if (scene.SceneResource.Identity == "ChildA")
                {
                    scene.PreLoadAction = async _ =>
                    {
                        await siblingStarted.Task;
                        throw new InvalidOperationException("ChildA PreLoad failure");
                    };
                }
                else if (scene.SceneResource.Identity == "ChildB")
                {
                    scene.PreLoadAction = async _ =>
                    {
                        siblingStarted.TrySetResult();
                        await siblingRelease.Task;
                    };
                }
            };

            var addTask = director.AddScene("Parent", null, CancellationToken.None);
            await siblingStarted.Task;
            siblingRelease.TrySetResult();

            try
            {
                await addTask;
                Assert.Fail("InvalidOperationException が throw されるべき");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.IsFalse(director.ContainsScene("Parent"));
            Assert.IsFalse(director.ContainsScene("ChildA"));
            Assert.IsFalse(director.ContainsScene("ChildB"));
            Assert.IsFalse(director.HasPendingUnload("ChildA"));
            Assert.IsFalse(director.HasPendingUnload("ChildB"));
        });

        [UnityTest]
        public IEnumerator IncrementalChild_LoadException_CleansChild_KeepsParent()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupParentChild("Parent", "Child", LoadType.IncrementalAlways);
            Factory.OnCreated = scene =>
            {
                if (scene.SceneResource.Identity == "Child")
                {
                    scene.LoadedAction = _ => throw new InvalidOperationException("Incremental OnLoaded failure");
                }
            };

            LogAssert.Expect(LogType.Error, new Regex("Incremental load failed"));

            await director.AddScene("Parent", null, CancellationToken.None);

            for (var i = 0; i < 64 && director.ContainsScene("Child"); i++)
            {
                await UniTask.Yield();
            }

            Assert.AreEqual(SceneState.Stable, director.GetSceneState("Parent"));
            Assert.IsFalse(director.ContainsScene("Child"));
            Assert.IsFalse(director.HasPendingUnload("Child"));

            Factory.OnCreated = null;
            await director.AddScene("Child", null, CancellationToken.None);
            Assert.AreEqual(SceneState.Stable, director.GetSceneState("Child"));
            Assert.AreEqual(SceneState.Stable, director.GetSceneState("Parent"));
        });

        [UnityTest]
        public IEnumerator UnloadScene_DuringFailedAdd_DoesNotLeavePending()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var gate = new UniTaskCompletionSource();
            director.UnitySceneLoadGate = gate;

            Factory.OnCreated = scene =>
            {
                scene.LoadedAction = _ => throw new InvalidOperationException("OnLoaded failure");
            };

            var addTask = director.AddScene("TestScene", null, CancellationToken.None);
            await UniTask.WaitUntil(() =>
                director.ContainsScene("TestScene")
                && director.GetSceneState("TestScene") == SceneState.Loading);
            var unloadTask = director.UnloadScene("TestScene");
            gate.TrySetResult();

            try
            {
                await addTask;
                Assert.Fail("InvalidOperationException が throw されるべき");
            }
            catch (InvalidOperationException)
            {
            }

            await unloadTask;

            Assert.IsFalse(director.ContainsScene("TestScene"));
            Assert.IsFalse(director.HasPendingUnload("TestScene"));
        });

        private TestableSceneDirector SetupParentWithTwoNecessaryChildren()
        {
            var parentRes = SceneTestHelper.CreateSceneResource("Parent");
            var childA = SceneTestHelper.CreateSceneResource("ChildA", LoadType.NecessaryAlways, parentRes);
            var childB = SceneTestHelper.CreateSceneResource("ChildB", LoadType.NecessaryAlways, parentRes);
            SceneTestHelper.AddChild(parentRes, childA);
            SceneTestHelper.AddChild(parentRes, childB);

            CreatedSOs.Add(parentRes);
            CreatedSOs.Add(childA);
            CreatedSOs.Add(childB);

            Map = SceneTestHelper.CreateSceneResourceMap(parentRes, childA, childB);
            CreatedSOs.Add(Map);

            Director = new TestableSceneDirector(Factory, UICommon, Map, AssetManagement);
            return Director;
        }
    }
}
