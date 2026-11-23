#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.DebugCommands;
using OneStarMaker.Runtime.DebugSocketServices;
using SampleGame.DependOnAll.DebugCommands;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SampleGame.Tests.DebugCommands
{
    /// <summary>
    /// DebugStudio の <c>debug.set-active</c> の失敗経路と適用を固定する。
    /// 解決（NodeId → GameObject）は delegate で差し替え、DebugSocketService を起動しない。
    /// fixture の GameObject はどれも OnEnable / OnDisable で他を変える component を持たない。
    /// </summary>
    [TestFixture]
    public sealed class SetActiveDebugCommandTests
    {
        private const long RootNodeId = 1;
        private const long ChildNodeId = 2;
        private const long InactiveParentNodeId = 3;
        private const long ChildOfInactiveParentNodeId = 4;
        private const long OtherRootNodeId = 5;
        private const long DestroyedNodeId = 9;
        private const long UnknownNodeId = 999;

        private readonly Dictionary<long, GameObject> _nodes = new();
        private readonly List<GameObject> _created = new();
        private Dictionary<long, bool> _beforeLastExecute = new();
        private int _resolveCount;
        private int _notifyCount;

        [SetUp]
        public void SetUp()
        {
            var root = Create("Root", null);
            var child = Create("Child", root);
            var inactiveParent = Create("InactiveParent", null);
            var childOfInactiveParent = Create("ChildOfInactiveParent", inactiveParent);
            inactiveParent.SetActive(false);
            var otherRoot = Create("OtherRoot", null);

            _nodes[RootNodeId] = root;
            _nodes[ChildNodeId] = child;
            _nodes[InactiveParentNodeId] = inactiveParent;
            _nodes[ChildOfInactiveParentNodeId] = childOfInactiveParent;
            _nodes[OtherRootNodeId] = otherRoot;

            // registry に対応が残ったまま破棄された GameObject（偽 null）を表す。
            var destroyed = new GameObject("Destroyed");
            Object.DestroyImmediate(destroyed);
            _nodes[DestroyedNodeId] = destroyed;

            _resolveCount = 0;
            _notifyCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _created)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }

            _created.Clear();
            _nodes.Clear();
        }

        [TestCase(UnknownNodeId)]
        [TestCase(DestroyedNodeId)]
        public void Execute_NodeNotResolvable_RejectsAsNodeNotFoundWithoutChange(long nodeId)
        {
            // 守る契約: 今の hierarchy で NodeId が GameObject を指さない（未知・prune 済み・reset 後・破棄済み）なら、
            // SetActive の前に node-not-found で拒否し、どの GameObject も変えないこと。
            // 退行時の障害: 古い NodeId の要求が handler 例外（結果不明）になる、または別の GameObject を変える。
            var before = SnapshotActiveSelf();

            var result = Execute($"{{\"nodeId\":{nodeId},\"active\":false}}");

            AssertRejected(result, SetActiveDebugCommand.RejectedNodeNotFound, before);
        }

        [Test]
        public void Execute_DebugSocketMissing_RejectsAsNodeNotFoundWithoutChange()
        {
            // 守る契約: DebugSocket が無い（解決の手段が無い）なら node-not-found で、何も変えないこと。
            // 退行時の障害: 解決をせずに SetActive してしまう、または例外で結果不明になる。
            var before = SnapshotActiveSelf();

            var result = SetActiveDebugCommand.Execute($"{{\"nodeId\":{RootNodeId},\"active\":false}}", null, Notify);

            AssertRejected(result, SetActiveDebugCommand.RejectedNodeNotFound, before);
        }

        [TestCase("not json")]
        [TestCase("")]
        [TestCase("{}")]
        [TestCase("{\"active\":false}")]
        [TestCase("{\"nodeId\":0,\"active\":false}")]
        [TestCase("{\"nodeId\":-1,\"active\":false}")]
        [TestCase("{\"nodeId\":1}")]
        [TestCase("{\"nodeId\":3}")]
        public void Execute_InvalidPayload_RejectsWithoutResolvingOrChange(string payloadJson)
        {
            // 守る契約: 解析できない payload、nodeId が無い・1 未満、active が無い（解決できる nodeId だけを含む）なら
            // 解決を呼ばずに invalid-payload で拒否すること。active の欠落を false とも true とも読まない。
            // nodeId 1 は active な root、3 は非アクティブな root で、欠落を初期値で読むとどちらかが変わる。
            // 退行時の障害: 値を書き忘れた要求で GameObject が非アクティブ（または逆）になる。
            var before = SnapshotActiveSelf();

            var result = Execute(payloadJson);

            AssertRejected(result, SetActiveDebugCommand.RejectedInvalidPayload, before);
            Assert.That(_resolveCount, Is.EqualTo(0), "invalid-payload では解決を呼ばない");
        }

        [Test]
        public void TryParse_ValidPayload_ReadsBothValuesWithoutGameObject()
        {
            // 守る契約: 解析と拒否判定は GameObject なしで決まること（解決・SetActive と分かれている）。
            // 退行時の障害: payload の解析が scene の状態に依存し、拒否の理由が入れ替わる。
            Assert.That(SetActiveDebugCommand.TryParse("{\"nodeId\":42,\"active\":true}", out var nodeId, out var active), Is.True);
            Assert.That(nodeId, Is.EqualTo(42));
            Assert.That(active, Is.True);

            Assert.That(SetActiveDebugCommand.TryParse("{\"active\":false,\"nodeId\":7}", out nodeId, out active), Is.True);
            Assert.That(nodeId, Is.EqualTo(7));
            Assert.That(active, Is.False);
        }

        [TestCase(RootNodeId)]
        [TestCase(ChildNodeId)]
        [TestCase(ChildOfInactiveParentNodeId)]
        public void Execute_ResolvableNode_AppliesFalseThenTrueAndReportsActualValues(long nodeId)
        {
            // 守る契約: 解決できれば active の値（false・true の両方）にし、結果の nodeId・name・activeSelf が実際と一致し、
            // 他の GameObject の activeSelf は変わらないこと。対象は root、子、親が非アクティブな子の 3 種。
            // 退行時の障害: CLI の結果と Editor の値・inspect が食い違う、または指定していない GameObject が変わる。
            var target = _nodes[nodeId];

            var off = Execute($"{{\"nodeId\":{nodeId},\"active\":false}}");

            AssertApplied(off, nodeId, expectedActiveSelf: false);
            Assert.That(_notifyCount, Is.EqualTo(1));

            var on = Execute($"{{\"nodeId\":{nodeId},\"active\":true}}");

            AssertApplied(on, nodeId, expectedActiveSelf: true);
            Assert.That(_notifyCount, Is.EqualTo(2));
            Assert.That(target.activeSelf, Is.True);
        }

        [Test]
        public void Execute_ChildOfInactiveParent_ReportsActiveSelfNotActiveInHierarchy()
        {
            // 守る契約: 結果の activeSelf は GameObject 自身の値で、親が非アクティブなら true でも activeInHierarchy は false のまま。
            // 退行時の障害: 結果を activeInHierarchy で返し、README の読み方（親が非アクティブなら見えない）と食い違う。
            var result = Execute($"{{\"nodeId\":{ChildOfInactiveParentNodeId},\"active\":true}}");

            AssertApplied(result, ChildOfInactiveParentNodeId, expectedActiveSelf: true);
            Assert.That(_nodes[ChildOfInactiveParentNodeId].activeInHierarchy, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Execute_SameValueTwice_StaysAtRequestedValue(bool active)
        {
            // 守る契約: 反転ではなく値の指定なので、結果不明の後に送り直しても逆に戻らないこと。
            // 退行時の障害: 送り直しで GameObject が元の値へ戻り、利用者の意図と逆になる。
            var literal = active ? "true" : "false";
            Execute($"{{\"nodeId\":{RootNodeId},\"active\":{literal}}}");

            var again = Execute($"{{\"nodeId\":{RootNodeId},\"active\":{literal}}}");

            AssertApplied(again, RootNodeId, expectedActiveSelf: active);
        }

        [Test]
        public void Execute_NameWithQuote_ResultPayloadIsReadableJson()
        {
            // 守る契約: 名前に " や \ を含む GameObject でも、結果の payload が JSON として読め、name が元の名前に戻ること。
            // 退行時の障害: 名前を手でつないだ JSON が壊れ、CLI の出力から変えた GameObject を確かめられない。
            const string name = "Quote\"Back\\slash";
            _nodes[RootNodeId].name = name;

            var result = Execute($"{{\"nodeId\":{RootNodeId},\"active\":false}}");

            Assert.That(result.Success, Is.True);
            var parsed = JsonUtility.FromJson<ResultPayloadForTest>(result.PayloadJson);
            Assert.That(parsed.nodeId, Is.EqualTo(RootNodeId));
            Assert.That(parsed.name, Is.EqualTo(name));
            Assert.That(parsed.activeSelf, Is.False);
        }

        [TestCase("\"false\"", true)]
        [TestCase("\"true\"", false)]
        [TestCase("null", true)]
        [TestCase("null", false)]
        public void Execute_ActiveIsStringOrNull_RejectsOrReportsActualValue(string activeLiteral, bool initialActive)
        {
            // 非 bool の active は拒否を保証しない（README も拒否するとは書かない）。挙動は TestContext の出力に残す。
            // どちらの挙動でも守る契約: 拒否するなら invalid-payload で activeSelf を変えず、
            // 適用するなら結果の activeSelf が実際の値と一致し、他の GameObject は変わらないこと。
            // 退行時の障害: 拒否と報告したのに値が変わる、または報告と実際が食い違う。
            var target = _nodes[RootNodeId];
            target.SetActive(initialActive);
            var before = SnapshotActiveSelf();

            var result = Execute($"{{\"nodeId\":{RootNodeId},\"active\":{activeLiteral}}}");

            TestContext.WriteLine(
                $"active={activeLiteral} initialActive={initialActive} -> success={result.Success} " +
                $"message={result.Message} payload={result.PayloadJson} activeSelf={target.activeSelf}");
            if (!result.Success)
            {
                AssertRejected(result, SetActiveDebugCommand.RejectedInvalidPayload, before);
                return;
            }

            AssertApplied(result, RootNodeId, target.activeSelf);
        }

        [Test]
        public void Register_NotEditor_DoesNotRegister()
        {
            // 守る契約: Editor でないことを渡すと catalog に debug.set-active を登録しないこと。
            // 退行時の障害: DebugSocket を有効にした Player に書き込みコマンドが現れる。
            var catalog = new DebugCommandCatalog();
            var reads = 0;

            var registered = SetActiveDebugCommand.Register(catalog, isEditor: false, () =>
            {
                reads++;
                return null;
            });

            Assert.That(registered, Is.False);
            Assert.That(catalog.Count, Is.EqualTo(0));
            Assert.That(catalog.TryExecute("debug.set-active", "{\"nodeId\":1,\"active\":false}", out _), Is.False);
            Assert.That(reads, Is.EqualTo(0));
            Assert.That(_nodes[RootNodeId].activeSelf, Is.True);
        }

        [Test]
        public void Register_InEditor_RegistersAndReadsDebugSocketAtExecution()
        {
            // 守る契約: Editor では debug.set-active を登録し、DebugSocket を登録時ではなく実行のたびに読む（無ければ node-not-found）こと。
            // 退行時の障害: 登録名が README とずれる、または登録時（service の構築前）の null を握り続けて常に拒否する。
            var catalog = new DebugCommandCatalog();
            var reads = 0;
            Func<DebugSocketService?> getDebugSocket = () =>
            {
                reads++;
                return null;
            };

            var registered = SetActiveDebugCommand.Register(catalog, isEditor: true, getDebugSocket);

            Assert.That(registered, Is.True);
            Assert.That(catalog.Count, Is.EqualTo(1));
            Assert.That(reads, Is.EqualTo(0), "登録時には DebugSocket を読まない");

            var before = SnapshotActiveSelf();
            Assert.That(catalog.TryExecute("debug.set-active", "{\"nodeId\":1,\"active\":false}", out var first), Is.True);
            Assert.That(catalog.TryExecute("debug.set-active", "{\"nodeId\":1,\"active\":false}", out _), Is.True);

            Assert.That(reads, Is.EqualTo(2), "実行のたびに DebugSocket を読む");
            AssertRejected(first, SetActiveDebugCommand.RejectedNodeNotFound, before);
        }

        private GameObject Create(string name, GameObject? parent)
        {
            var gameObject = new GameObject(name);
            if (parent != null)
            {
                gameObject.transform.SetParent(parent.transform, false);
            }

            _created.Add(gameObject);
            return gameObject;
        }

        private DebugCommandResult Execute(string payloadJson)
        {
            _beforeLastExecute = SnapshotActiveSelf();
            return SetActiveDebugCommand.Execute(payloadJson, Resolve, Notify);
        }

        // DebugSocketService.TryGetHierarchyGameObject の代わり。破棄済みの対応は偽 null の GameObject を返す。
        private bool Resolve(long nodeId, out GameObject? gameObject)
        {
            _resolveCount++;
            return _nodes.TryGetValue(nodeId, out gameObject);
        }

        private void Notify()
        {
            _notifyCount++;
        }

        private Dictionary<long, bool> SnapshotActiveSelf()
        {
            var snapshot = new Dictionary<long, bool>();
            foreach (var pair in _nodes)
            {
                if (pair.Value != null)
                {
                    snapshot[pair.Key] = pair.Value.activeSelf;
                }
            }

            return snapshot;
        }

        private void AssertRejected(DebugCommandResult result, string expectedMessage, Dictionary<long, bool> before)
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo(expectedMessage));
            Assert.That(SnapshotActiveSelf(), Is.EqualTo(before), "拒否では fixture の activeSelf を変えない");
            Assert.That(_notifyCount, Is.EqualTo(0));
        }

        private void AssertApplied(DebugCommandResult result, long nodeId, bool expectedActiveSelf)
        {
            // 直前の Execute の前に取った値と比べ、対象以外の activeSelf が変わっていないことを確かめる。
            var target = _nodes[nodeId];
            var othersBefore = new Dictionary<long, bool>(_beforeLastExecute);
            othersBefore.Remove(nodeId);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Is.EqualTo(SetActiveDebugCommand.AppliedMessage));
            Assert.That(target.activeSelf, Is.EqualTo(expectedActiveSelf));
            Assert.That(
                result.PayloadJson,
                Is.EqualTo(
                    "{\"nodeId\":" + nodeId + ",\"name\":\"" + target.name + "\",\"activeSelf\":" +
                    (expectedActiveSelf ? "true" : "false") + "}"));

            var othersAfter = SnapshotActiveSelf();
            othersAfter.Remove(nodeId);
            Assert.That(othersAfter, Is.EqualTo(othersBefore));
        }

        [Serializable]
        private sealed class ResultPayloadForTest
        {
            public long nodeId;
            public string name = string.Empty;
            public bool activeSelf;
        }
    }
}
