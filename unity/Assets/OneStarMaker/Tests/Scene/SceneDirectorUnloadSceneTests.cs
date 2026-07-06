#nullable enable

using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.SceneSystem
{
    /// <summary>正常系: アンロード テスト。</summary>
    [TestFixture]
    public class SceneDirectorUnloadSceneTests : SceneDirectorTestBase
    {
        [UnityTest]
        public IEnumerator UnloadScene_StableScene_RemovesAndDisposes() => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            await director.AddScene("TestScene", null, CancellationToken.None);

            var sceneBase = Factory.GetCreated("TestScene");
            await director.UnloadScene("TestScene");

            Assert.IsFalse(director.ContainsScene("TestScene"));
            Assert.IsTrue(sceneBase.PreUnLoadCalled);
            Assert.IsTrue(sceneBase.AfterUnLoadCalled);
        });

        [UnityTest]
        public IEnumerator UnloadScene_WithChildren_UnloadsInPostOrder() => UniTask.ToCoroutine(async () =>
        {
            var director = SetupThreeLevel();
            await director.AddScene("Root", null, CancellationToken.None);

            Assert.IsTrue(director.ContainsScene("Root"));
            Assert.IsTrue(director.ContainsScene("Mid"));
            Assert.IsTrue(director.ContainsScene("Leaf"));

            await director.UnloadScene("Root");

            Assert.IsFalse(director.ContainsScene("Root"));
            Assert.IsFalse(director.ContainsScene("Mid"));
            Assert.IsFalse(director.ContainsScene("Leaf"));

            Assert.IsTrue(Factory.GetCreated("Leaf").PreUnLoadCalled);
            Assert.IsTrue(Factory.GetCreated("Leaf").AfterUnLoadCalled);
            Assert.IsTrue(Factory.GetCreated("Mid").PreUnLoadCalled);
            Assert.IsTrue(Factory.GetCreated("Root").PreUnLoadCalled);
        });
    }
}
