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
        private readonly SceneGraphPasteService _pasteService;
        private readonly Dictionary<SceneNodeData, SceneGraphNode> _nodeMap = new();
        private bool _isRebuilding;
        private bool _rebuildScheduled;

        // GraphView 内部クリップボードは外から読めないため、Ctrl+C 経路の JSON をここに残す。
        // static: グラフ切替で SceneGraphView が作り直されても「別グラフへの参照ペースト」を残すため。
        private static string _lastClipboardJson = string.Empty;

        /// <summary>
        /// 実行中の編集コマンド名（"Copy" / "Cut" / "Duplicate" / "Paste" …）。
        /// GraphView は Duplicate（Ctrl+D）でも serializeGraphElements を呼ぶため、
        /// クリップボードを更新してよい操作かどうかをこれで判別する。
        /// </summary>
        private string _activeCommandName = string.Empty;

        /// <summary>Duplicate はクリップボードを変更しない操作である、という区別のための定数。</summary>
        private const string DuplicateCommandName = "Duplicate";

        public SceneGraphView(SceneGraphViewModel viewModel)
        {
            _viewModel = viewModel;
            _pasteService = new SceneGraphPasteService(viewModel);

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
            // R1: 同期的に RebuildGraph を呼ぶと、graphViewChanged ハンドラの内側（BeginBatch の
            // Dispose は return graphViewChange; より前に走る）で GraphView が elementsToRemove /
            // edgesToCreate を適用し終える前に全要素を撤去してしまう（B5 再発）。次フレームへ
            // コアレスして遅延させる。
            _viewModel.OnGraphChanged += ScheduleRebuild;

            // ── GraphView コールバック ──
            graphViewChanged = OnGraphViewChanged;
            serializeGraphElements = OnSerializeGraphElements;
            canPasteSerializedData = OnCanPasteSerializedData;
            unserializeAndPaste = OnUnserializeAndPaste;
            deleteSelection = OnDeleteSelection;

            // ── 実行中コマンドの捕捉 ──
            // TrickleDown: GraphView 本体がコマンドを処理する前に名前を控える必要がある
            // （serializeGraphElements はその処理の内側から呼ばれるため）。
            RegisterCallback<ExecuteCommandEvent>(OnExecuteCommandCapture, TrickleDown.TrickleDown);

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
                // 全要素を作り直すと GraphView の選択は失われる。Paste 経路だけが自前で
                // 復元していたため、削除 / 接続 / Undo・Redo / グラフ再読込では選択が消えていた。
                // ここで一括して面倒を見る（対象が残っていれば選択し直す）。
                var previousSelection = selection.OfType<SceneGraphNode>()
                    .Select(n => n.NodeData)
                    .Where(n => n != null)
                    .Cast<SceneNodeData>()
                    .ToList();

                // Node と Edge のみ削除（MiniMap 等の GraphElement は残す）
                foreach (var element in graphElements.ToList())
                {
                    if (element is Node or UnityEditor.Experimental.GraphView.Edge)
                        RemoveElement(element);
                }
                _nodeMap.Clear();

                // §2.3(d): 破棄済み ScriptableObject は == null が true になる。?. / ?? は使わない。
                var currentEdges = _viewModel.CurrentEdges;
                if (currentEdges == null) return;

                // ── ノードの作成 ──
                // B9: `_viewModel.Nodes` は Nodes フォルダの全アセットを含む（一意名の採番や
                // Generate が全件を必要とするため）。描画してよいのは **現在のグラフに所属している
                // ノードだけ**。全件描くと、グラフから除外したノードが RefreshNodes のたびに
                // (0,0) へ復活し、「Remove from Graph が効いていない」ように見える。
                foreach (var nodeData in _viewModel.Nodes)
                {
                    if (nodeData == null) continue;
                    if (!currentEdges.ContainsNode(nodeData)) continue;
                    AddNodeElement(nodeData);
                }

                // ── エッジの作成 ──
                foreach (var edge in currentEdges.Edges)
                {
                    if (edge.Parent == null || edge.Child == null) continue;
                    if (!_nodeMap.TryGetValue(edge.Parent, out var parentNode)) continue;
                    if (!_nodeMap.TryGetValue(edge.Child, out var childNode)) continue;

                    var graphEdge = parentNode.OutputPort.ConnectTo(childNode.InputPort);
                    AddElement(graphEdge);
                }

                RestoreSelection(previousSelection);
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        /// <summary>
        /// リビルド前に選択されていたノードのうち、まだ描画対象に残っているものを選択し直す。
        /// 復元できなかったもの（グラフから除外された / 破棄された）は黙って捨てる。
        /// </summary>
        private void RestoreSelection(IReadOnlyList<SceneNodeData> previousSelection)
        {
            if (previousSelection.Count == 0) return;

            ClearSelection();
            foreach (var nodeData in previousSelection)
            {
                if (nodeData == null) continue;
                if (_nodeMap.TryGetValue(nodeData, out var visualNode))
                {
                    AddToSelection(visualNode);
                }
            }
        }

        /// <summary>
        /// リビルドを次フレームへ遅延させる（R1 対策）。graphViewChanged ハンドラの内側で同期的に
        /// RebuildGraph を走らせると、GraphView が elementsToRemove / edgesToCreate を適用し終える前に
        /// 全要素を撤去してしまう（BeginBatch の Dispose は return graphViewChange; より前に走るため）。
        /// 複数回呼ばれても 1 回にコアレスする。
        /// </summary>
        private void ScheduleRebuild()
        {
            if (_rebuildScheduled) return;
            _rebuildScheduled = true;
            schedule.Execute(() =>
            {
                _rebuildScheduled = false;
                RebuildGraph();
            }).ExecuteLater(0);
        }

        private SceneGraphNode AddNodeElement(SceneNodeData nodeData)
        {
            var node = new SceneGraphNode(nodeData);

            // 位置の復元
            var layout = _viewModel.CurrentLayout;
            var pos = layout != null ? layout.GetPosition(nodeData) : Vector2.zero;
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
        /// 一括操作は 1 つの BeginBatch で囲み、削除/切断/移動をそれぞれリストへ集めてから
        /// 一括コマンドを 1 回ずつ呼ぶ（B5: 逐次削除による RebuildGraph の再入連打を防ぐ）。
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            // RebuildGraph 中はハンドラを無視（再入防止）
            if (_isRebuilding) return graphViewChange;

            using (_viewModel.BeginBatch("Edit Scene Graph"))
            {
                // ── Edge 作成 ──
                if (graphViewChange.edgesToCreate != null)
                {
                    var validEdges = new List<Edge>();
                    var batchConnectHandled = false;

                    foreach (var edge in graphViewChange.edgesToCreate)
                    {
                        var parentNode = edge.output?.node as SceneGraphNode;
                        var childNode = edge.input?.node as SceneGraphNode;

                        if (parentNode?.NodeData == null || childNode?.NodeData == null)
                            continue;

                        // 複数選択中に子側が選択に含まれるエッジを引いたら、選択全部を同じ親へ一括接続。
                        // 視覚エッジは ConnectEdges → OnGraphChanged → ScheduleRebuild が引き直すため
                        // validEdges には入れない（GraphView にも作らせると二重になる）。
                        if (!batchConnectHandled)
                        {
                            var selectedNodes = selection.OfType<SceneGraphNode>().ToList();
                            if (selectedNodes.Count >= 2 && selectedNodes.Contains(childNode))
                            {
                                var children = selectedNodes
                                    .Select(n => n.NodeData)
                                    .Where(n => n != null && n != parentNode.NodeData)
                                    .ToList();

                                if (children.Count > 0)
                                {
                                    _viewModel.ConnectEdges(parentNode.NodeData, children);
                                    batchConnectHandled = true;
                                    continue;
                                }
                            }
                        }

                        if (_viewModel.ConnectEdge(parentNode.NodeData, childNode.NodeData))
                        {
                            validEdges.Add(edge);
                        }
                    }

                    // バリデーション失敗したエッジは除外
                    graphViewChange.edgesToCreate = validEdges;
                }

                // ── Edge / Node 削除 ──
                if (graphViewChange.elementsToRemove != null)
                {
                    var nodesToRemove = new List<SceneNodeData>();
                    var edgeChildrenToDisconnect = new List<SceneNodeData>();

                    foreach (var element in graphViewChange.elementsToRemove)
                    {
                        if (element is Edge edge)
                        {
                            var childNode = edge.input?.node as SceneGraphNode;
                            if (childNode?.NodeData != null)
                            {
                                edgeChildrenToDisconnect.Add(childNode.NodeData);
                            }
                        }
                        else if (element is SceneGraphNode node)
                        {
                            if (node.NodeData != null)
                            {
                                nodesToRemove.Add(node.NodeData);
                            }
                        }
                    }

                    if (edgeChildrenToDisconnect.Count > 0)
                    {
                        _viewModel.DisconnectEdges(edgeChildrenToDisconnect);
                    }

                    if (nodesToRemove.Count > 0)
                    {
                        _viewModel.RemoveNodesFromGraph(nodesToRemove);
                    }
                }

                // ── Node 移動 ──
                if (graphViewChange.movedElements != null)
                {
                    var moves = new List<(SceneNodeData Node, Vector2 Position)>();

                    foreach (var element in graphViewChange.movedElements)
                    {
                        if (element is SceneGraphNode node && node.NodeData != null)
                        {
                            var rect = node.GetPosition();
                            moves.Add((node.NodeData, new Vector2(rect.x, rect.y)));
                        }
                    }

                    if (moves.Count > 0)
                    {
                        _viewModel.MoveNodes(moves);
                    }
                }
            }

            return graphViewChange;
        }

        // ── クリップボード: Copy / Paste / Duplicate / Delete ──
        // Unity の CopySelectionCallback() 等は版によってアクセシビリティが違うので依存しない。
        // 自前メソッドを作り、GraphView のデリゲートはそこへ委譲する。

        public void CopySelectionToClipboard()
        {
            StoreClipboardJson(BuildClipboardJson(selection.OfType<GraphElement>()));
        }

        public void PasteFromClipboard()
        {
            var json = GetPasteSource();
            if (!SceneGraphClipboard.CanPaste(json)) return;
            ApplyPaste(json, forceDuplicate: false);
        }

        public void DuplicateSelection()
        {
            var json = BuildClipboardJson(selection.OfType<GraphElement>());
            if (string.IsNullOrEmpty(json)) return;
            ApplyPaste(json, forceDuplicate: true);
        }

        /// <summary>
        /// 選択されているノード（+ グラフ内エッジ）をグラフから除外する。
        /// アセットの実削除はここからは絶対に呼ばない。
        /// R2: 以前は `public new void DeleteSelection()` として基底 GraphView の同名メンバを隠していた。
        /// 基底側の経路（GraphView.DeleteSelection()）から呼ばれると `new` 側ではなく基底実装が動いて
        /// しまい意図が保証されないため、専用名にリネームした。
        /// </summary>
        public void RemoveSelectionFromGraph()
        {
            var nodesToRemove = selection.OfType<SceneGraphNode>()
                .Select(n => n.NodeData)
                .Where(n => n != null)
                .Cast<SceneNodeData>()
                .ToList();

            var edgeChildrenToDisconnect = selection.OfType<Edge>()
                .Select(e => (e.input?.node as SceneGraphNode)?.NodeData)
                .Where(n => n != null)
                .Cast<SceneNodeData>()
                .ToList();

            if (nodesToRemove.Count == 0 && edgeChildrenToDisconnect.Count == 0) return;

            using (_viewModel.BeginBatch("Remove from Graph"))
            {
                if (edgeChildrenToDisconnect.Count > 0)
                {
                    _viewModel.DisconnectEdges(edgeChildrenToDisconnect);
                }

                if (nodesToRemove.Count > 0)
                {
                    _viewModel.RemoveNodesFromGraph(nodesToRemove);
                }
            }
        }

        /// <summary>実行中の編集コマンド名を控える。GraphView が処理する前に呼ばれる。</summary>
        private void OnExecuteCommandCapture(ExecuteCommandEvent evt)
        {
            _activeCommandName = evt.commandName;
        }

        private string OnSerializeGraphElements(IEnumerable<GraphElement> elements)
        {
            var json = BuildClipboardJson(elements);

            // GraphView は Copy / Cut だけでなく Duplicate（Ctrl+D）でもこのデリゲートを呼ぶ。
            // Duplicate はクリップボードを変更しない操作なので、ここで保存すると
            // 「A を Ctrl+C → B を Ctrl+D」でユーザーのコピー内容が B に破壊される。
            var isDuplicate = _activeCommandName == DuplicateCommandName;
            _activeCommandName = string.Empty;

            if (!isDuplicate)
            {
                StoreClipboardJson(json);
            }

            return json;
        }

        private bool OnCanPasteSerializedData(string data)
        {
            return SceneGraphClipboard.CanPaste(GetPasteSource());
        }

        /// <summary>
        /// ペースト元 JSON の唯一の窓口。systemCopyBuffer が有効ならそれを、通らなければ Ctrl+C 由来の static スナップショットを使う。
        /// </summary>
        private static string GetPasteSource()
        {
            var systemBuffer = EditorGUIUtility.systemCopyBuffer;
            if (SceneGraphClipboard.CanPaste(systemBuffer))
                return systemBuffer;
            return _lastClipboardJson;
        }

        /// <summary>
        /// Copy 経路（メニュー / Ctrl+C）の書き込み口。CanPaste を通る内容だけを両系統へ書く。
        /// </summary>
        private static void StoreClipboardJson(string json)
        {
            if (!SceneGraphClipboard.CanPaste(json)) return;
            _lastClipboardJson = json;
            EditorGUIUtility.systemCopyBuffer = json;
        }

        private void OnUnserializeAndPaste(string operationName, string data)
        {
            // serialize されずにこの経路へ来た場合に備えて必ず落とす。
            // 残しておくと、次に serialize されたとき誤って Duplicate 扱いになり
            // クリップボードが更新されなくなる。
            _activeCommandName = string.Empty;

            var forceDuplicate = operationName == DuplicateCommandName;

            // Duplicate は「いま選択されている要素」を複製する操作なので、直前に
            // OnSerializeGraphElements が作った data をそのまま使う（クリップボードは見ない）。
            // Paste はクリップボードの内容を貼る操作なので GetPasteSource() を唯一の窓口とする。
            var json = forceDuplicate ? data : GetPasteSource();
            ApplyPaste(json, forceDuplicate);
        }

        private void OnDeleteSelection(string operationName, GraphView.AskUser askUser)
        {
            RemoveSelectionFromGraph();
        }

        /// <summary>
        /// GraphElement 群を SceneNodeData に変換してから、ペーストサービスへ委譲する薄いラッパ。
        /// </summary>
        private string BuildClipboardJson(IEnumerable<GraphElement> elements)
        {
            var nodes = elements.OfType<SceneGraphNode>()
                .Select(n => n.NodeData)
                .Where(n => n != null)
                .Cast<SceneNodeData>()
                .Distinct()
                .ToList();
            return nodes.Count == 0 ? string.Empty : _pasteService.BuildClipboardJson(nodes);
        }

        /// <summary>
        /// クリップボード JSON を貼り付け、結果ノードを選択状態にする。
        /// ドメイン処理はペーストサービスへ委譲し、選択復元だけ View に残す。
        /// </summary>
        private void ApplyPaste(string json, bool forceDuplicate)
        {
            var result = _pasteService.ApplyPaste(json, forceDuplicate);
            if (result.Count == 0) return;

            // ペースト後は生成/追加されたノードを選択状態にする。
            // RebuildGraph は ScheduleRebuild で次フレームへ遅延している（R1）ため、
            // _nodeMap の更新を待つ必要がある。ScheduleRebuild のスケジュールより後に
            // キューされるので実行順序は保たれる。
            schedule.Execute(() =>
            {
                ClearSelection();
                foreach (var node in result)
                {
                    if (_nodeMap.TryGetValue(node, out var visualNode))
                        AddToSelection(visualNode);
                }
            }).ExecuteLater(0);
        }

        private static SceneGraphNode? ResolveContextTargetNode(IEventHandler? target)
        {
            if (target is SceneGraphNode node) return node;
            if (target is VisualElement ve) return ve.GetFirstAncestorOfType<SceneGraphNode>();
            return null;
        }

        private void OnContextMenuPopulate(ContextualMenuPopulateEvent evt)
        {
            var localMousePosition = contentViewContainer.WorldToLocal(evt.mousePosition);
            var targetNode = ResolveContextTargetNode(evt.target);

            var selectedGraphNodes = selection.OfType<SceneGraphNode>().ToList();
            var selectedNodes = selectedGraphNodes
                .Select(n => n.NodeData)
                .Where(n => n != null)
                .Cast<SceneNodeData>()
                .ToList();

            if (selectedGraphNodes.Count == 0)
            {
                // R-2: 右クリックで即座にノード作成（ダイアログなし）
                evt.menu.AppendAction("Create Node", _ =>
                {
                    var name = _viewModel.GenerateUniqueName();
                    _viewModel.CreateNode(name, localMousePosition);
                });

                if (SceneGraphClipboard.CanPaste(GetPasteSource()))
                {
                    evt.menu.AppendAction("Paste", _ => PasteFromClipboard());
                }

                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Auto Layout", _ => PerformAutoLayout());
                return;
            }

            evt.menu.AppendAction("Copy", _ => CopySelectionToClipboard());
            evt.menu.AppendAction("Duplicate", _ => DuplicateSelection());

            evt.menu.AppendSeparator();

            // 「Parent to」の親は右クリックしたノードとする。選択の順序には依存させない。
            if (selectedNodes.Count >= 2 && targetNode != null && targetNode.NodeData != null)
            {
                var parentData = targetNode.NodeData;
                evt.menu.AppendAction($"Parent to '{parentData.Identity}'", _ =>
                {
                    var children = selectedNodes.Where(n => n != parentData).ToList();
                    _viewModel.ConnectEdges(parentData, children);
                });
            }

            var currentEdges = _viewModel.CurrentEdges;
            var nodesWithParent = currentEdges != null
                ? selectedNodes.Where(n => currentEdges.GetParent(n) != null).ToList()
                : new List<SceneNodeData>();
            if (nodesWithParent.Count > 0)
            {
                evt.menu.AppendAction("Unparent Selected", _ => _viewModel.DisconnectEdges(nodesWithParent));
            }

            evt.menu.AppendAction("Remove from Graph", _ => _viewModel.RemoveNodesFromGraph(selectedNodes));

            evt.menu.AppendSeparator();

            evt.menu.AppendAction("Select in Project", _ =>
            {
                Selection.objects = selectedNodes.Cast<UnityEngine.Object>().ToArray();
                if (selectedNodes.Count > 0) EditorGUIUtility.PingObject(selectedNodes[0]);
            });

            evt.menu.AppendAction("Frame Selection", _ => FrameSelection());

            evt.menu.AppendSeparator();

            evt.menu.AppendAction("Auto Layout", _ => PerformAutoLayout());
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

            // §3.1: BeginBatch を通す（以前は IncrementCurrentGroup 無しで直前の操作を巻き込んでいた）
            using (_viewModel.BeginBatch("Drop SceneAsset(s)"))
            {
                for (int i = 0; i < sceneAssetPaths.Count; i++)
                {
                    var pos = new Vector2(dropPos.x, dropPos.y + i * 200);
                    _viewModel.CreateNodeWithSceneAsset(sceneAssetPaths[i], pos);
                }
            }
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
            // R3: BeginBatch を通す（Undo.IncrementCurrentGroup 無しで直前の無関係な操作を
            // 巻き込んでいた B8 が Auto Layout にだけ残っていた）。
            var currentEdges = _viewModel.CurrentEdges;
            var currentLayout = _viewModel.CurrentLayout;
            if (currentEdges == null || currentLayout == null) return;

            using (_viewModel.BeginBatch("Auto Layout"))
            {
                Undo.RecordObject(currentLayout, "Auto Layout");

                var roots = currentEdges.GetRoots();
                float startX = 0;

                foreach (var root in roots)
                {
                    LayoutTree(root, startX, 0, out var width);
                    startX += width + 100;
                }

                EditorUtility.SetDirty(currentLayout);
            }

            // R1: ViewModel 側に公開の再描画要求が無いため、バッチを抜けた後に自前で ScheduleRebuild する。
            ScheduleRebuild();
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
