#nullable enable

using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine.TestTools;
using Cysharp.Threading.Tasks.Linq;

namespace OneStarMaker.Tests.SceneSystem
{
    /// <summary>ガード条件テスト。</summary>
    [TestFixture]
    public class SceneDirectorGuardTests : SceneDirectorTestBase
    {
    //    [UnityTest]
    //    public IEnumerator AddScene_DuplicateCall_Skips() => UniTask.Create(async () =>
    //    {
    //        var director = SetupSingleScene();
    //        await director.AddScene("TestScene", null, CancellationToken.None);

    //        // 2回目の AddScene → 例外なしでスキップ
    //        await director.AddScene("TestScene", null, CancellationToken.None);

    //        Assert.AreEqual(SceneState.Stable, director.GetSceneState("TestScene"));
    //    }).ToCoroutine();

    //    [UnityTest]
    //    public IEnumerator UnloadScene_NonexistentScene_NoOp() => UniTask.Create(async () =>
    //    {
    //        SetupSingleScene();

    //        // 存在しないシーンをアンロード → 例外なし
    //        await Director.UnloadScene("NonExistent");
    //        Assert.Pass();
    //    }).ToCoroutine();

    //    [UnityTest]
    //    public IEnumerator UnloadScene_AlreadyUnloading_Skips() => UniTask.Create(async () =>
    //    {
    //        var director = SetupSingleScene();
    //        await director.AddScene("TestScene", null, CancellationToken.None);

    //        var task1 = director.UnloadScene("TestScene");
    //        var task2 = director.UnloadScene("TestScene");

    //        await UniTask.WhenAll(task1, task2);

    //        Assert.IsFalse(director.ContainsScene("TestScene"));
    //    }).ToCoroutine();
    }
}
