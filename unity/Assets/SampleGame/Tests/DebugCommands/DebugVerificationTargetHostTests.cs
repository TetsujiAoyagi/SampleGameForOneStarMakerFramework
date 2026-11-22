#nullable enable

using NUnit.Framework;
using SampleGame.DependOnAll.DebugCommands;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SampleGame.Tests.DebugCommands
{
    /// <summary>
    /// DebugStudio の検証用 GameObject の生成・選択数・解放の契約を固定する。
    /// 実際の置き場所（UICommon の scene）は Editor の Play で確かめるので、ここでは渡された scene に置くことだけを見る。
    /// </summary>
    [TestFixture]
    public sealed class DebugVerificationTargetHostTests
    {
        private DebugVerificationTargetHost _host = null!;

        [SetUp]
        public void SetUp()
        {
            _host = new DebugVerificationTargetHost();
        }

        [TearDown]
        public void TearDown()
        {
            _host.Release();
        }

        [Test]
        public void TryCreate_InEditor_CreatesOneTransformOnlyRootInGivenScene()
        {
            // 守る契約: 渡された scene の root に、検証用の名前で Transform だけの GameObject を 1 つ作ること。
            // 退行時の障害: hierarchy に出ない場所へ置かれる、または余計な component が検証以外の挙動を持ち込む。
            var scene = SceneManager.GetActiveScene();
            var before = CountTargetsInScene(scene);

            Assert.That(_host.TryCreate(isEditor: true, scene), Is.True);

            var target = _host.Target;
            Assert.That(target != null, Is.True);
            Assert.That(target!.name, Is.EqualTo("[DebugStudioVerificationTarget]"));
            Assert.That(target.scene, Is.EqualTo(scene));
            Assert.That(target.transform.parent == null, Is.True, "scene の root に置くこと");
            var components = target.GetComponents<Component>();
            Assert.That(components.Length, Is.EqualTo(1));
            Assert.That(components[0], Is.InstanceOf<Transform>());
            Assert.That(CountTargetsInScene(scene), Is.EqualTo(before + 1));
        }

        [Test]
        public void TryCreate_WhileTargetAlive_DoesNotCreateSecond()
        {
            // 守る契約: 生きた GameObject を持っている間は 2 つ目を作らないこと（選択数 1）。
            // 退行時の障害: hierarchy に検証用 GameObject が 2 つ出て、利用者がどちらの NodeId を送るか決められない。
            var scene = SceneManager.GetActiveScene();
            var before = CountTargetsInScene(scene);
            Assert.That(_host.TryCreate(isEditor: true, scene), Is.True);
            var first = _host.Target;

            Assert.That(_host.TryCreate(isEditor: true, scene), Is.False);

            Assert.That(_host.Target, Is.SameAs(first));
            Assert.That(CountTargetsInScene(scene), Is.EqualTo(before + 1));
        }

        [Test]
        public void Release_DestroysTarget_AndNextCreateMakesNewOne()
        {
            // 守る契約: 解放で GameObject を破棄して所有を手放し、次の生成で新しく作ること。
            // 退行時の障害: 前の Play の GameObject が残る、または次の Play で作られない。
            var scene = SceneManager.GetActiveScene();
            var before = CountTargetsInScene(scene);
            Assert.That(_host.TryCreate(isEditor: true, scene), Is.True);
            var first = _host.Target;

            _host.Release();

            Assert.That(first == null, Is.True, "解放で破棄すること");
            Assert.That(_host.Target, Is.Null);
            Assert.That(CountTargetsInScene(scene), Is.EqualTo(before));

            Assert.That(_host.TryCreate(isEditor: true, scene), Is.True);
            Assert.That(_host.Target != null && _host.Target != first, Is.True);
        }

        [Test]
        public void Release_AfterUnityDestroyedTarget_DoesNotThrow()
        {
            // 守る契約: Unity が先に破棄した（偽 null の）GameObject でも解放は失敗せず、2 回呼んでもよいこと。
            // 退行時の障害: Play の終わりの後の SubsystemRegistration で例外になり、次の Play の初期化が止まる。
            var scene = SceneManager.GetActiveScene();
            Assert.That(_host.TryCreate(isEditor: true, scene), Is.True);
            Object.DestroyImmediate(_host.Target);

            Assert.That(_host.Target, Is.Null, "破棄済みを生きた GameObject として返さないこと");
            Assert.DoesNotThrow(() => _host.Release());
            Assert.DoesNotThrow(() => _host.Release());

            Assert.That(_host.TryCreate(isEditor: true, scene), Is.True, "破棄済みなら作り直すこと");
        }

        [Test]
        public void TryCreate_NotEditor_CreatesNothing()
        {
            // 守る契約: Editor でないことを渡すと検証用 GameObject を作らないこと。
            // 退行時の障害: Player の hierarchy に検証用 GameObject が現れ、Player の挙動を変える。
            var scene = SceneManager.GetActiveScene();
            var before = CountTargetsInScene(scene);

            Assert.That(_host.TryCreate(isEditor: false, scene), Is.False);

            Assert.That(_host.Target, Is.Null);
            Assert.That(CountTargetsInScene(scene), Is.EqualTo(before));
        }

        [Test]
        public void TryCreate_InvalidScene_CreatesNothing()
        {
            // 守る契約: 置き場所が無効なら作らず、active scene に取り残さないこと。
            // 退行時の障害: ゲームの scene に検証用 GameObject が残り、SceneDirector の入れ替えで消える。
            var active = SceneManager.GetActiveScene();
            var before = CountTargetsInScene(active);

            Assert.That(_host.TryCreate(isEditor: true, default), Is.False);

            Assert.That(_host.Target, Is.Null);
            Assert.That(CountTargetsInScene(active), Is.EqualTo(before));
        }

        private static int CountTargetsInScene(Scene scene)
        {
            var count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == DebugVerificationTargetHost.TargetName)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
