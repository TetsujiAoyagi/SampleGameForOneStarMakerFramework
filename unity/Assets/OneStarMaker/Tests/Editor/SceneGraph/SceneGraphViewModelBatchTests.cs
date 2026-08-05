#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Runtime.AssetDescriptions;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.SceneGraph
{
    /// <summary>
    /// SceneGraphViewModel のバッチ Undo / 一括コマンドを検証する。
    /// 既存の Assets/SceneGraphData/ には絶対に書き込まない。専用の一時フォルダを使う。
    /// </summary>
    [TestFixture]
    public sealed class SceneGraphViewModelBatchTests
    {
        private const string RootFolder = "Assets/__SceneGraphEditorTests__";
        private const string NodesFolder = RootFolder + "/Nodes";
        private const string GraphsFolder = RootFolder + "/Graphs";
        private const string LayoutsFolder = RootFolder + "/Layouts";

        private string _originalNodesFolder = string.Empty;
        private string _originalGraphsFolder = string.Empty;
        private string _originalLayoutsFolder = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _originalNodesFolder = SceneGraphViewModel.NodesFolder;
            _originalGraphsFolder = SceneGraphViewModel.GraphsFolder;
            _originalLayoutsFolder = SceneGraphViewModel.LayoutsFolder;

            SceneGraphViewModel.NodesFolder = NodesFolder;
            SceneGraphViewModel.GraphsFolder = GraphsFolder;
            SceneGraphViewModel.LayoutsFolder = LayoutsFolder;

            if (!AssetDatabase.IsValidFolder(RootFolder))
            {
                AssetDatabase.CreateFolder("Assets", "__SceneGraphEditorTests__");
            }
            AssetDatabase.CreateFolder(RootFolder, "Nodes");
            AssetDatabase.CreateFolder(RootFolder, "Graphs");
            AssetDatabase.CreateFolder(RootFolder, "Layouts");
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();

            if (AssetDatabase.IsValidFolder(RootFolder))
            {
                AssetDatabase.DeleteAsset(RootFolder);
            }

            SceneGraphViewModel.NodesFolder = _originalNodesFolder;
            SceneGraphViewModel.GraphsFolder = _originalGraphsFolder;
            SceneGraphViewModel.LayoutsFolder = _originalLayoutsFolder;

            AssetDatabase.Refresh();
        }

        private static SceneNodeData CreateNodeAsset(string identity)
        {
            var node = ScriptableObject.CreateInstance<SceneNodeData>();
            node.Identity = identity;
            node.name = identity;
            AssetDatabase.CreateAsset(node, $"{NodesFolder}/{identity}.asset");
            return node;
        }

        private static (SceneGraphEdges Edges, SceneGraphLayout Layout) CreateGraph(string graphName)
        {
            var edges = ScriptableObject.CreateInstance<SceneGraphEdges>();
            edges.GraphName = graphName;
            AssetDatabase.CreateAsset(edges, $"{GraphsFolder}/{graphName}.asset");

            var layout = ScriptableObject.CreateInstance<SceneGraphLayout>();
            AssetDatabase.CreateAsset(layout, $"{LayoutsFolder}/{graphName}_Layout.asset");

            AssetDatabase.SaveAssets();
            return (edges, layout);
        }

        [Test]
        public void RemoveNodesFromGraph_SingleUndo_RestoresMembershipEdgesAndLayout()
        {
            var (edges, layout) = CreateGraph("G1");
            var a = CreateNodeAsset("A");
            var b = CreateNodeAsset("B");

            edges.AddNode(a);
            edges.AddNode(b);
            edges.AddEdge(a, b);
            layout.SetPosition(a, new Vector2(1, 2));
            layout.SetPosition(b, new Vector2(3, 4));
            EditorUtility.SetDirty(edges);
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();

            var viewModel = new SceneGraphViewModel();
            viewModel.LoadGraph(edges);

            viewModel.RemoveNodesFromGraph(new List<SceneNodeData> { a, b });

            Assert.IsFalse(edges.ContainsNode(a));
            Assert.IsFalse(edges.ContainsNode(b));
            Assert.AreEqual(0, edges.Edges.Count);

            Undo.PerformUndo();

            Assert.IsTrue(edges.ContainsNode(a));
            Assert.IsTrue(edges.ContainsNode(b));
            Assert.AreEqual(1, edges.Edges.Count);
            Assert.AreEqual(new Vector2(1, 2), layout.GetPosition(a));
            Assert.AreEqual(new Vector2(3, 4), layout.GetPosition(b));
        }

        [Test]
        public void ReferencePasteEquivalent_SingleUndo_RestoresMembershipAndEdges()
        {
            var (sourceEdges, _) = CreateGraph("Source");
            var a = CreateNodeAsset("A2");
            var b = CreateNodeAsset("B2");
            sourceEdges.AddNode(a);
            sourceEdges.AddNode(b);
            sourceEdges.AddEdge(a, b);
            EditorUtility.SetDirty(sourceEdges);
            AssetDatabase.SaveAssets();

            var (targetEdges, _) = CreateGraph("Target");

            var viewModel = new SceneGraphViewModel();
            viewModel.LoadGraph(targetEdges);

            Assert.IsFalse(targetEdges.ContainsNode(a));
            Assert.IsFalse(targetEdges.ContainsNode(b));

            // View 層の「参照ペースト」相当: AddExistingNodesToGraph + ConnectEdges を
            // 1 つの外側 BeginBatch で束ねる（SceneGraphView.ApplyPaste と同じネスト構造）。
            using (viewModel.BeginBatch("Paste 2 node(s)"))
            {
                viewModel.AddExistingNodesToGraph(new List<(SceneNodeData Node, Vector2 Position)>
                {
                    (a, new Vector2(10, 10)),
                    (b, new Vector2(20, 20)),
                });
                viewModel.ConnectEdges(a, new List<SceneNodeData> { b });
            }

            Assert.IsTrue(targetEdges.ContainsNode(a));
            Assert.IsTrue(targetEdges.ContainsNode(b));
            Assert.AreEqual(1, targetEdges.Edges.Count(e => e.Parent == a && e.Child == b));

            Undo.PerformUndo();

            Assert.IsFalse(targetEdges.ContainsNode(a));
            Assert.IsFalse(targetEdges.ContainsNode(b));
            Assert.AreEqual(0, targetEdges.Edges.Count);

            // ソースグラフは無傷であること
            Assert.IsTrue(sourceEdges.ContainsNode(a));
            Assert.IsTrue(sourceEdges.ContainsNode(b));
        }

        [Test]
        public void DuplicateNode_HasEmptyPayloadsUniqueIdentityAndMatchingFileName()
        {
            var (edges, _) = CreateGraph("G3");
            var source = CreateNodeAsset("Source");
            source.Payloads.Add(new AssetPayload());
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();

            edges.AddNode(source);

            var viewModel = new SceneGraphViewModel();
            viewModel.LoadGraph(edges);

            var duplicate = viewModel.DuplicateNode(source, new Vector2(5, 5));

            Assert.IsNotNull(duplicate);
            Assert.AreEqual(0, duplicate!.Payloads.Count);
            Assert.AreNotEqual(source.Identity, duplicate.Identity);

            var path = AssetDatabase.GetAssetPath(duplicate);
            var fileName = Path.GetFileNameWithoutExtension(path);
            Assert.AreEqual(duplicate.Identity, fileName);
        }

        [Test]
        public void ConnectEdges_SingleUndo_RestoresAllEdges()
        {
            var (edges, _) = CreateGraph("G4");
            var parent = CreateNodeAsset("Parent");
            var a = CreateNodeAsset("ChildA");
            var b = CreateNodeAsset("ChildB");
            var c = CreateNodeAsset("ChildC");

            edges.AddNode(parent);
            edges.AddNode(a);
            edges.AddNode(b);
            edges.AddNode(c);
            EditorUtility.SetDirty(edges);
            AssetDatabase.SaveAssets();

            var viewModel = new SceneGraphViewModel();
            viewModel.LoadGraph(edges);

            Assert.AreEqual(0, edges.Edges.Count);

            viewModel.ConnectEdges(parent, new List<SceneNodeData> { a, b, c });

            Assert.AreEqual(3, edges.Edges.Count);
            Assert.AreEqual(1, edges.Edges.Count(e => e.Parent == parent && e.Child == a));
            Assert.AreEqual(1, edges.Edges.Count(e => e.Parent == parent && e.Child == b));
            Assert.AreEqual(1, edges.Edges.Count(e => e.Parent == parent && e.Child == c));

            Undo.PerformUndo();

            Assert.AreEqual(0, edges.Edges.Count);
        }

        [Test]
        public void GenerateUniqueName_StillAvoidsNodesRemovedFromGraph()
        {
            // cursor[bot] High #3:
            // RemoveNodesFromGraph が _nodes からも外していたため、除外直後の New Node が
            // 同じ Identity を返し、ディスク上に残っている既存アセットと衝突していた。
            // グラフからの除外はアセットを消さないので、名前は引き続き使用中として扱うこと。
            var (edges, _) = CreateGraph("G5");
            var node = CreateNodeAsset("Taken");
            edges.AddNode(node);
            EditorUtility.SetDirty(edges);
            AssetDatabase.SaveAssets();

            var viewModel = new SceneGraphViewModel();
            viewModel.LoadGraph(edges);

            Assert.AreNotEqual("Taken", viewModel.GenerateUniqueName("Taken"));

            viewModel.RemoveNodesFromGraph(new List<SceneNodeData> { node });

            Assert.IsFalse(edges.ContainsNode(node), "グラフからは外れていること");
            Assert.IsTrue(node != null, "アセット自体は消えていないこと");
            Assert.AreNotEqual("Taken", viewModel.GenerateUniqueName("Taken"),
                "グラフから外しても、アセットが残っている限り Identity は空かない");
        }
    }
}
