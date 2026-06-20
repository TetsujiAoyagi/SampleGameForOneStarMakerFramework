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
    /// <summary>正常系: ロード テスト。</summary>
    [TestFixture]
    public class SceneDirectorAddSceneTests : SceneDirectorTestBase
    {
        //[UnityTest]
        //public IEnumerator AddScene_SingleScene_ReachesStable() => UniTask.ToCoroutine(async () =>
        //{
        //    var director = SetupSingleScene();
        //    await director.AddScene("TestScene", null, CancellationToken.None);

        //    Assert.AreEqual(SceneState.Stable, director.GetSceneState("TestScene"));
        //    Assert.IsTrue(Factory.GetCreated("TestScene").PreLoadCalled);
        //    Assert.IsTrue(Factory.GetCreated("TestScene").LoadedCalled);
        //});

        //[UnityTest]
        //public IEnumerator AddScene_WithParent_LoadsParentFirst() => UniTask.ToCoroutine(async () =>
        //{
        //    var director = SetupParentChild();
        //    await director.AddScene("Child", null, CancellationToken.None);

        //    Assert.IsTrue(director.ContainsScene("Parent"));
        //    Assert.AreEqual(SceneState.Stable, director.GetSceneState("Parent"));
        //    Assert.AreEqual(SceneState.Stable, director.GetSceneState("Child"));
        //});

        //[UnityTest]
        //public IEnumerator AddScene_WithNecessaryChild_LoadsChildAutomatically() => UniTask.ToCoroutine(async () =>
        //{
        //    var director = SetupParentChild("Parent", "Child", LoadType.NecessaryAlways);
        //    await director.AddScene("Parent", null, CancellationToken.None);

        //    Assert.IsTrue(director.ContainsScene("Child"));
        //    Assert.AreEqual(SceneState.Stable, director.GetSceneState("Child"));
        //    Assert.IsTrue(Factory.GetCreated("Child").PreLoadCalled);
        //});

        //[UnityTest]
        //public IEnumerator AddScene_WithOnDemandChild_DoesNotLoadChild() => UniTask.ToCoroutine(async () =>
        //{
        //    var director = SetupParentChild("Parent", "Child", LoadType.OnDemand);
        //    await director.AddScene("Parent", null, CancellationToken.None);

        //    Assert.IsFalse(director.ContainsScene("Child"));
        //});

        //[UnityTest]
        //public IEnumerator AddScene_AfterOnLoadedTask_IsExecuted() => UniTask.ToCoroutine(async () =>
        //{
        //    var director = SetupSingleScene();
        //    var afterCalled = false;

        //    await director.AddScene("TestScene", async () =>
        //    {
        //        afterCalled = true;
        //        await UniTask.CompletedTask;
        //    }, CancellationToken.None);

        //    Assert.IsTrue(afterCalled);
        //    Assert.AreEqual(SceneState.Stable, director.GetSceneState("TestScene"));
        //});
    }
}
