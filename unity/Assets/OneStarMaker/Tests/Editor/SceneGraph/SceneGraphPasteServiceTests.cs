#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Runtime.AssetDescriptions;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.SceneGraph
{
    /// <summary>
    /// SceneGraphPasteService の複製時親引き継ぎ（F2）を検証する。
    /// 既存の Assets/SceneGraphData/ には絶対に書き込まない。専用の一時フォルダを使う。
    /// </summary>
    [TestFixture]
    public sealed class SceneGraphPasteServiceTests
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
        public void Duplicate_InheritsExternalParent()
        {
            // World → Cell_A で Cell_A だけ複製 → World → Cell_A1 ができる
            var (edges, _) = CreateGraph("DupExternalParent");
            var world = CreateNodeAsset("World");
            var cellA = CreateNodeAsset("Cell_A");

            edges.AddNode(world);
            edges.AddNode(cellA);
            edges.AddEdge(world, cellA);
            EditorUtility.SetDirty(edges);
            AssetDatabase.SaveAssets();

            var viewModel = new SceneGraphViewModel();
            viewModel.LoadGraph(edges);
            var service = new SceneGraphPasteService(viewModel);

            var json = service.BuildClipboardJson(new List<SceneNodeData> { cellA });
            var result = service.ApplyPaste(json, forceDuplicate: true);

            Assert.AreEqual(1, result.Count);
            var cellA1 = result[0];
            Assert.AreNotEqual(cellA, cellA1);
            Assert.AreEqual(world, edges.GetParent(cellA1));
            Assert.AreEqual(world, edges.GetParent(cellA));
            Assert.AreEqual(1, edges.Edges.Count(e => e.Parent == world && e.Child == cellA1));
        }

        [Test]
        public void Duplicate_DoesNotDoubleConnect_WhenParentIsInCopySet()
        {
            // World と Cell_A を両方複製 → World1 → Cell_A1 が 1 本だけ
            var (edges, _) = CreateGraph("DupInternalParent");
            var world = CreateNodeAsset("World");
            var cellA = CreateNodeAsset("Cell_A");

            edges.AddNode(world);
            edges.AddNode(cellA);
            edges.AddEdge(world, cellA);
            EditorUtility.SetDirty(edges);
            AssetDatabase.SaveAssets();

            var viewModel = new SceneGraphViewModel();
            viewModel.LoadGraph(edges);
            var service = new SceneGraphPasteService(viewModel);

            var json = service.BuildClipboardJson(new List<SceneNodeData> { world, cellA });
            var result = service.ApplyPaste(json, forceDuplicate: true);

            Assert.AreEqual(2, result.Count);
            var world1 = result[0];
            var cellA1 = result[1];

            Assert.AreEqual(world1, edges.GetParent(cellA1));
            Assert.AreEqual(1, edges.Edges.Count(e => e.Parent == world1 && e.Child == cellA1));
            Assert.AreEqual(0, edges.Edges.Count(e => e.Parent == world && e.Child == cellA1));
        }

        [Test]
        public void ReferencePaste_DoesNotConnectExternalParent()
        {
            // 参照ペースト（別グラフ）では親を勝手に繋がない
            var (sourceEdges, _) = CreateGraph("RefPasteSource");
            var world = CreateNodeAsset("World");
            var cellA = CreateNodeAsset("Cell_A");

            sourceEdges.AddNode(world);
            sourceEdges.AddNode(cellA);
            sourceEdges.AddEdge(world, cellA);
            EditorUtility.SetDirty(sourceEdges);
            AssetDatabase.SaveAssets();

            var (targetEdges, _) = CreateGraph("RefPasteTarget");

            var sourceVm = new SceneGraphViewModel();
            sourceVm.LoadGraph(sourceEdges);
            var sourceService = new SceneGraphPasteService(sourceVm);
            var json = sourceService.BuildClipboardJson(new List<SceneNodeData> { cellA });

            var targetVm = new SceneGraphViewModel();
            targetVm.LoadGraph(targetEdges);
            var targetService = new SceneGraphPasteService(targetVm);

            var result = targetService.ApplyPaste(json, forceDuplicate: false);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(cellA, result[0]);
            Assert.IsTrue(targetEdges.ContainsNode(cellA));
            Assert.IsFalse(targetEdges.ContainsNode(world));
            Assert.IsNull(targetEdges.GetParent(cellA));
            Assert.AreEqual(0, targetEdges.Edges.Count);
        }

        [Test]
        public void Duplicate_ClearsPayloads_W5()
        {
            // 複製ノードの Payloads が空（W-5 の回帰）
            var (edges, _) = CreateGraph("DupPayloads");
            var source = CreateNodeAsset("Source");
            source.Payloads.Add(new AssetPayload());
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();

            edges.AddNode(source);
            EditorUtility.SetDirty(edges);
            AssetDatabase.SaveAssets();

            var viewModel = new SceneGraphViewModel();
            viewModel.LoadGraph(edges);
            var service = new SceneGraphPasteService(viewModel);

            var json = service.BuildClipboardJson(new List<SceneNodeData> { source });
            var result = service.ApplyPaste(json, forceDuplicate: true);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(0, result[0].Payloads.Count);
            Assert.AreNotEqual(source.Identity, result[0].Identity);
        }
    }
}
