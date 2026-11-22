#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime.DebugCommands;
using SampleGame.DependOnAll.DebugCommands;
using UnityEngine;

namespace SampleGame.Tests.DebugCommands
{
    /// <summary>
    /// DebugStudio の <c>debug.set-active</c> の失敗経路と適用を固定する。
    /// 解決（GameObject → 現在の NodeId）は delegate で差し替え、DebugSocketService を起動しない。
    /// </summary>
    [TestFixture]
    public sealed class SetActiveDebugCommandTests
    {
        private const long TargetNodeId = 7;
        private const long OtherNodeId = 3;

        private GameObject _target = null!;
        private GameObject _other = null!;
        private int _notifyCount;

        [SetUp]
        public void SetUp()
        {
            _target = new GameObject(DebugVerificationTargetHost.TargetName);
            // 同じ名前を付けた検証用でない GameObject。名前で照合していれば書き換わってしまう。
            _other = new GameObject(DebugVerificationTargetHost.TargetName);
            _notifyCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
            }

            if (_other != null)
            {
                Object.DestroyImmediate(_other);
            }
        }

        [TestCase(OtherNodeId)]
        [TestCase(999L)]
        public void Execute_NodeIdIsNotVerificationTarget_RejectsWithoutChange(long requestedNodeId)
        {
            // 守る契約: payload の nodeId が検証用 GameObject の現在の NodeId と違えば、SetActive の前に拒否すること。
            // その nodeId に対応する別の GameObject（同じ名前を付けたものを含む）も変えない。
            // 退行時の障害: 前の Play や古い hierarchy の NodeId で、ゲームや Framework の GameObject が非アクティブになる。
            var result = Execute($"{{\"nodeId\":{requestedNodeId},\"active\":false}}");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("rejected: not-verification-target"));
            Assert.That(_target.activeSelf, Is.True);
            Assert.That(_other.activeSelf, Is.True);
            Assert.That(_notifyCount, Is.EqualTo(0));
        }

        [Test]
        public void Execute_ResolverReturnsFalse_RejectsAsTargetUnavailable()
        {
            // 守る契約: 検証用 GameObject が未 capture・prune 済み・reset 後で NodeId が引けなければ target-unavailable。
            // 退行時の障害: 利用者が見ていない番号で照合が通る、または handler 例外（結果不明）になる。
            var result = SetActiveDebugCommand.Execute(
                $"{{\"nodeId\":{TargetNodeId},\"active\":false}}",
                _target,
                (GameObject _, out long nodeId) =>
                {
                    nodeId = 0;
                    return false;
                },
                Notify);

            AssertRejected(result, "rejected: target-unavailable", expectedActive: true);
        }

        [Test]
        public void Execute_TargetDestroyed_RejectsAsTargetUnavailable()
        {
            // 守る契約: 破棄済み（偽 null）の検証用 GameObject は target-unavailable で、例外にならないこと。
            // 退行時の障害: Play の終わりの後に届いた要求が handler 例外（結果不明）になる。
            var destroyed = _target;
            Object.DestroyImmediate(destroyed);

            var result = SetActiveDebugCommand.Execute(
                $"{{\"nodeId\":{TargetNodeId},\"active\":false}}",
                destroyed,
                Resolve,
                Notify);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("rejected: target-unavailable"));
            Assert.That(_other.activeSelf, Is.True);
            Assert.That(_notifyCount, Is.EqualTo(0));
        }

        [Test]
        public void Execute_TargetMissing_RejectsAsTargetUnavailable()
        {
            // 守る契約: 検証用 GameObject が無い（UICommon が無く作らなかった等）なら target-unavailable。
            // 退行時の障害: 対象が無いのに NullReferenceException で結果不明になる。
            var result = SetActiveDebugCommand.Execute(
                $"{{\"nodeId\":{TargetNodeId},\"active\":false}}",
                null,
                Resolve,
                Notify);

            AssertRejected(result, "rejected: target-unavailable", expectedActive: true);
        }

        [Test]
        public void Execute_DebugSocketMissing_RejectsAsTargetUnavailable()
        {
            // 守る契約: DebugSocket が無い（解決の手段が無い）なら target-unavailable。
            // 退行時の障害: 照合をせずに SetActive してしまう、または例外で結果不明になる。
            var result = SetActiveDebugCommand.Execute(
                $"{{\"nodeId\":{TargetNodeId},\"active\":false}}",
                _target,
                null,
                Notify);

            AssertRejected(result, "rejected: target-unavailable", expectedActive: true);
        }

        [TestCase("not json", true)]
        [TestCase("", true)]
        [TestCase("{}", true)]
        [TestCase("{\"active\":false}", true)]
        [TestCase("{\"nodeId\":0,\"active\":false}", true)]
        [TestCase("{\"nodeId\":-7,\"active\":false}", true)]
        [TestCase("{\"nodeId\":7}", true)]
        [TestCase("{\"nodeId\":7}", false)]
        public void Execute_InvalidPayload_RejectsWithoutChange(string payloadJson, bool initialActive)
        {
            // 守る契約: 解析できない payload、nodeId が無い・1 未満、active が無い（一致する nodeId だけを含む）なら
            // SetActive の前に invalid-payload で拒否すること。active の欠落を false とも true とも読まない。
            // 退行時の障害: 値を書き忘れた要求で検証用 GameObject が非アクティブ（または逆）になる。
            _target.SetActive(initialActive);

            var result = Execute(payloadJson);

            AssertRejected(result, "rejected: invalid-payload", initialActive);
        }

        [Test]
        public void Execute_MatchingNodeId_AppliesFalseThenTrue()
        {
            // 守る契約: 一致すれば active の値（false・true の両方）にし、結果の payload の nodeId・activeSelf が実際と一致すること。
            // 退行時の障害: CLI の結果と Editor の値・inspect が食い違い、利用者が適用を確かめられない。
            var off = Execute($"{{\"nodeId\":{TargetNodeId},\"active\":false}}");

            Assert.That(off.Success, Is.True);
            Assert.That(off.Message, Is.EqualTo("applied"));
            Assert.That(_target.activeSelf, Is.False);
            Assert.That(off.PayloadJson, Is.EqualTo("{\"nodeId\":7,\"activeSelf\":false}"));
            Assert.That(_other.activeSelf, Is.True);
            Assert.That(_notifyCount, Is.EqualTo(1));

            var on = Execute($"{{\"nodeId\":{TargetNodeId},\"active\":true}}");

            Assert.That(on.Success, Is.True);
            Assert.That(on.Message, Is.EqualTo("applied"));
            Assert.That(_target.activeSelf, Is.True);
            Assert.That(on.PayloadJson, Is.EqualTo("{\"nodeId\":7,\"activeSelf\":true}"));
            Assert.That(_notifyCount, Is.EqualTo(2));
        }

        [Test]
        public void Execute_SameValueTwice_StaysAtRequestedValue()
        {
            // 守る契約: 反転ではなく値の指定なので、結果不明の後に送り直しても逆に戻らないこと。
            // 退行時の障害: 送り直しで検証用 GameObject が元の値へ戻り、利用者の意図と逆になる。
            Execute($"{{\"nodeId\":{TargetNodeId},\"active\":false}}");
            var again = Execute($"{{\"nodeId\":{TargetNodeId},\"active\":false}}");

            Assert.That(again.Success, Is.True);
            Assert.That(_target.activeSelf, Is.False);
            Assert.That(again.PayloadJson, Is.EqualTo("{\"nodeId\":7,\"activeSelf\":false}"));
        }

        [TestCase("\"false\"", true)]
        [TestCase("\"true\"", false)]
        [TestCase("null", true)]
        [TestCase("null", false)]
        public void Execute_ActiveIsStringOrNull_RejectsOrReportsActualValue(string activeLiteral, bool initialActive)
        {
            // 外部計画レビューの確認事項（非ブロッキング）: active に文字列や null を渡したときの JsonUtility の挙動を観測する。
            // 結果は TestContext の出力に残す。どちらの挙動でも守る契約:
            // 拒否するなら invalid-payload で activeSelf を変えず、適用するなら結果の activeSelf が実際の値と一致すること。
            // 退行時の障害: 拒否と報告したのに値が変わる、または報告と実際が食い違う。
            _target.SetActive(initialActive);

            var result = Execute($"{{\"nodeId\":{TargetNodeId},\"active\":{activeLiteral}}}");

            TestContext.WriteLine(
                $"active={activeLiteral} initialActive={initialActive} -> success={result.Success} " +
                $"message={result.Message} payload={result.PayloadJson} activeSelf={_target.activeSelf}");
            if (!result.Success)
            {
                AssertRejected(result, "rejected: invalid-payload", initialActive);
                return;
            }

            Assert.That(result.Message, Is.EqualTo("applied"));
            Assert.That(
                result.PayloadJson,
                Is.EqualTo("{\"nodeId\":7,\"activeSelf\":" + (_target.activeSelf ? "true" : "false") + "}"));
            Assert.That(_other.activeSelf, Is.True);
        }

        [Test]
        public void Register_NotEditor_DoesNotRegister()
        {
            // 守る契約: Editor でないことを渡すと catalog に debug.set-active を登録しないこと。
            // 退行時の障害: DebugSocket を有効にした Player に書き込みコマンドが現れる。
            var catalog = new DebugCommandCatalog();

            var registered = SetActiveDebugCommand.Register(catalog, isEditor: false, () => null, () => _target);

            Assert.That(registered, Is.False);
            Assert.That(catalog.Count, Is.EqualTo(0));
            Assert.That(catalog.TryExecute("debug.set-active", "{\"nodeId\":7,\"active\":false}", out _), Is.False);
            Assert.That(_target.activeSelf, Is.True);
        }

        [Test]
        public void Register_InEditor_RegistersAndReadsDebugSocketAtExecution()
        {
            // 守る契約: Editor では debug.set-active を登録し、DebugSocket を実行時に読む（無ければ target-unavailable）こと。
            // 退行時の障害: 登録名が README とずれる、または登録時の null を握り続けて常に拒否する。
            var catalog = new DebugCommandCatalog();

            var registered = SetActiveDebugCommand.Register(catalog, isEditor: true, () => null, () => _target);

            Assert.That(registered, Is.True);
            Assert.That(catalog.Count, Is.EqualTo(1));
            Assert.That(
                catalog.TryExecute("debug.set-active", "{\"nodeId\":7,\"active\":false}", out var result),
                Is.True);
            AssertRejected(result, "rejected: target-unavailable", expectedActive: true);
        }

        private DebugCommandResult Execute(string payloadJson)
        {
            return SetActiveDebugCommand.Execute(payloadJson, _target, Resolve, Notify);
        }

        // DebugSocketService.TryGetHierarchyNodeId の代わり。検証用と同名の別 GameObject にも別の NodeId を割り当てておく。
        private bool Resolve(GameObject gameObject, out long nodeId)
        {
            if (gameObject == _target)
            {
                nodeId = TargetNodeId;
                return true;
            }

            if (gameObject == _other)
            {
                nodeId = OtherNodeId;
                return true;
            }

            nodeId = 0;
            return false;
        }

        private void Notify()
        {
            _notifyCount++;
        }

        private void AssertRejected(DebugCommandResult result, string expectedMessage, bool expectedActive)
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo(expectedMessage));
            Assert.That(_target.activeSelf, Is.EqualTo(expectedActive));
            Assert.That(_other.activeSelf, Is.True);
            Assert.That(_notifyCount, Is.EqualTo(0));
        }
    }
}
