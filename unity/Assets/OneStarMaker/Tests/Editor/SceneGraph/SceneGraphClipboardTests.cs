#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Editor.SceneGraph;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.SceneGraph
{
    /// <summary>
    /// SceneGraphClipboard の純粋関数（AssetDatabase / UnityEditor に依存しない）を検証する。
    /// </summary>
    [TestFixture]
    public sealed class SceneGraphClipboardTests
    {
        [Test]
        public void SerializeThenDeserialize_PreservesNodesEdgesAndPosition()
        {
            var data = new SceneGraphClipboardData
            {
                SourceGraphGuid = "graph-guid",
            };
            data.Nodes.Add(new SceneGraphClipboardEntry
            {
                NodeGuid = "guid-a",
                Identity = "A",
                LoadType = 1,
                Position = new Vector2(10, 20),
            });
            data.Nodes.Add(new SceneGraphClipboardEntry
            {
                NodeGuid = "guid-b",
                Identity = "B",
                LoadType = 0,
                Position = new Vector2(30, 40),
            });
            data.Edges.Add(new SceneGraphClipboardLink { ParentIndex = 0, ChildIndex = 1 });

            var json = SceneGraphClipboard.Serialize(data);
            var restored = SceneGraphClipboard.TryDeserialize(json);

            Assert.IsNotNull(restored);
            Assert.AreEqual("graph-guid", restored!.SourceGraphGuid);
            Assert.AreEqual(2, restored.Nodes.Count);
            Assert.AreEqual("guid-a", restored.Nodes[0].NodeGuid);
            Assert.AreEqual("A", restored.Nodes[0].Identity);
            Assert.AreEqual(1, restored.Nodes[0].LoadType);
            Assert.AreEqual(new Vector2(10, 20), restored.Nodes[0].Position);
            Assert.AreEqual("guid-b", restored.Nodes[1].NodeGuid);
            Assert.AreEqual(new Vector2(30, 40), restored.Nodes[1].Position);

            Assert.AreEqual(1, restored.Edges.Count);
            Assert.AreEqual(0, restored.Edges[0].ParentIndex);
            Assert.AreEqual(1, restored.Edges[0].ChildIndex);

            Assert.IsTrue(SceneGraphClipboard.CanPaste(json));
        }

        [Test]
        public void CanPaste_ReturnsFalse_ForOtherToolsJsonWithMismatchedType()
        {
            // 他ツールの GraphView クリップボードを模した JSON（Type が一致しない）。
            const string foreignJson = "{\"Type\":\"SomeOtherTool.Clipboard\",\"Version\":1,\"Nodes\":[{}]}";

            Assert.IsFalse(SceneGraphClipboard.CanPaste(foreignJson));
        }

        [Test]
        public void TryDeserializeAndCanPaste_DoNotThrow_ForMalformedOrEmptyInput()
        {
            SceneGraphClipboardData? a = null;
            SceneGraphClipboardData? b = null;
            SceneGraphClipboardData? c = null;

            Assert.DoesNotThrow(() =>
            {
                a = SceneGraphClipboard.TryDeserialize(string.Empty);
                b = SceneGraphClipboard.TryDeserialize("not json at all {{{");
                c = SceneGraphClipboard.TryDeserialize(null);
            });

            Assert.IsNull(a);
            Assert.IsNull(b);
            Assert.IsNull(c);

            Assert.IsFalse(SceneGraphClipboard.CanPaste(string.Empty));
            Assert.IsFalse(SceneGraphClipboard.CanPaste("not json at all {{{"));
            Assert.IsFalse(SceneGraphClipboard.CanPaste((string?)null));
        }

        [Test]
        public void CanPaste_ReturnsFalse_ForJsonWithoutTypeKey()
        {
            // R4: JsonUtility.FromJson は JSON に無いキーをフィールド初期化子の値のままにする。
            // "Type" キー自体を持たない JSON（他ツールがマジックフィールドを持たない場合）でも
            // 弾けることを確認する。Type の初期化子は string.Empty なので TypeTag と一致しない。
            const string jsonWithoutTypeKey = "{\"Version\":1,\"Nodes\":[{\"NodeGuid\":\"guid-a\"}]}";

            Assert.IsFalse(SceneGraphClipboard.CanPaste(jsonWithoutTypeKey));
        }

        [Test]
        public void BuildInternalLinks_ExcludesEdgesWithOnlyOneEndpointInCopySet()
        {
            // コピー集合には A(guid-a) と B(guid-b) のみが含まれる。
            var nodeGuids = new List<string> { "guid-a", "guid-b" };

            var allEdges = new List<(string ParentGuid, string ChildGuid)>
            {
                ("guid-a", "guid-b"), // 両端がコピー集合内 → 含まれる
                ("guid-a", "guid-c"), // 子がコピー集合外 → 除外される
                ("guid-x", "guid-b"), // 親がコピー集合外 → 除外される
            };

            var links = SceneGraphClipboard.BuildInternalLinks(nodeGuids, allEdges);

            Assert.AreEqual(1, links.Count);
            Assert.AreEqual(0, links[0].ParentIndex);
            Assert.AreEqual(1, links[0].ChildIndex);
        }

        [Test]
        public void GetIndicesWithoutInternalParent_ReturnsRootsParentsAndEmpty()
        {
            // 親なし: 全 index
            var noParents = SceneGraphClipboard.GetIndicesWithoutInternalParent(
                2, new List<SceneGraphClipboardLink>());
            CollectionAssert.AreEqual(new[] { 0, 1 }, noParents);

            // 親あり: ChildIndex=1 のみ親を持つ → 0 だけ
            var withParent = SceneGraphClipboard.GetIndicesWithoutInternalParent(
                2,
                new List<SceneGraphClipboardLink>
                {
                    new SceneGraphClipboardLink { ParentIndex = 0, ChildIndex = 1 },
                });
            CollectionAssert.AreEqual(new[] { 0 }, withParent);

            // 空リスト: nodeCount=0
            var empty = SceneGraphClipboard.GetIndicesWithoutInternalParent(
                0, new List<SceneGraphClipboardLink>());
            CollectionAssert.AreEqual(new int[0], empty);
        }
    }
}
