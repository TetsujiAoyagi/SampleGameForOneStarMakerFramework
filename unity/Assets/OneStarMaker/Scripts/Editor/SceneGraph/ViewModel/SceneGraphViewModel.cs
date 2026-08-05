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
        // internal static: テストが一時フォルダへ差し替えられるようにするための最小限の変更。
        // それ以上の抽象化（IAssetDatabase 等）は導入しない。
        internal static string NodesFolder { get; set; } = "Assets/SceneGraphData/Nodes";
        internal static string GraphsFolder { get; set; } = "Assets/SceneGraphData/Graphs";
        internal static string LayoutsFolder { get; set; } = "Assets/SceneGraphData/Layouts";

        // ── Model 参照 ──
        private SceneGraphEdges? _currentEdges;
        private SceneGraphLayout? _currentLayout;
        private readonly List<SceneNodeData> _nodes = new();

        // ── 選択状態 ──
        private readonly List<SceneNodeData> _selectedNodes = new();

        // ── バッチスコープ ──
        private int _batchDepth;
        private int _batchGroupIndex;
        private bool _graphChangedPending;

        // ── イベント（View への通知）──
        public event Action? OnGraphChanged;
        public event Action<IReadOnlyList<SceneNodeData>>? OnSelectionChanged;
        public event Action<string>? OnValidationMessage;

        // ── プロパティ ──
        public IReadOnlyList<SceneNodeData> Nodes => _nodes;
        public SceneGraphEdges? CurrentEdges => _currentEdges;
        public SceneGraphLayout? CurrentLayout => _currentLayout;

        /// <summary>選択中のノード（GraphView の選択順）。</summary>
        public IReadOnlyList<SceneNodeData> SelectedNodes => _selectedNodes;

        /// <summary>単一選択時のみ非 null。Inspector の単一編集用。</summary>
        public SceneNodeData? SelectedNode => _selectedNodes.Count == 1 ? _selectedNodes[0] : null;

        /// <summary>
        /// 選択を差し替える。破棄済みオブジェクト（偽 null）は除外する。内容が同じなら何もしない。
        /// </summary>
        public void SetSelection(IReadOnlyList<SceneNodeData>? nodes)
        {
            var filtered = new List<SceneNodeData>();
            if (nodes != null)
            {
                foreach (var n in nodes)
                {
                    // §2.3(d): 偽 null チェックは `== null` で行う（`is null` / ReferenceEquals は使わない）
                    if (n != null) filtered.Add(n);
                }
            }

            if (SelectionSequenceEquals(_selectedNodes, filtered)) return;

            _selectedNodes.Clear();
            _selectedNodes.AddRange(filtered);
            OnSelectionChanged?.Invoke(_selectedNodes);
        }

        private static bool SelectionSequenceEquals(List<SceneNodeData> a, List<SceneNodeData> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        // ─── バッチスコープ ───

        /// <summary>
        /// 一括操作スコープ。Undo を 1 グループへ畳み、OnGraphChanged を Dispose 時に 1 回だけ発火し、
        /// AssetDatabase.SaveAssets() も 1 回に集約する。ネスト可（深さカウント）。
        /// </summary>
        public IDisposable BeginBatch(string undoName)
        {
            if (_batchDepth == 0)
            {
                // §3.1: IncrementCurrentGroup を必ず先に呼ぶ。これが無いと直前の無関係な操作を巻き込む（B8）。
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName(undoName);
                _batchGroupIndex = Undo.GetCurrentGroup();
            }

            _batchDepth++;
            return new BatchScope(this);
        }

        private void EndBatch()
        {
            _batchDepth--;
            if (_batchDepth > 0) return;

            Undo.CollapseUndoOperations(_batchGroupIndex);
            AssetDatabase.SaveAssets();

            if (_graphChangedPending)
            {
                _graphChangedPending = false;
                OnGraphChanged?.Invoke();
            }
        }

        /// <summary>OnGraphChanged の発火。バッチ中は保留する。既存の直接 Invoke はすべてこれに置き換える。</summary>
        private void RaiseGraphChanged()
        {
            if (_batchDepth > 0)
            {
                _graphChangedPending = true;
                return;
            }

            OnGraphChanged?.Invoke();
        }

        private sealed class BatchScope : IDisposable
        {
            private readonly SceneGraphViewModel _owner;
            private bool _disposed;

            public BatchScope(SceneGraphViewModel owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.EndBatch();
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
        /// _nodes と NodesFolder のアセットパスの両方が空くまでループし、
        /// identity == アセットファイル名 を保証した組を返す（B7 対策）。
        /// AssetDatabase.GenerateUniqueAssetPath は "Title 1.asset"（空白入り）を返しうるので使わない。
        /// </summary>
        private bool TryGenerateUniqueIdentity(string baseName, out string identity, out string assetPath)
        {
            for (int i = 0; i <= 100000; i++)
            {
                var candidate = i == 0 ? baseName : $"{baseName}{i}";
                var candidatePath = $"{NodesFolder}/{candidate}.asset";

                var identityTaken = _nodes.Any(n => n != null && n.Identity == candidate);
                var pathTaken = AssetDatabase.AssetPathExists(candidatePath);

                if (!identityTaken && !pathTaken)
                {
                    identity = candidate;
                    assetPath = candidatePath;
                    return true;
                }
            }

            identity = string.Empty;
            assetPath = string.Empty;
            return false;
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

            using (BeginBatch($"Add existing node '{node.Identity}' to graph"))
            {
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

                RaiseGraphChanged();
            }

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

            using (BeginBatch($"Create Node '{identity}'"))
            {
                EnsureDirectoryExists(NodesFolder);

                if (!TryGenerateUniqueIdentity(identity, out var uniqueIdentity, out var path))
                {
                    OnValidationMessage?.Invoke($"Could not generate a unique identity for '{identity}'.");
                    return null;
                }

                var node = ScriptableObject.CreateInstance<SceneNodeData>();
                node.Identity = uniqueIdentity;
                node.name = uniqueIdentity;

                AssetDatabase.CreateAsset(node, path);
                Undo.RegisterCreatedObjectUndo(node, $"Create Node '{uniqueIdentity}'");

                _nodes.Add(node);

                // グラフにノードを登録
                if (_currentEdges != null)
                {
                    Undo.RecordObject(_currentEdges, $"Add node '{uniqueIdentity}' to graph");
                    _currentEdges.AddNode(node);
                    EditorUtility.SetDirty(_currentEdges);
                }

                // レイアウトに初期位置を設定
                if (_currentLayout != null)
                {
                    Undo.RecordObject(_currentLayout, $"Add position for '{uniqueIdentity}'");
                    _currentLayout.SetPosition(node, position);
                    EditorUtility.SetDirty(_currentLayout);
                }

                RaiseGraphChanged();
                return node;
            }
        }

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

            using (BeginBatch($"Create Node '{identity}' from SceneAsset"))
            {
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
                }

                return node;
            }
        }

        // ─── コマンド: ノード削除（グラフからの除外 / 資産の実削除） ───

        /// <summary>
        /// ノードを現在のグラフから除外する。関連エッジとレイアウトも消すが、
        /// アセットファイルは削除しない（＝完全に Undo 可能）。
        /// ノードは複数グラフから共有されるため、グラフからの除外と資産の削除は別操作とする。
        /// </summary>
        public void RemoveNodesFromGraph(IReadOnlyList<SceneNodeData> nodes)
        {
            if (nodes == null || nodes.Count == 0) return;
            var validNodes = nodes.Where(n => n != null).ToList();
            if (validNodes.Count == 0) return;

            using (BeginBatch($"Remove {validNodes.Count} node(s) from graph"))
            {
                if (_currentEdges != null)
                {
                    Undo.RecordObject(_currentEdges, "Remove node(s) from graph");
                    foreach (var node in validNodes)
                    {
                        _currentEdges.RemoveNode(node);
                    }
                    EditorUtility.SetDirty(_currentEdges);
                }

                if (_currentLayout != null)
                {
                    Undo.RecordObject(_currentLayout, "Remove layout entries");
                    foreach (var node in validNodes)
                    {
                        _currentLayout.RemovePosition(node);
                    }
                    EditorUtility.SetDirty(_currentLayout);
                }

                foreach (var node in validNodes)
                {
                    _nodes.Remove(node);
                }

                var remainingSelection = _selectedNodes
                    .Where(n => n != null && !validNodes.Contains(n))
                    .ToList();
                SetSelection(remainingSelection);

                RaiseGraphChanged();
            }
        }

        // ─── コマンド: エッジ接続 ───

        /// <summary>
        /// 親子エッジを作成する。サイクルチェック + 既存親の自動解除を行う。
        /// GraphView がビジュアルエッジを処理するため OnGraphChanged は発火しない。
        /// </summary>
        public bool ConnectEdge(SceneNodeData parent, SceneNodeData child)
        {
            // BeginBatch（this の呼び出し）を挟むとフィールドの null 絞り込みが失われるため、
            // ローカル変数へ捕まえてから使う。
            var currentEdges = _currentEdges;
            if (currentEdges == null) return false;

            // サイクルチェック
            if (currentEdges.WouldCreateCycle(parent, child))
            {
                OnValidationMessage?.Invoke(
                    $"Cannot connect '{parent.Identity}' → '{child.Identity}': would create a cycle.");
                return false;
            }

            using (BeginBatch($"Connect '{parent.Identity}' to '{child.Identity}'"))
            {
                Undo.RecordObject(currentEdges, $"Connect '{parent.Identity}' → '{child.Identity}'");

                // 既存の親があれば削除（ツリー制約: 最大1親）
                currentEdges.RemoveEdgeByChild(child);

                // 新エッジ追加
                currentEdges.AddEdge(parent, child);

                // B10: membership を保証する（未所属ノードに繋ぐと GetRoots 等が壊れる）
                currentEdges.AddNode(parent);
                currentEdges.AddNode(child);

                EditorUtility.SetDirty(currentEdges);

                // ⮿ OnGraphChanged は発火しない（GraphView がビジュアルエッジを追加する）
            }

            return true;
        }

        /// <summary>
        /// children をすべて parent の子にする。個別にサイクル判定し、
        /// 失敗したものは 1 つのメッセージへまとめて報告する（ダイアログ連打を防ぐ）。
        /// ContextMenu 等、GraphView 自身が視覚更新を行わない経路から呼ばれるため OnGraphChanged を発火する。
        /// </summary>
        /// <returns>接続に成功した数。</returns>
        public int ConnectEdges(SceneNodeData parent, IReadOnlyList<SceneNodeData> children)
        {
            if (parent == null || children == null || children.Count == 0) return 0;
            var currentEdges = _currentEdges;
            if (currentEdges == null) return 0;

            var successCount = 0;
            var failedIdentities = new List<string>();

            using (BeginBatch($"Connect {children.Count} node(s) to '{parent.Identity}'"))
            {
                foreach (var child in children)
                {
                    if (child == null) continue;

                    // WouldCreateCycle を 1 件接続するたびに再評価する
                    // （先に繋いだ結果でサイクルになる組み合わせがあるため）
                    if (currentEdges.WouldCreateCycle(parent, child))
                    {
                        failedIdentities.Add(child.Identity);
                        continue;
                    }

                    Undo.RecordObject(currentEdges, $"Connect '{parent.Identity}' → '{child.Identity}'");
                    currentEdges.RemoveEdgeByChild(child);
                    currentEdges.AddEdge(parent, child);
                    currentEdges.AddNode(parent);
                    currentEdges.AddNode(child);
                    EditorUtility.SetDirty(currentEdges);
                    successCount++;
                }

                if (successCount > 0)
                {
                    RaiseGraphChanged();
                }
            }

            if (failedIdentities.Count > 0)
            {
                OnValidationMessage?.Invoke(
                    $"Cannot connect {failedIdentities.Count} node(s) to '{parent.Identity}': would create a cycle.\n" +
                    string.Join(", ", failedIdentities));
            }

            return successCount;
        }

        // ─── コマンド: エッジ切断 ───

        // 単数版の DisconnectEdge は削除した（呼び出し元 0 件 / 未使用 API 分類 C = 置き換え残骸）。
        // 切断は複数版の DisconnectEdges に一本化されている。

        /// <summary>
        /// 指定ノードの親エッジをまとめて切断する。
        /// ContextMenu 等、GraphView 自身が視覚更新を行わない経路から呼ばれるため OnGraphChanged を発火する。
        /// </summary>
        public void DisconnectEdges(IReadOnlyList<SceneNodeData> children)
        {
            if (children == null || children.Count == 0) return;
            var currentEdges = _currentEdges;
            if (currentEdges == null) return;

            var validChildren = children.Where(c => c != null).ToList();
            if (validChildren.Count == 0) return;

            using (BeginBatch($"Disconnect {validChildren.Count} node(s)"))
            {
                Undo.RecordObject(currentEdges, "Disconnect node(s)");
                foreach (var child in validChildren)
                {
                    currentEdges.RemoveEdgeByChild(child);
                }
                EditorUtility.SetDirty(currentEdges);

                RaiseGraphChanged();
            }
        }

        // ─── コマンド: 既存ノードの参照追加 ───

        /// <summary>
        /// 既存ノードを現在のグラフへまとめて追加する（参照ペースト）。
        /// 既に所属しているノードはスキップする。
        /// </summary>
        /// <returns>実際に追加されたノード。</returns>
        public IReadOnlyList<SceneNodeData> AddExistingNodesToGraph(
            IReadOnlyList<(SceneNodeData Node, Vector2 Position)> entries)
        {
            var added = new List<SceneNodeData>();
            if (entries == null || entries.Count == 0) return added;
            var currentEdges = _currentEdges;
            if (currentEdges == null) return added;

            using (BeginBatch($"Add {entries.Count} existing node(s) to graph"))
            {
                foreach (var (node, position) in entries)
                {
                    if (node == null) continue;
                    if (currentEdges.ContainsNode(node)) continue;

                    if (!_nodes.Contains(node)) _nodes.Add(node);

                    Undo.RecordObject(currentEdges, $"Add existing node '{node.Identity}' to graph");
                    currentEdges.AddNode(node);
                    EditorUtility.SetDirty(currentEdges);

                    if (_currentLayout != null)
                    {
                        Undo.RecordObject(_currentLayout, $"Set position for '{node.Identity}'");
                        _currentLayout.SetPosition(node, position);
                        EditorUtility.SetDirty(_currentLayout);
                    }

                    added.Add(node);
                }

                if (added.Count > 0)
                {
                    RaiseGraphChanged();
                }
            }

            return added;
        }

        /// <summary>
        /// ノードを新しいアセットとして複製する。
        /// Identity は一意な新名、LoadType は継承、Payloads は空（§2.3(a) の W-5 対策）。
        /// </summary>
        public SceneNodeData? DuplicateNode(SceneNodeData source, Vector2 position)
        {
            if (source == null) return null;

            using (BeginBatch($"Duplicate '{source.Identity}'"))
            {
                EnsureDirectoryExists(NodesFolder);

                if (!TryGenerateUniqueIdentity(source.Identity, out var identity, out var path))
                {
                    OnValidationMessage?.Invoke($"Could not generate a unique identity for '{source.Identity}'.");
                    return null;
                }

                var node = ScriptableObject.CreateInstance<SceneNodeData>();
                node.Identity = identity;
                node.name = identity;
                node.NodeLoadType = source.NodeLoadType;
                // §2.3(a) W-5: Payloads ごと複製すると OnValidate が Identity を元へ戻し重複 Error になる。
                // そのため複製時は Payloads を必ず空にする。LoadType のみ継承する。
                Debug.Log($"[SceneGraph] Duplicated '{source.Identity}' as '{identity}': payloads were cleared (W-5).");

                AssetDatabase.CreateAsset(node, path);
                Undo.RegisterCreatedObjectUndo(node, $"Duplicate '{identity}'");

                _nodes.Add(node);

                if (_currentEdges != null)
                {
                    Undo.RecordObject(_currentEdges, $"Add node '{identity}' to graph");
                    _currentEdges.AddNode(node);
                    EditorUtility.SetDirty(_currentEdges);
                }

                if (_currentLayout != null)
                {
                    Undo.RecordObject(_currentLayout, $"Add position for '{identity}'");
                    _currentLayout.SetPosition(node, position);
                    EditorUtility.SetDirty(_currentLayout);
                }

                RaiseGraphChanged();
                return node;
            }
        }

        // ─── コマンド: ノード移動 ───

        // 単数版の MoveNode は削除した（呼び出し元 0 件 / 未使用 API 分類 C = 置き換え残骸）。
        // 移動は複数版の MoveNodes に一本化されている。

        /// <summary>複数ノードの位置をまとめて更新する。Undo は 1 グループ。</summary>
        public void MoveNodes(IReadOnlyList<(SceneNodeData Node, Vector2 Position)> moves)
        {
            if (moves == null || moves.Count == 0) return;
            var currentLayout = _currentLayout;
            if (currentLayout == null) return;

            using (BeginBatch($"Move {moves.Count} node(s)"))
            {
                // 同一オブジェクトへの複数記録は無駄なので先頭で 1 回だけ RecordObject する
                Undo.RecordObject(currentLayout, "Move node(s)");
                foreach (var (node, position) in moves)
                {
                    if (node == null) continue;
                    currentLayout.SetPosition(node, position);
                }
                EditorUtility.SetDirty(currentLayout);
            }
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

            // B7: Identity == アセットファイル名 を保つ。IsValidIdentity は _nodes の中しか見ないため、
            // ディスク上に同名ファイルが先にあると RenameAsset だけが失敗して食い違いが残る。
            // 何も書き換える前にここで弾く。
            var assetPath = AssetDatabase.GetAssetPath(node);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var directory = Path.GetDirectoryName(assetPath);
                var targetPath = string.IsNullOrEmpty(directory)
                    ? $"{newIdentity}.asset"
                    : $"{directory.Replace('\\', '/')}/{newIdentity}.asset";

                if (targetPath != assetPath && AssetDatabase.AssetPathExists(targetPath))
                {
                    OnValidationMessage?.Invoke(
                        $"Cannot rename to '{newIdentity}': an asset already exists at '{targetPath}'.");
                    return false;
                }
            }

            using (BeginBatch($"Rename '{node.Identity}' to '{newIdentity}'"))
            {
                Undo.RecordObject(node, $"Rename '{node.Identity}' → '{newIdentity}'");
                node.Identity = newIdentity;
                node.name = newIdentity;
                EditorUtility.SetDirty(node);

                // アセットのリネーム（Undo 非対応。B3: SyncAssetNamesToIdentity で Undo/Redo 後に補正する）
                if (!string.IsNullOrEmpty(assetPath))
                {
                    // RenameAsset は例外を投げず、失敗時にエラー文字列を返す。
                    // 握り潰すと Identity とファイル名が食い違ったまま成功扱いになる（B7 違反）。
                    var error = AssetDatabase.RenameAsset(assetPath, newIdentity);
                    if (!string.IsNullOrEmpty(error))
                    {
                        OnValidationMessage?.Invoke(
                            $"Identity was changed to '{newIdentity}', but the asset file could not be renamed: {error}");
                    }
                }

                RaiseGraphChanged();
            }

            return true;
        }

        /// <summary>
        /// Undo/Redo 後に Identity とアセットファイル名の食い違いを直す（B3 対策）。
        /// AssetDatabase.RenameAsset は Undo 非対応のため、Undo/Redo で node.Identity だけ戻っても
        /// ファイル名は残ったままになる。
        /// </summary>
        public void SyncAssetNamesToIdentity()
        {
            foreach (var node in _nodes)
            {
                if (node == null) continue;

                var path = AssetDatabase.GetAssetPath(node);
                if (string.IsNullOrEmpty(path)) continue;

                var fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName == node.Identity) continue;

                // RenameAsset は例外を投げず失敗時にエラー文字列を返す。握り潰すと
                // Undo/Redo のたびに同じリネームを黙って再試行し続けることになるため、必ず報告する。
                // ここは Undo/Redo 経路なのでダイアログは出さず Console に警告を出す。
                var error = AssetDatabase.RenameAsset(path, node.Identity);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogWarning(
                        $"[SceneGraph] Could not rename '{path}' to match identity '{node.Identity}': {error}. " +
                        "Identity and asset file name are out of sync (B7).");
                }
            }
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
