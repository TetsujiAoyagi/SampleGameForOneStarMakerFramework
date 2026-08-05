#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// クリップボード JSON の組み立てと貼り付けを行う。View 層の UI 要素に依存しない。
    /// </summary>
    internal sealed class SceneGraphPasteService
    {
        private readonly SceneGraphViewModel _viewModel;

        public SceneGraphPasteService(SceneGraphViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        /// <summary>コピー対象のノードからクリップボード JSON を組み立てる。</summary>
        public string BuildClipboardJson(IReadOnlyList<SceneNodeData> nodes)
        {
            if (nodes.Count == 0) return string.Empty;

            var data = new SceneGraphClipboardData();
            var guidByNode = new Dictionary<SceneNodeData, string>();

            foreach (var node in nodes)
            {
                var guid = GetGuidForNode(node);
                guidByNode[node] = guid;

                // §2.3(d): ?. と ?? は Unity の == オーバーロードを迂回するため、破棄済み SO に対して
                // 短絡せず呼び出してしまう。偽 null を検出できる != null で明示的に判定する。
                var layout = _viewModel.CurrentLayout;
                var position = layout != null ? layout.GetPosition(node) : Vector2.zero;

                data.Nodes.Add(new SceneGraphClipboardEntry
                {
                    NodeGuid = guid,
                    Identity = node.Identity,
                    LoadType = (int)node.NodeLoadType,
                    Position = position,
                });
            }

            var currentEdgesForCopy = _viewModel.CurrentEdges;
            if (currentEdgesForCopy != null)
            {
                var nodeGuids = nodes.Select(n => guidByNode[n]).ToList();
                var allEdgeTuples = new List<(string ParentGuid, string ChildGuid)>();

                foreach (var edge in currentEdgesForCopy.Edges)
                {
                    if (edge.Parent == null || edge.Child == null) continue;
                    allEdgeTuples.Add((GetGuidForNode(edge.Parent), GetGuidForNode(edge.Child)));
                }

                data.Edges = SceneGraphClipboard.BuildInternalLinks(nodeGuids, allEdgeTuples);
            }

            var currentGraphPath = currentEdgesForCopy != null
                ? AssetDatabase.GetAssetPath(currentEdgesForCopy)
                : string.Empty;
            data.SourceGraphGuid = string.IsNullOrEmpty(currentGraphPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(currentGraphPath);

            return SceneGraphClipboard.Serialize(data);
        }

        private static string GetGuidForNode(SceneNodeData node)
        {
            var path = AssetDatabase.GetAssetPath(node);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        /// <summary>
        /// クリップボード JSON を貼り付ける。
        /// 選択の復元は呼び出し側（View）の責務なので、結果ノードを返すだけにする。
        /// §3.7.1: 解決できたノードのうち 1 つでも現在のグラフに未所属なら参照ペースト、
        /// 全ノードが既に所属していれば複製。forceDuplicate（Ctrl+D）は常に複製。
        /// 貼り付け位置は元座標 +(40,40) の一律オフセット。
        /// </summary>
        public IReadOnlyList<SceneNodeData> ApplyPaste(string json, bool forceDuplicate)
        {
            var currentEdges = _viewModel.CurrentEdges;
            if (currentEdges == null) return Array.Empty<SceneNodeData>();

            var clipboardData = SceneGraphClipboard.TryDeserialize(json);
            if (clipboardData == null || !SceneGraphClipboard.CanPaste(clipboardData))
                return Array.Empty<SceneNodeData>();

            var resolved = new List<SceneNodeData?>();
            foreach (var entry in clipboardData.Nodes)
            {
                var path = string.IsNullOrEmpty(entry.NodeGuid)
                    ? string.Empty
                    : AssetDatabase.GUIDToAssetPath(entry.NodeGuid);
                var node = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneNodeData>(path);

                if (node == null)
                {
                    Debug.LogWarning(
                        $"[SceneGraph] Paste: could not resolve node for GUID '{entry.NodeGuid}' " +
                        $"(was '{entry.Identity}'). Skipping this entry.");
                }

                resolved.Add(node);
            }

            if (resolved.All(n => n == null)) return Array.Empty<SceneNodeData>();

            var allAlreadyInCurrentGraph = resolved
                .Where(n => n != null)
                .All(n => currentEdges.ContainsNode(n!));

            var duplicate = forceDuplicate || allAlreadyInCurrentGraph;

            var offset = new Vector2(40f, 40f);
            var resultByIndex = new SceneNodeData?[resolved.Count];
            var validCount = resolved.Count(n => n != null);
            var undoName = duplicate ? $"Duplicate {validCount} node(s)" : $"Paste {validCount} node(s)";

            // R5: 参照ペーストで既存ノードの親子関係が黙って差し替わったケースを報告する
            var reparentedIdentities = new List<string>();

            using (_viewModel.BeginBatch(undoName))
            {
                if (duplicate)
                {
                    for (int i = 0; i < resolved.Count; i++)
                    {
                        var src = resolved[i];
                        if (src == null) continue;
                        var pos = clipboardData.Nodes[i].Position + offset;
                        resultByIndex[i] = _viewModel.DuplicateNode(src, pos);
                    }
                }
                else
                {
                    var entries = new List<(SceneNodeData Node, Vector2 Position)>();
                    for (int i = 0; i < resolved.Count; i++)
                    {
                        var src = resolved[i];
                        if (src == null) continue;
                        resultByIndex[i] = src;
                        entries.Add((src, clipboardData.Nodes[i].Position + offset));
                    }

                    _viewModel.AddExistingNodesToGraph(entries);
                }

                foreach (var link in clipboardData.Edges)
                {
                    if (link.ParentIndex < 0 || link.ParentIndex >= resultByIndex.Length) continue;
                    if (link.ChildIndex < 0 || link.ChildIndex >= resultByIndex.Length) continue;

                    var parent = resultByIndex[link.ParentIndex];
                    var child = resultByIndex[link.ChildIndex];
                    if (parent == null || child == null) continue;

                    if (!duplicate)
                    {
                        // 複製されたノードは常に新規アセットで既存の親を持ち得ないため、
                        // 参照ペースト（既存ノードの再利用）のときだけ判定すればよい。
                        var existingParent = currentEdges.GetParent(child);
                        if (existingParent != null && existingParent != parent)
                        {
                            reparentedIdentities.Add(child.Identity);
                        }
                    }

                    _viewModel.ConnectEdges(parent, new[] { child });
                }

                // F2: 複製時、コピー集合に含まれない親を持つノードは複製先も同じ親へ繋ぐ。
                // 親がコピー集合内なら上記 Edges ループが複製同士を繋ぐので、ここでは触らない。
                // 参照ペーストでは貼り付け先に同じ親がいるとは限らないため適用しない。
                if (duplicate)
                {
                    var indicesWithoutInternalParent = SceneGraphClipboard.GetIndicesWithoutInternalParent(
                        resolved.Count, clipboardData.Edges);

                    foreach (var i in indicesWithoutInternalParent)
                    {
                        var src = resolved[i];
                        if (src == null) continue;

                        var dup = resultByIndex[i];
                        if (dup == null) continue;

                        // §2.3(d): 破棄済み SO は == null が true。?. / ?? は使わない。
                        var externalParent = currentEdges.GetParent(src);
                        if (externalParent == null) continue;

                        _viewModel.ConnectEdges(externalParent, new[] { dup });
                    }
                }
            }

            if (reparentedIdentities.Count > 0)
            {
                Debug.LogWarning(
                    $"[SceneGraph] Paste: re-parented {reparentedIdentities.Count} existing node(s): " +
                    string.Join(", ", reparentedIdentities));
            }

            return resultByIndex.Where(n => n != null).Cast<SceneNodeData>().ToList();
        }
    }
}
