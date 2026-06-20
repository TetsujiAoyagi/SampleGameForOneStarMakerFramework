#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// SceneGraph Editor の ViewModel。
    /// Model（ScriptableObject）と View（GraphView）を仲介する。
    /// 全操作は ViewModel のコマンド経由で行い、Undo/Redo を統一管理する。
    /// </summary>
    public class SceneGraphViewModel
    {
        private const string NodesFolder = "Assets/SceneGraphData/Nodes";
        private const string GraphsFolder = "Assets/SceneGraphData/Graphs";
        private const string LayoutsFolder = "Assets/SceneGraphData/Layouts";

        // ── Model 参照 ──
        private SceneGraphEdges? _currentEdges;
        private SceneGraphLayout? _currentLayout;
        private readonly List<SceneNodeData> _nodes = new();

        // ── 選択状態 ──
        private SceneNodeData? _selectedNode;

        // ── イベント（View への通知）──
        public event Action? OnGraphChanged;
        public event Action<SceneNodeData?>? OnSelectionChanged;
        public event Action<string>? OnValidationMessage;

        // ── プロパティ ──
        public IReadOnlyList<SceneNodeData> Nodes => _nodes;
        public SceneGraphEdges? CurrentEdges => _currentEdges;
        public SceneGraphLayout? CurrentLayout => _currentLayout;

        public SceneNodeData? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode == value) return;
                _selectedNode = value;
                OnSelectionChanged?.Invoke(_selectedNode);
            }
        }

        // ─── グラフ一覧 ───

        /// <summary>
        /// 利用可能なグラフ一覧を取得する。
        /// </summary>
        public List<SceneGraphEdges> GetAvailableGraphs()
        {
            EnsureDirectoryExists(GraphsFolder);
            var guids = AssetDatabase.FindAssets("t:SceneGraphEdges", new[] { GraphsFolder });
            var graphs = new List<SceneGraphEdges>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var g = AssetDatabase.LoadAssetAtPath<SceneGraphEdges>(path);
                if (g != null) graphs.Add(g);
            }
            return graphs;
        }

        // ─── ロード ───

        /// <summary>
        /// グラフをロードする。ノード・エッジ・レイアウトすべてを読み込む。
        /// </summary>
        public void LoadGraph(SceneGraphEdges edges)
        {
            _currentEdges = edges;

            // 対応するレイアウトをロード
            var layoutPath = $"{LayoutsFolder}/{edges.GraphName}_Layout.asset";
            _currentLayout = AssetDatabase.LoadAssetAtPath<SceneGraphLayout>(layoutPath);

            if (_currentLayout == null)
            {
                EnsureDirectoryExists(LayoutsFolder);
                _currentLayout = ScriptableObject.CreateInstance<SceneGraphLayout>();
                AssetDatabase.CreateAsset(_currentLayout, layoutPath);
                AssetDatabase.SaveAssets();
            }

            // 全ノードをロード
            RefreshNodes();

            OnGraphChanged?.Invoke();
        }

        /// <summary>
        /// 現在のグラフのノード一覧を再読み込みする。
        /// グラフの GraphNodes + Nodes フォルダの両方を参照する。
        /// </summary>
        public void RefreshNodes()
        {
            _nodes.Clear();

            // グラフのノード一覧から復元
            if (_currentEdges != null)
            {
                foreach (var node in _currentEdges.GraphNodes)
                {
                    if (node != null && !_nodes.Contains(node))
                        _nodes.Add(node);
                }
            }

            // Nodes フォルダにあるがグラフ未登録のノードも収集（選択肢として提示用）
            if (AssetDatabase.IsValidFolder(NodesFolder))
            {
                var guids = AssetDatabase.FindAssets("t:SceneNodeData", new[] { NodesFolder });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var node = AssetDatabase.LoadAssetAtPath<SceneNodeData>(path);
                    if (node != null && !_nodes.Contains(node))
                        _nodes.Add(node);
                }
            }
        }

        // ─── コマンド: ノード作成 ───

        /// <summary>
        /// グラフ内で一意な名前を自動生成する（"NewScene", "NewScene1", "NewScene2", …）。
        /// </summary>
        public string GenerateUniqueName(string baseName = "NewScene")
        {
            var existingNames = new HashSet<string>();
            foreach (var n in _nodes)
            {
                if (n != null) existingNames.Add(n.Identity);
            }

            if (!existingNames.Contains(baseName)) return baseName;

            for (int i = 1; ; i++)
            {
                var candidate = $"{baseName}{i}";
                if (!existingNames.Contains(candidate)) return candidate;
            }
        }

        /// <summary>
        /// 指定 Identity のノードが既に存在するか検索する。
        /// ロード済みリスト → アセットフォルダの順で探す。
        /// </summary>
        private SceneNodeData? FindExistingNode(string identity)
        {
            foreach (var node in _nodes)
            {
                if (node != null && node.Identity == identity) return node;
            }

            if (AssetDatabase.IsValidFolder(NodesFolder))
            {
                var guids = AssetDatabase.FindAssets("t:SceneNodeData", new[] { NodesFolder });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var node = AssetDatabase.LoadAssetAtPath<SceneNodeData>(path);
                    if (node != null && node.Identity == identity) return node;
                }
            }
            return null;
        }

        /// <summary>
        /// 既存ノードを現在のグラフに再利用する。
        /// </summary>
        private SceneNodeData ReuseNode(SceneNodeData node, Vector2 position)
        {
            Debug.Log($"[SceneGraph] Reusing existing node '{node.Identity}'.");

            if (_currentEdges != null && _currentEdges.ContainsNode(node))
            {
                Debug.Log($"[SceneGraph] Node '{node.Identity}' is already in the current graph.");
                return node;
            }

            if (!_nodes.Contains(node)) _nodes.Add(node);

            if (_currentEdges != null)
            {
                Undo.RecordObject(_currentEdges, $"Add existing node '{node.Identity}' to graph");
                _currentEdges.AddNode(node);
                EditorUtility.SetDirty(_currentEdges);
            }

            if (_currentLayout != null)
            {
                Undo.RecordObject(_currentLayout, $"Set position for '{node.Identity}'");
                _currentLayout.SetPosition(node, position);
                EditorUtility.SetDirty(_currentLayout);
            }

            AssetDatabase.SaveAssets();
            OnGraphChanged?.Invoke();
            return node;
        }

        /// <summary>
        /// 新規ノードを作成する。同名ノードが既に存在する場合は再利用する（R-6）。
        /// </summary>
        public SceneNodeData? CreateNode(string identity, Vector2 position)
        {
            if (string.IsNullOrWhiteSpace(identity))
            {
                OnValidationMessage?.Invoke("Identity cannot be empty.");
                return null;
            }

            // R-6: 同名ノードが存在すれば再利用
            var existing = FindExistingNode(identity);
            if (existing != null)
            {
                return ReuseNode(existing, position);
            }

            EnsureDirectoryExists(NodesFolder);

            var node = ScriptableObject.CreateInstance<SceneNodeData>();
            node.Identity = identity;
            node.name = identity;

            var path = $"{NodesFolder}/{identity}.asset";
            if (AssetDatabase.AssetPathExists(path))
            {
                // rename
                path = AssetDatabase.GenerateUniqueAssetPath(path);
            }
            AssetDatabase.CreateAsset(node, path);
            Undo.RegisterCreatedObjectUndo(node, $"Create Node '{identity}'");

            _nodes.Add(node);

            // グラフにノードを登録
            if (_currentEdges != null)
            {
                Undo.RecordObject(_currentEdges, $"Add node '{identity}' to graph");
                _currentEdges.AddNode(node);
                EditorUtility.SetDirty(_currentEdges);
            }

            // レイアウトに初期位置を設定
            if (_currentLayout != null)
            {
                Undo.RecordObject(_currentLayout, $"Add position for '{identity}'");
                _currentLayout.SetPosition(node, position);
                EditorUtility.SetDirty(_currentLayout);
            }

            AssetDatabase.SaveAssets();

            OnGraphChanged?.Invoke();
            return node;
        }

        // ─── コマンド: ノード削除 ───

        /// <summary>
        /// SceneAsset のドラッグ＆ドロップでノードを作成する。
        /// Identity = SceneAsset.name、Payload[0] = AssetReference(GUID) をセットする。
        /// 同名ノードが既に存在すれば再利用する（R-6）。
        /// </summary>
        public SceneNodeData? CreateNodeWithSceneAsset(string sceneAssetPath, Vector2 position)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sceneAssetPath);
            if (sceneAsset == null) return null;

            var identity = sceneAsset.name;
            var node = CreateNode(identity, position);
            if (node == null) return null;

            // Payload[0] に SceneAsset の AssetReference をセット
            var guid = AssetDatabase.AssetPathToGUID(sceneAssetPath);
            if (!string.IsNullOrEmpty(guid))
            {
                Undo.RecordObject(node, $"Set Payload for '{identity}'");

                if (node.Payloads.Count == 0)
                {
                    node.Payloads.Add(new AssetPayload());
                }
                node.Payloads[0].Reference = new AssetReference(guid);
                EditorUtility.SetDirty(node);
                AssetDatabase.SaveAssets();
            }

            return node;
        }

        // ─── コマンド: ノード削除 ───

        /// <summary>
        /// ノードを削除する。関連するエッジとレイアウトも削除する。
        /// </summary>
        public void DeleteNode(SceneNodeData node)
        {
            if (node == null) return;

            Undo.SetCurrentGroupName($"Delete Node '{node.Identity}'");
            var groupIndex = Undo.GetCurrentGroup();

            // エッジ削除 + グラフからノード除去
            if (_currentEdges != null)
            {
                Undo.RecordObject(_currentEdges, "Remove node from graph");
                _currentEdges.RemoveNode(node);
                EditorUtility.SetDirty(_currentEdges);
            }

            // レイアウト削除
            if (_currentLayout != null)
            {
                Undo.RecordObject(_currentLayout, "Remove layout");
                _currentLayout.RemovePosition(node);
                EditorUtility.SetDirty(_currentLayout);
            }

            // ノード削除
            _nodes.Remove(node);
            var path = AssetDatabase.GetAssetPath(node);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            if (_selectedNode == node)
                SelectedNode = null;

            Undo.CollapseUndoOperations(groupIndex);
            OnGraphChanged?.Invoke();
        }

        // ─── コマンド: エッジ接続 ───

        /// <summary>
        /// 親子エッジを作成する。サイクルチェック + 既存親の自動解除を行う。
        /// GraphView がビジュアルエッジを処理するため OnGraphChanged は発火しない。
        /// </summary>
        public bool ConnectEdge(SceneNodeData parent, SceneNodeData child)
        {
            if (_currentEdges == null) return false;

            // サイクルチェック
            if (_currentEdges.WouldCreateCycle(parent, child))
            {
                OnValidationMessage?.Invoke(
                    $"Cannot connect '{parent.Identity}' → '{child.Identity}': would create a cycle.");
                return false;
            }

            Undo.RecordObject(_currentEdges, $"Connect '{parent.Identity}' → '{child.Identity}'");

            // 既存の親があれば削除（ツリー制約: 最大1親）
            _currentEdges.RemoveEdgeByChild(child);

            // 新エッジ追加
            _currentEdges.AddEdge(parent, child);
            EditorUtility.SetDirty(_currentEdges);

            // ⮿ OnGraphChanged は発火しない（GraphView がビジュアルエッジを追加する）
            return true;
        }

        // ─── コマンド: エッジ切断 ───

        /// <summary>
        /// 子ノードのエッジを切断する。
        /// GraphView がビジュアルエッジを処理するため OnGraphChanged は発火しない。
        /// </summary>
        public void DisconnectEdge(SceneNodeData child)
        {
            if (_currentEdges == null) return;

            Undo.RecordObject(_currentEdges, $"Disconnect '{child.Identity}'");
            _currentEdges.RemoveEdgeByChild(child);
            EditorUtility.SetDirty(_currentEdges);

            // ⮿ OnGraphChanged は発火しない（GraphView がビジュアルエッジを削除する）
        }

        // ─── コマンド: ノード移動 ───

        /// <summary>
        /// ノード位置を更新する。エッジファイルは触らない。
        /// </summary>
        public void MoveNode(SceneNodeData node, Vector2 newPosition)
        {
            if (_currentLayout == null) return;

            Undo.RecordObject(_currentLayout, $"Move '{node.Identity}'");
            _currentLayout.SetPosition(node, newPosition);
            EditorUtility.SetDirty(_currentLayout);
        }

        // ─── コマンド: ノードプロパティ変更 ───

        /// <summary>
        /// ノードの Identity を変更する。重複チェック付き。
        /// </summary>
        public bool RenameNode(SceneNodeData node, string newIdentity)
        {
            if (!SceneGraphValidator.IsValidIdentity(newIdentity, node, _nodes))
            {
                OnValidationMessage?.Invoke($"Identity '{newIdentity}' is invalid or already in use.");
                return false;
            }

            Undo.RecordObject(node, $"Rename '{node.Identity}' → '{newIdentity}'");
            node.Identity = newIdentity;
            node.name = newIdentity;
            EditorUtility.SetDirty(node);

            // アセットのリネーム
            var path = AssetDatabase.GetAssetPath(node);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.RenameAsset(path, newIdentity);
            }

            OnGraphChanged?.Invoke();
            return true;
        }

        // ─── コマンド: グラフ新規作成 ───

        /// <summary>
        /// 新しいグラフを作成する。
        /// </summary>
        public SceneGraphEdges? CreateGraph(string graphName)
        {
            if (string.IsNullOrWhiteSpace(graphName))
            {
                OnValidationMessage?.Invoke("Graph name cannot be empty.");
                return null;
            }

            EnsureDirectoryExists(GraphsFolder);
            EnsureDirectoryExists(LayoutsFolder);

            var edgesPath = $"{GraphsFolder}/{graphName}.asset";
            if (AssetDatabase.LoadAssetAtPath<SceneGraphEdges>(edgesPath) != null)
            {
                OnValidationMessage?.Invoke($"Graph '{graphName}' already exists.");
                return null;
            }

            var edges = ScriptableObject.CreateInstance<SceneGraphEdges>();
            edges.GraphName = graphName;
            AssetDatabase.CreateAsset(edges, edgesPath);

            var layout = ScriptableObject.CreateInstance<SceneGraphLayout>();
            var layoutPath = $"{LayoutsFolder}/{graphName}_Layout.asset";
            AssetDatabase.CreateAsset(layout, layoutPath);

            AssetDatabase.SaveAssets();

            return edges;
        }

        // ─── コマンド: Generate ───

        /// <summary>
        /// SceneResource / SceneResourceMap を生成する。
        /// </summary>
        public bool Generate()
        {
            var allGraphEdges = GetAvailableGraphs();
            RefreshNodes();

            var result = SceneResourceGenerator.Generate(
                _nodes,
                allGraphEdges);

            if (result)
            {
                SceneResourceGenerator.CleanupOrphanedResources(_nodes);
            }

            return result;
        }

        // ─── コマンド: バリデーション ───

        /// <summary>
        /// Generate 忘れを検出する（W-3）。
        /// SceneResourceMap に保存されたハッシュと現在の中間データから再計算したハッシュを比較する。
        /// </summary>
        /// <returns>true: 不整合あり（Generate 忘れ）、false: 整合。SceneResourceMap が存在しない場合も true。</returns>
        public bool IsGenerateStale()
        {
            const string mapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(mapPath);
            if (map == null) return true; // まだ Generate されていない

            var savedHash = map.GenerateHash;
            if (string.IsNullOrEmpty(savedHash)) return true;

            var allGraphEdges = GetAvailableGraphs();
            RefreshNodes();

            // 現在の中間データからハッシュを再計算して比較
            var currentHash = SceneResourceGenerator.ComputeCurrentHash(_nodes, allGraphEdges);
            return savedHash != currentHash;
        }

        /// <summary>
        /// 全バリデーションを実行し、結果を返す。
        /// </summary>
        public List<SceneGraphValidator.ValidationResult> Validate()
        {
            var allGraphEdges = GetAvailableGraphs();
            RefreshNodes();
            return SceneGraphValidator.ValidateAll(_nodes, allGraphEdges);
        }

        // ─── ユーティリティ ───

        private static void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Replace("\\", "/").Split('/');
                var current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }
    }
}
