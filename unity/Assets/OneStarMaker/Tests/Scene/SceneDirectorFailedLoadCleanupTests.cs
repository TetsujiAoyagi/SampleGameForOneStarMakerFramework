#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
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

        [UnityTest]
        public IEnumerator AddScene_ViewInException_RemovesInitializingScene_AndAllowsReAdd()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var roots = new List<GameObject>();
            director.RootObjectsFactory = _ => new[] { CreateRootWithViewInError(roots, "ViewIn failure") };

            try
            {
                await director.AddScene("TestScene", null, CancellationToken.None);
                Assert.Fail("InvalidOperationException が throw されるべき");
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                DestroyRoots(roots);
            }

            Assert.IsFalse(director.ContainsScene("TestScene"));
            Assert.IsFalse(director.HasPendingUnload("TestScene"));

            director.RootObjectsFactory = null;
            await director.AddScene("TestScene", null, CancellationToken.None);
            Assert.AreEqual(SceneState.Stable, director.GetSceneState("TestScene"));
        });

        [UnityTest]
        public IEnumerator UnloadScene_DuringInitializing_RemovesScene_AndAllowsReAdd()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var viewInEntered = new UniTaskCompletionSource();
            var viewInRelease = new UniTaskCompletionSource();
            var roots = new List<GameObject>();
            director.RootObjectsFactory = _ => new[]
            {
                CreateRootWithGatedViewIn(roots, viewInEntered, viewInRelease)
            };

            var addTask = director.AddScene("TestScene", null, CancellationToken.None);
            await viewInEntered.Task;
            Assert.AreEqual(SceneState.Initializing, director.GetSceneState("TestScene"));

            var unloadTask = director.UnloadScene("TestScene");
            viewInRelease.TrySetResult();

            await unloadTask;
            await addTask;

            Assert.IsFalse(director.ContainsScene("TestScene"));
            Assert.IsFalse(director.HasPendingUnload("TestScene"));
            DestroyRoots(roots);

            director.RootObjectsFactory = null;
            await director.AddScene("TestScene", null, CancellationToken.None);
            Assert.AreEqual(SceneState.Stable, director.GetSceneState("TestScene"));
        });

        [UnityTest]
        public IEnumerator Recover_ResumesFailedPreUnload_AndAllowsReAdd()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var viewInEntered = new UniTaskCompletionSource();
            var viewInRelease = new UniTaskCompletionSource();
            var preUnloadEntered = new UniTaskCompletionSource();
            var preUnloadRelease = new UniTaskCompletionSource();
            var roots = new List<GameObject>();
            var throwPreUnload = true;
            director.RootObjectsFactory = _ => new[]
            {
                CreateRootWithViewInErrorAfterGate(
                    roots, viewInEntered, viewInRelease, "ViewIn failure after unload started")
            };

            Factory.OnCreated = scene =>
            {
                scene.PreUnLoadAction = async () =>
                {
                    preUnloadEntered.TrySetResult();
                    await preUnloadRelease.Task;
                    if (throwPreUnload)
                    {
                        throwPreUnload = false;
                        throw new InvalidOperationException("PreUnload failure");
                    }
                };
            };

            var addTask = director.AddScene("TestScene", null, CancellationToken.None);
            await viewInEntered.Task;
            var unloadTask = director.UnloadScene("TestScene");
            await preUnloadEntered.Task;
            viewInRelease.TrySetResult();
            preUnloadRelease.TrySetResult();

            try
            {
                await addTask;
                Assert.Fail("InvalidOperationException が throw されるべき");
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                await unloadTask;
            }
            catch (InvalidOperationException)
            {
            }

            Assert.IsFalse(director.ContainsScene("TestScene"));
            Assert.IsFalse(director.HasPendingUnload("TestScene"));
            DestroyRoots(roots);

            Factory.OnCreated = null;
            director.RootObjectsFactory = null;
            await director.AddScene("TestScene", null, CancellationToken.None);
            Assert.AreEqual(SceneState.Stable, director.GetSceneState("TestScene"));
        });

        [UnityTest]
        public IEnumerator Recover_ResumesFailedUnityUnload_AndAllowsReAdd()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var viewInEntered = new UniTaskCompletionSource();
            var viewInRelease = new UniTaskCompletionSource();
            var unloadEntered = new UniTaskCompletionSource();
            var unloadRelease = new UniTaskCompletionSource();
            var roots = new List<GameObject>();
            var throwUnload = true;
            director.RootObjectsFactory = _ => new[]
            {
                CreateRootWithViewInErrorAfterGate(
                    roots, viewInEntered, viewInRelease, "ViewIn failure after unload started")
            };
            director.UnitySceneUnloadAction = async _ =>
            {
                unloadEntered.TrySetResult();
                await unloadRelease.Task;
                if (throwUnload)
                {
                    throwUnload = false;
                    throw new InvalidOperationException("Unity unload failure");
                }
            };

            var addTask = director.AddScene("TestScene", null, CancellationToken.None);
            await viewInEntered.Task;
            var unloadTask = director.UnloadScene("TestScene");
            await unloadEntered.Task;
            viewInRelease.TrySetResult();
            unloadRelease.TrySetResult();

            try
            {
                await addTask;
                Assert.Fail("InvalidOperationException が throw されるべき");
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                await unloadTask;
            }
            catch (InvalidOperationException)
            {
            }

            Assert.IsFalse(director.ContainsScene("TestScene"));
            Assert.IsFalse(director.HasPendingUnload("TestScene"));
            DestroyRoots(roots);

            director.RootObjectsFactory = null;
            director.UnitySceneUnloadAction = null;
            await director.AddScene("TestScene", null, CancellationToken.None);
            Assert.AreEqual(SceneState.Stable, director.GetSceneState("TestScene"));
        });

        [UnityTest]
        public IEnumerator Recover_ResumesFailedAfterUnload_AndAllowsReAdd()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var viewInEntered = new UniTaskCompletionSource();
            var viewInRelease = new UniTaskCompletionSource();
            var afterEntered = new UniTaskCompletionSource();
            var afterRelease = new UniTaskCompletionSource();
            var roots = new List<GameObject>();
            var throwAfter = true;
            director.RootObjectsFactory = _ => new[]
            {
                CreateRootWithViewInErrorAfterGate(
                    roots, viewInEntered, viewInRelease, "ViewIn failure after unload started")
            };

            Factory.OnCreated = scene =>
            {
                scene.AfterUnLoadAction = async () =>
                {
                    afterEntered.TrySetResult();
                    await afterRelease.Task;
                    if (throwAfter)
                    {
                        throwAfter = false;
                        throw new InvalidOperationException("AfterUnload failure");
                    }
                };
            };

            var addTask = director.AddScene("TestScene", null, CancellationToken.None);
            await viewInEntered.Task;
            var unloadTask = director.UnloadScene("TestScene");
            await afterEntered.Task;
            viewInRelease.TrySetResult();
            afterRelease.TrySetResult();

            try
            {
                await addTask;
                Assert.Fail("InvalidOperationException が throw されるべき");
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                await unloadTask;
            }
            catch (InvalidOperationException)
            {
            }

            Assert.IsFalse(director.ContainsScene("TestScene"));
            Assert.IsFalse(director.HasPendingUnload("TestScene"));
            DestroyRoots(roots);

            Factory.OnCreated = null;
            director.RootObjectsFactory = null;
            await director.AddScene("TestScene", null, CancellationToken.None);
            Assert.AreEqual(SceneState.Stable, director.GetSceneState("TestScene"));
        });

        [UnityTest]
        public IEnumerator Recover_ResumeHookKeepsFailing_ClearsPendingAndLogsLeftover()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var gate = new UniTaskCompletionSource();
            var roots = new List<GameObject>();
            director.UnitySceneLoadGate = gate;
            director.RootObjectsFactory = _ => new[] { CreateRootWithViewInError(roots, "ViewIn failure") };

            Factory.OnCreated = scene =>
            {
                scene.PreUnLoadAction = () =>
                    throw new InvalidOperationException("PreUnload always fails");
            };

            var addTask = director.AddScene("TestScene", null, CancellationToken.None);
            await UniTask.WaitUntil(() =>
                director.ContainsScene("TestScene")
                && director.GetSceneState("TestScene") == SceneState.Loading);
            var unloadTask = director.UnloadScene("TestScene");
            Assert.IsTrue(director.HasPendingUnload("TestScene"));

            LogAssert.Expect(LogType.Exception, new Regex("PreUnload always fails"));
            LogAssert.Expect(LogType.Exception, new Regex("PreUnload always fails"));
            LogAssert.Expect(LogType.Error, new Regex("Failed-load recovery left TestScene"));

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

            Assert.IsFalse(director.HasPendingUnload("TestScene"));
            Assert.IsTrue(director.ContainsScene("TestScene"));
            Assert.AreEqual(SceneState.PreUnloading, director.GetSceneState("TestScene"));
            DestroyRoots(roots);
        });

        [UnityTest]
        public IEnumerator Recover_ResumeHookKeepsFailing_DoesNotSkipSiblingCleanup()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupParentWithTwoNecessaryChildren();
            var viewInEntered = new UniTaskCompletionSource();
            var viewInRelease = new UniTaskCompletionSource();
            var roots = new List<GameObject>();
            director.RootObjectsFactory = id => id == "Parent"
                ? new[]
                {
                    CreateRootWithViewInErrorAfterGate(
                        roots, viewInEntered, viewInRelease, "ViewIn failure after ChildA unload")
                }
                : Array.Empty<GameObject>();

            Factory.OnCreated = scene =>
            {
                if (scene.SceneResource.Identity == "ChildA")
                {
                    scene.PreUnLoadAction = () =>
                        throw new InvalidOperationException("ChildA PreUnload always fails");
                }
            };

            var addTask = director.AddScene("Parent", null, CancellationToken.None);
            await viewInEntered.Task;

            try
            {
                await director.UnloadScene("ChildA");
            }
            catch (InvalidOperationException)
            {
            }

            LogAssert.Expect(LogType.Exception, new Regex("ChildA PreUnload always fails"));
            LogAssert.Expect(LogType.Error, new Regex("Failed-load recovery left ChildA"));

            viewInRelease.TrySetResult();

            try
            {
                await addTask;
                Assert.Fail("InvalidOperationException が throw されるべき");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.IsFalse(director.ContainsScene("Parent"));
            Assert.IsFalse(director.ContainsScene("ChildB"));
            Assert.IsTrue(director.ContainsScene("ChildA"));
            Assert.AreEqual(SceneState.PreUnloading, director.GetSceneState("ChildA"));
            Assert.IsFalse(director.HasPendingUnload("Parent"));
            Assert.IsFalse(director.HasPendingUnload("ChildA"));
            Assert.IsFalse(director.HasPendingUnload("ChildB"));
            DestroyRoots(roots);
        });

        private static GameObject CreateRootWithViewInError(List<GameObject> roots, string message)
        {
            var go = new GameObject("TestRoot");
            var view = go.AddComponent<ThrowingTestUIView>();
            view.Message = message;
            roots.Add(go);
            return go;
        }

        private static GameObject CreateRootWithGatedViewIn(
            List<GameObject> roots,
            UniTaskCompletionSource entered,
            UniTaskCompletionSource release)
        {
            var go = new GameObject("TestRoot");
            var view = go.AddComponent<GatedTestUIView>();
            view.Entered = entered;
            view.Release = release;
            roots.Add(go);
            return go;
        }

        private static GameObject CreateRootWithViewInErrorAfterGate(
            List<GameObject> roots,
            UniTaskCompletionSource entered,
            UniTaskCompletionSource release,
            string message)
        {
            var go = new GameObject("TestRoot");
            var view = go.AddComponent<GatedThrowingTestUIView>();
            view.Entered = entered;
            view.Release = release;
            view.Message = message;
            roots.Add(go);
            return go;
        }

        private static void DestroyRoots(List<GameObject> roots)
        {
            for (var i = 0; i < roots.Count; i++)
            {
                if (roots[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(roots[i]);
                }
            }

            roots.Clear();
        }

        private sealed class ThrowingTestUIView : UIView
        {
            public string Message = "ViewIn failure";

            public override UniTask ViewIn(CancellationToken ct)
                => UniTask.FromException(new InvalidOperationException(Message));
        }

        private sealed class GatedTestUIView : UIView
        {
            public UniTaskCompletionSource? Entered;
            public UniTaskCompletionSource? Release;

            public override async UniTask ViewIn(CancellationToken ct)
            {
                Entered?.TrySetResult();
                if (Release != null)
                {
                    await Release.Task;
                }
            }
        }

        private sealed class GatedThrowingTestUIView : UIView
        {
            public UniTaskCompletionSource? Entered;
            public UniTaskCompletionSource? Release;
            public string Message = "ViewIn failure";

            public override async UniTask ViewIn(CancellationToken ct)
            {
                Entered?.TrySetResult();
                if (Release != null)
                {
                    await Release.Task;
                }

                throw new InvalidOperationException(Message);
            }
        }

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
