#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// Scene Graph Editor のメインウィンドウ。
    /// Menu: OneStarMaker → Scene Graph Editor で開く。
    /// </summary>
    public sealed class SceneGraphEditorWindow : EditorWindow
    {
        private SceneGraphViewModel? _viewModel;
        private SceneGraphView? _graphView;
        private SceneGraphInspectorPanel? _inspectorPanel;
        private ToolbarMenu? _graphSelector;

        private const string LastGraphGuidKey = "SceneGraphEditor_LastGraphGuid";

        [MenuItem("OneStarMaker/Scene Graph Editor")]
        public static void Open()
        {
            var window = GetWindow<SceneGraphEditorWindow>();
            window.titleContent = new GUIContent("Scene Graph Editor");
            window.minSize = new Vector2(800, 500);
        }

        private void CreateGUI()
        {
            _viewModel = new SceneGraphViewModel();
            _viewModel.OnValidationMessage += OnValidationMessage;

            // ── ルートレイアウト ──
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            // ── ツールバー ──
            var toolbar = CreateToolbar();
            root.Add(toolbar);

            // ── メインコンテンツ（Inspector + GraphView）──
            var mainContainer = new VisualElement();
            mainContainer.style.flexDirection = FlexDirection.Row;
            mainContainer.style.flexGrow = 1;
            root.Add(mainContainer);

            // Inspector パネル
            _inspectorPanel = new SceneGraphInspectorPanel(_viewModel);
            mainContainer.Add(_inspectorPanel);

            // GraphView
            _graphView = new SceneGraphView(_viewModel);
            _graphView.style.flexGrow = 1;
            mainContainer.Add(_graphView);

            // Delete キー
            _graphView.RegisterCallback<KeyDownEvent>(OnKeyDown);

            // GraphView のノード選択を 100ms ポーリングで ViewModel に反映
            // （GraphView は選択変更イベントを直接公開しないため）
            rootVisualElement.schedule.Execute(PollGraphViewSelection).Every(100);

            // Undo/Redo で View を Model に同期
            Undo.undoRedoPerformed += OnUndoRedo;

            // 初期グラフのロード
            LoadInitialGraph();

            // W-3: Generate 忘れチェック
            CheckGenerateStale();
        }

        private Toolbar CreateToolbar()
        {
            var toolbar = new Toolbar();

            // ── グラフ選択 ──
            _graphSelector = new ToolbarMenu { text = "Select Graph" };
            RefreshGraphSelector();
            toolbar.Add(_graphSelector);

            // ── New Graph ──
            var newGraphButton = new ToolbarButton(OnNewGraph) { text = "New Graph" };
            toolbar.Add(newGraphButton);

            toolbar.Add(new ToolbarSpacer());

            // ── New Node ──
            var newNodeButton = new ToolbarButton(OnNewNode) { text = "New Node" };
            toolbar.Add(newNodeButton);

            // ── Delete ──
            var deleteButton = new ToolbarButton(OnDeleteSelected) { text = "Delete" };
            toolbar.Add(deleteButton);

            toolbar.Add(new ToolbarSpacer());

            // ── Auto Layout ──
            var autoLayoutButton = new ToolbarButton(OnAutoLayout) { text = "Auto Layout" };
            toolbar.Add(autoLayoutButton);

            // ── Validate ──
            var validateButton = new ToolbarButton(OnValidate) { text = "Validate" };
            toolbar.Add(validateButton);

            toolbar.Add(new ToolbarSpacer { flex = true });

            // ── Generate ──
            var generateButton = new ToolbarButton(OnGenerate) { text = "Generate" };
            generateButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f);
            generateButton.style.color = Color.white;
            generateButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            generateButton.style.paddingLeft = 12;
            generateButton.style.paddingRight = 12;
            toolbar.Add(generateButton);

            return toolbar;
        }

        private void RefreshGraphSelector()
        {
            if (_graphSelector == null || _viewModel == null) return;

            _graphSelector.menu.ClearItems();

            var graphs = _viewModel.GetAvailableGraphs();
            foreach (var graph in graphs)
            {
                var g = graph; // capture
                _graphSelector.menu.AppendAction(
                    g.GraphName,
                    action =>
                    {
                        _viewModel.LoadGraph(g);
                        _graphSelector.text = g.GraphName;
                        SaveLastGraphGuid(g);
                    },
                    action => _viewModel.CurrentEdges == g
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }
        }

        private void LoadInitialGraph()
        {
            if (_viewModel == null) return;

            var graphs = _viewModel.GetAvailableGraphs();
            if (graphs.Count == 0) return;

            // 前回選択していたグラフを EditorPrefs から復元
            SceneGraphEdges? target = null;
            var lastGuid = EditorPrefs.GetString(LastGraphGuidKey, "");
            if (!string.IsNullOrEmpty(lastGuid))
            {
                var lastPath = AssetDatabase.GUIDToAssetPath(lastGuid);
                if (!string.IsNullOrEmpty(lastPath))
                {
                    target = AssetDatabase.LoadAssetAtPath<SceneGraphEdges>(lastPath);
                }
            }

            // 見つからなければ最初のグラフ
            target ??= graphs[0];

            _viewModel.LoadGraph(target);
            if (_graphSelector != null)
                _graphSelector.text = target.GraphName;
            SaveLastGraphGuid(target);
        }

        // ─── ツールバーコマンド ───

        private void OnNewGraph()
        {
            var dialog = CreateInstance<CreateGraphDialog>();
            dialog.OnConfirm = graphName =>
            {
                if (_viewModel == null) return;

                var edges = _viewModel.CreateGraph(graphName);
                if (edges != null)
                {
                    _viewModel.LoadGraph(edges);
                    RefreshGraphSelector();
                    if (_graphSelector != null)
                        _graphSelector.text = graphName;
                    SaveLastGraphGuid(edges);
                }
            };
            var dialogSize = new Vector2(300, 80);
            var dialogPos = GetDialogScreenPosition(dialogSize);
            dialog.ShowAsDropDown(new Rect(dialogPos, Vector2.zero), dialogSize);
        }

        private void OnNewNode()
        {
            if (_viewModel?.CurrentEdges == null)
            {
                EditorUtility.DisplayDialog("Scene Graph", "Please select or create a graph first.", "OK");
                return;
            }

            // R-2: ダイアログなしで即座にノード作成
            _graphView?.CreateNodeAtCenter();
        }

        private void OnDeleteSelected()
        {
            if (_viewModel?.SelectedNode != null)
            {
                _viewModel.DeleteNode(_viewModel.SelectedNode);
            }
        }

        private void OnAutoLayout()
        {
            _graphView?.PerformAutoLayout();
        }

        private void OnValidate()
        {
            if (_viewModel == null) return;

            var results = _viewModel.Validate();
            if (results.Count == 0)
            {
                EditorUtility.DisplayDialog("Validation", "No issues found.", "OK");
            }
            else
            {
                foreach (var result in results)
                {
                    switch (result.Severity)
                    {
                        case SceneGraphValidator.Severity.Error:
                            Debug.LogError($"[SceneGraph] {result}");
                            break;
                        case SceneGraphValidator.Severity.Warning:
                            Debug.LogWarning($"[SceneGraph] {result}");
                            break;
                        default:
                            Debug.Log($"[SceneGraph] {result}");
                            break;
                    }
                }

                var errorCount = results.Count(r => r.Severity == SceneGraphValidator.Severity.Error);
                var warnCount = results.Count(r => r.Severity == SceneGraphValidator.Severity.Warning);
                EditorUtility.DisplayDialog("Validation",
                    $"Found {errorCount} error(s), {warnCount} warning(s). See Console for details.",
                    "OK");
            }
        }

        private void OnGenerate()
        {
            if (_viewModel == null) return;

            var success = _viewModel.Generate();
            if (success)
            {
                EditorUtility.DisplayDialog("Generate", "SceneResource generation completed successfully.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Generate",
                    "Generation failed. Check Console for validation errors.", "OK");
            }
        }

        private void OnValidationMessage(string message)
        {
            EditorUtility.DisplayDialog("Scene Graph", message, "OK");
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            // Delete キーでノード削除
            if (evt.keyCode == KeyCode.Delete && _viewModel?.SelectedNode != null)
            {
                _viewModel.DeleteNode(_viewModel.SelectedNode);
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// GraphView のノード選択をポーリングで検知し ViewModel に反映する。
        /// </summary>
        private SceneNodeData? _lastPolledSelection;

        private void PollGraphViewSelection()
        {
            if (_graphView == null || _viewModel == null) return;

            var selectedNodes = _graphView.selection
                .OfType<SceneGraphNode>()
                .ToList();

            var current = selectedNodes.Count == 1 ? selectedNodes[0].NodeData : null;

            if (current != _lastPolledSelection)
            {
                _lastPolledSelection = current;
                _viewModel.SelectedNode = current;
            }
        }

        private void OnUndoRedo()
        {
            _viewModel?.RefreshNodes();
            _graphView?.RebuildGraph();
        }

        /// <summary>
        /// 選択したグラフの GUID を EditorPrefs に保存し、次回オープン時に復元する。
        /// </summary>
        private static void SaveLastGraphGuid(SceneGraphEdges edges)
        {
            var path = AssetDatabase.GetAssetPath(edges);
            if (!string.IsNullOrEmpty(path))
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                EditorPrefs.SetString(LastGraphGuidKey, guid);
            }
        }

        /// <summary>
        /// ダイアログをウィンドウ中央付近に表示するためのスクリーン座標を取得する。
        /// UIToolkit コールバック内では Event.current が null のため、EditorWindow.position を基準にする。
        /// </summary>
        private Vector2 GetDialogScreenPosition(Vector2 dialogSize)
        {
            var winPos = position;
            return new Vector2(
                winPos.x + (winPos.width - dialogSize.x) / 2,
                winPos.y + (winPos.height - dialogSize.y) / 2);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;

            if (_viewModel != null)
            {
                _viewModel.OnValidationMessage -= OnValidationMessage;
            }
        }

        /// <summary>
        /// W-3: Generate 忘れ検出。ウィンドウ起動時に中間データとの不整合を警告する。
        /// </summary>
        private void CheckGenerateStale()
        {
            if (_viewModel == null) return;

            try
            {
                if (_viewModel.IsGenerateStale())
                {
                    Debug.LogWarning(
                        "[SceneGraph] SceneResourceMap is out of date. " +
                        "Intermediate data has changed since last Generate. " +
                        "Please run Generate to update runtime assets.");
                }
            }
            catch (System.Exception e)
            {
                // ハッシュ計算失敗は無視（初回起動時など）
                Debug.Log($"[SceneGraph] Could not check Generate staleness: {e.Message}");
            }
        }
    }

    /// <summary>
    /// グラフ作成ダイアログ。
    /// </summary>
    public sealed class CreateGraphDialog : EditorWindow
    {
        public System.Action<string>? OnConfirm;
        private string _graphName = "NewGraph";

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var textField = new TextField("Graph Name") { value = _graphName };
            textField.RegisterValueChangedCallback(evt => _graphName = evt.newValue);
            root.Add(textField);

            var button = new Button(() =>
            {
                OnConfirm?.Invoke(_graphName);
                Close();
            })
            {
                text = "Create"
            };
            root.Add(button);

            textField.Focus();
        }
    }
}
