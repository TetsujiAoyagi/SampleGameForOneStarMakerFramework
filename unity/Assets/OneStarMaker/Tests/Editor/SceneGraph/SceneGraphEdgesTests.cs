#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.SceneGraph;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.SceneGraph
{
    /// <summary>
    /// SceneGraphEdges の DFS ベース判定（サイクル検出 / ルート抽出）とエッジ操作を検証する。
    /// ScriptableObject.CreateInstance のみに依存し、AssetDatabase へは触れない。
    /// </summary>
    [TestFixture]
    public sealed class SceneGraphEdgesTests
    {
        private readonly List<Object> _created = new();

        private T CreateInstance<T>() where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            _created.Add(obj);
            return obj;
        }

        private SceneNodeData CreateNode(string identity)
        {
            var node = CreateInstance<SceneNodeData>();
            node.Identity = identity;
            node.name = identity;
            return node;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        [Test]
        public void WouldCreateCycle_DetectsSelfLoop()
        {
            var edges = CreateInstance<SceneGraphEdges>();
            var a = CreateNode("A");

            Assert.IsTrue(edges.WouldCreateCycle(a, a));
        }

        [Test]
        public void WouldCreateCycle_DetectsMultiStepCycle()
        {
            var edges = CreateInstance<SceneGraphEdges>();
            var a = CreateNode("A");
            var b = CreateNode("B");
            var c = CreateNode("C");
            var d = CreateNode("D");

            edges.AddEdge(a, b);
            edges.AddEdge(b, c);

            // A -> B -> C が既に存在する状態で C -> A を繋ぐとサイクルになる
            Assert.IsTrue(edges.WouldCreateCycle(c, a));

            // 無関係な新規エッジはサイクル扱いされない
            Assert.IsFalse(edges.WouldCreateCycle(a, d));
        }

        [Test]
        public void RemoveEdgeByChild_RemovesOnlyThatEdge()
        {
            var edges = CreateInstance<SceneGraphEdges>();
            var a = CreateNode("A");
            var b = CreateNode("B");
            var c = CreateNode("C");

            edges.AddEdge(a, b);
            edges.AddEdge(a, c);

            var removed = edges.RemoveEdgeByChild(b);

            Assert.IsTrue(removed);
            Assert.AreEqual(1, edges.Edges.Count);
            Assert.AreEqual(c, edges.Edges[0].Child);
        }

        [Test]
        public void RemoveNode_AlsoRemovesEdgesInvolvingThatNode()
        {
            var edges = CreateInstance<SceneGraphEdges>();
            var a = CreateNode("A");
            var b = CreateNode("B");
            var c = CreateNode("C");

            edges.AddNode(a);
            edges.AddNode(b);
            edges.AddNode(c);
            edges.AddEdge(a, b);
            edges.AddEdge(b, c);

            edges.RemoveNode(b);

            Assert.IsFalse(edges.ContainsNode(b));
            Assert.AreEqual(0, edges.Edges.Count);
        }

        [Test]
        public void GetRoots_ReturnsOnlyNodesWithoutParent()
        {
            var edges = CreateInstance<SceneGraphEdges>();
            var a = CreateNode("A");
            var b = CreateNode("B");
            var c = CreateNode("C");

            edges.AddNode(a);
            edges.AddNode(b);
            edges.AddNode(c);
            edges.AddEdge(a, b);

            var roots = edges.GetRoots();

            Assert.IsTrue(roots.Contains(a));
            Assert.IsTrue(roots.Contains(c));
            Assert.IsFalse(roots.Contains(b));
        }
    }
}
