#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// シーン親子関係を編集する GraphView。
    /// ViewModel を通じてデータ操作を行う。
    /// </summary>
    public sealed class SceneGraphView : GraphView
    {
        private const string StyleSheetAssetPath =
            "Assets/OneStarMaker/Scripts/Editor/SceneGraph/View/SceneGraphView.uss";

        private readonly SceneGraphViewModel _viewModel;
        private readonly Dictionary<SceneNodeData, SceneGraphNode> _nodeMap = new();
        private bool _isRebuilding;

        public SceneGraphView(SceneGraphViewModel viewModel)
        {
            _viewModel = viewModel;

            // ── 基本操作の有効化 ──
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            // ── グリッド背景 ──
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // ── ミニマップ ──
            var miniMap = new MiniMap { anchored = true };
            miniMap.SetPosition(new Rect(10, 30, 200, 140));
            Add(miniMap);

            // ── スタイル ──
            // 以前は OneStarMakerCommon 配下を見ていたが、SceneGraph 本体は現在
            // Scripts/Editor 配下へ整理されている。asset 側だけ取り残されると
            // 「コードは style を読み込むのに USS が無い」状態になりやすいので、
            // path はここで 1 箇所へ固定し、欠落時は warning を出して原因追跡しやすくする。
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetAssetPath);
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning(
                    $"[SceneGraph] StyleSheet not found: {StyleSheetAssetPath}. " +
                    "SceneGraphView will continue with default GraphView styling.");
            }

            // ── ViewModel イベント購読 ──
            _viewModel.OnGraphChanged += RebuildGraph;

            // ── GraphView コールバック ──
            graphViewChanged = OnGraphViewChanged;

            // ── 右クリックメニュー ──
            RegisterCallback<ContextualMenuPopulateEvent>(OnContextMenuPopulate);

            // ── SceneAsset ドラッグ＆ドロップ（R-3/R-4）──
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        /// <summary>
        /// ポート互換性チェック。同じノードへの接続を禁止する。
        /// </summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();

            ports.ForEach(port =>
            {
                if (startPort == port) return;
                if (startPort.node == port.node) return;
                if (startPort.direction == port.direction) return;
                compatiblePorts.Add(port);
            });

            return compatiblePorts;
        }

        /// <summary>
        /// グラフを Model から完全に再構築する。
        /// _isRebuilding フラグで graphViewChanged ハンドラの再入を防ぐ。
        /// </summary>
        public void RebuildGraph()
        {
            _isRebuilding = true;
            try
            {
                // Node と Edge のみ削除（MiniMap 等の GraphElement は残す）
                foreach (var element in graphElements.ToList())
                {
                    if (element is Node or UnityEditor.Experimental.GraphView.Edge)
                        RemoveElement(element);
                }
                _nodeMap.Clear();

                if (_viewModel.CurrentEdges == null) return;

                // ── ノードの作成 ──
                foreach (var nodeData in _viewModel.Nodes)
                {
                    if (nodeData == null) continue;
                    AddNodeElement(nodeData);
                }

                // ── エッジの作成 ──
                foreach (var edge in _viewModel.CurrentEdges.Edges)
                {
                    if (edge.Parent == null || edge.Child == null) continue;
                    if (!_nodeMap.TryGetValue(edge.Parent, out var parentNode)) continue;
                    if (!_nodeMap.TryGetValue(edge.Child, out var childNode)) continue;

                    var graphEdge = parentNode.OutputPort.ConnectTo(childNode.InputPort);
                    AddElement(graphEdge);
                }
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        private SceneGraphNode AddNodeElement(SceneNodeData nodeData)
        {
            var node = new SceneGraphNode(nodeData);

            // 位置の復元
            var pos = _viewModel.CurrentLayout?.GetPosition(nodeData) ?? Vector2.zero;
            node.SetPosition(new Rect(pos.x, pos.y, 200, 150));

            AddElement(node);
            _nodeMap[nodeData] = node;

            // GraphView にアタッチ後にポートを再描画（アタッチ前だとレンダリングされない場合がある）
            node.RefreshExpandedState();
            node.RefreshPorts();

            return node;
        }

        /// <summary>
        /// GraphView の変更（Edge 追加/削除、Node 移動等）を処理する。
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            // RebuildGraph 中はハンドラを無視（再入防止）
            if (_isRebuilding) return graphViewChange;

            // ── Edge 作成 ──
            if (graphViewChange.edgesToCreate != null)
            {
                var validEdges = new List<Edge>();

                foreach (var edge in graphViewChange.edgesToCreate)
                {
                    var parentNode = edge.output?.node as SceneGraphNode;
                    var childNode = edge.input?.node as SceneGraphNode;

                    if (parentNode?.NodeData == null || childNode?.NodeData == null)
                        continue;

                    if (_viewModel.ConnectEdge(parentNode.NodeData, childNode.NodeData))
                    {
                        validEdges.Add(edge);
                    }
                }

                // バリデーション失敗したエッジは除外
                graphViewChange.edgesToCreate = validEdges;
            }

            // ── Edge 削除 ──
            if (graphViewChange.elementsToRemove != null)
            {
                foreach (var element in graphViewChange.elementsToRemove)
                {
                    if (element is Edge edge)
                    {
                        var childNode = edge.input?.node as SceneGraphNode;
                        if (childNode?.NodeData != null)
                        {
                            _viewModel.DisconnectEdge(childNode.NodeData);
                        }
                    }
                    else if (element is SceneGraphNode node)
                    {
                        _viewModel.DeleteNode(node.NodeData);
                    }
                }
            }

            // ── Node 移動 ──
            if (graphViewChange.movedElements != null)
            {
                foreach (var element in graphViewChange.movedElements)
                {
                    if (element is SceneGraphNode node)
                    {
                        var rect = node.GetPosition();
                        _viewModel.MoveNode(node.NodeData, new Vector2(rect.x, rect.y));
                    }
                }
            }

            return graphViewChange;
        }

        private void OnContextMenuPopulate(ContextualMenuPopulateEvent evt)
        {
            var localMousePosition = contentViewContainer.WorldToLocal(evt.mousePosition);

            // R-2: 右クリックで即座にノード作成（ダイアログなし）
            evt.menu.AppendAction("Create Node", action =>
            {
                var name = _viewModel.GenerateUniqueName();
                _viewModel.CreateNode(name, localMousePosition);
            });

            evt.menu.AppendSeparator();

            evt.menu.AppendAction("Auto Layout", action =>
            {
                PerformAutoLayout();
            });
        }

        /// <summary>
        /// 外部からの即座ノード作成（ツールバーボタン等）。GraphView 中央にノードを配置する。
        /// </summary>
        public void CreateNodeAtCenter()
        {
            var center = contentViewContainer.WorldToLocal(
                new Vector2(worldBound.x + worldBound.width / 2,
                            worldBound.y + worldBound.height / 2));
            var name = _viewModel.GenerateUniqueName();
            _viewModel.CreateNode(name, center);
        }

        // ── D&D: SceneAsset のドラッグ＆ドロップ（R-3/R-4）──

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (HasSceneAssetInDrag())
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            }
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            var sceneAssetPaths = GetSceneAssetPathsFromDrag();
            if (sceneAssetPaths.Count == 0) return;

            DragAndDrop.AcceptDrag();
            evt.StopPropagation();

            var dropPos = contentViewContainer.WorldToLocal(evt.mousePosition);

            Undo.SetCurrentGroupName("Drop SceneAsset(s)");
            var groupIndex = Undo.GetCurrentGroup();

            for (int i = 0; i < sceneAssetPaths.Count; i++)
            {
                var pos = new Vector2(dropPos.x, dropPos.y + i * 200);
                _viewModel.CreateNodeWithSceneAsset(sceneAssetPaths[i], pos);
            }

            Undo.CollapseUndoOperations(groupIndex);
        }

        private static bool HasSceneAssetInDrag()
        {
            if (DragAndDrop.objectReferences == null) return false;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is SceneAsset) return true;
            }
            return false;
        }

        private static List<string> GetSceneAssetPathsFromDrag()
        {
            var paths = new List<string>();
            if (DragAndDrop.objectReferences == null) return paths;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is SceneAsset)
                {
                    var path = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(path)) paths.Add(path);
                }
            }
            return paths;
        }

        /// <summary>
        /// 簡易的な自動レイアウト（ツリー構造をうっすら表現）。
        /// </summary>
        public void PerformAutoLayout()
        {
            if (_viewModel.CurrentEdges == null || _viewModel.CurrentLayout == null) return;

            Undo.RecordObject(_viewModel.CurrentLayout, "Auto Layout");

            var roots = _viewModel.CurrentEdges.GetRoots();
            float startX = 0;

            foreach (var root in roots)
            {
                LayoutTree(root, startX, 0, out var width);
                startX += width + 100;
            }

            EditorUtility.SetDirty(_viewModel.CurrentLayout);
            RebuildGraph();
        }

        private void LayoutTree(SceneNodeData node, float x, float y, out float subtreeWidth)
        {
            if (_viewModel.CurrentEdges == null || _viewModel.CurrentLayout == null)
            {
                subtreeWidth = 250;
                return;
            }

            var children = _viewModel.CurrentEdges.GetChildren(node);

            if (children.Count == 0)
            {
                _viewModel.CurrentLayout.SetPosition(node, new Vector2(x, y));
                subtreeWidth = 250;
                return;
            }

            float childX = x;
            float totalWidth = 0;

            foreach (var child in children)
            {
                LayoutTree(child, childX, y + 200, out var childWidth);
                childX += childWidth + 50;
                totalWidth += childWidth + 50;
            }
            totalWidth -= 50; // 最後の余計なマージンを除去

            // 親を子の中央に配置
            _viewModel.CurrentLayout.SetPosition(node, new Vector2(x + totalWidth / 2 - 125, y));
            subtreeWidth = Mathf.Max(totalWidth, 250);
        }
    }

}
