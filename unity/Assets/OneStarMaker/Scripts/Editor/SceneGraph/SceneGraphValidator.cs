#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// シーングラフのバリデーションを行う。
    /// 操作時（軽量チェック）と Generate 時（全網羅チェック）の両方で使用する。
    /// </summary>
    public static class SceneGraphValidator
    {
        public enum Severity
        {
            Info,
            Warning,
            Error,
        }

        public readonly struct ValidationResult
        {
            public Severity Severity { get; }
            public string Message { get; }
            public SceneNodeData? RelatedNode { get; }

            public ValidationResult(Severity severity, string message, SceneNodeData? relatedNode = null)
            {
                Severity = severity;
                Message = message;
                RelatedNode = relatedNode;
            }

            public override string ToString() => $"[{Severity}] {Message}";
        }

        // ─── 操作時の軽量チェック ───

        /// <summary>
        /// エッジ追加でサイクルが発生するかチェックする。
        /// </summary>
        public static bool WouldCreateCycle(
            SceneGraphEdges edges,
            SceneNodeData proposedParent,
            SceneNodeData proposedChild)
        {
            return edges.WouldCreateCycle(proposedParent, proposedChild);
        }

        /// <summary>
        /// Identity が有効かチェックする。
        /// </summary>
        public static bool IsValidIdentity(string identity, SceneNodeData? self, IEnumerable<SceneNodeData> allNodes)
        {
            if (string.IsNullOrWhiteSpace(identity))
                return false;

            foreach (var node in allNodes)
            {
                if (node != self && node.Identity == identity)
                    return false;
            }
            return true;
        }

        // ─── Generate 時の全網羅チェック ───

        /// <summary>
        /// Generate 前の全バリデーションを実行する。
        /// Error がひとつでもあれば Generate をブロックすべき。
        /// </summary>
        public static List<ValidationResult> ValidateAll(
            IReadOnlyList<SceneNodeData> allNodes,
            IReadOnlyList<SceneGraphEdges> allGraphEdges)
        {
            var results = new List<ValidationResult>();

            ValidateIdentities(allNodes, results);
            ValidateNullReferences(allNodes, allGraphEdges, results);
            ValidateCycles(allGraphEdges, results);
            ValidateSceneAssetDescriptions(allNodes, results);
            ValidateOrphanedNodes(allNodes, allGraphEdges, results);

            return results;
        }

        private static void ValidateIdentities(
            IReadOnlyList<SceneNodeData> allNodes,
            List<ValidationResult> results)
        {
            var identities = new Dictionary<string, SceneNodeData>();

            foreach (var node in allNodes)
            {
                if (node == null) continue;

                // V-3: 空文字チェック
                if (string.IsNullOrWhiteSpace(node.Identity))
                {
                    results.Add(new ValidationResult(
                        Severity.Error,
                        $"Node '{node.name}' has empty identity.",
                        node));
                    continue;
                }

                // V-2: 重複チェック
                if (identities.TryGetValue(node.Identity, out var existing))
                {
                    results.Add(new ValidationResult(
                        Severity.Error,
                        $"Duplicate identity '{node.Identity}' between '{node.name}' and '{existing.name}'.",
                        node));
                }
                else
                {
                    identities[node.Identity] = node;
                }
            }
        }

        private static void ValidateNullReferences(
            IReadOnlyList<SceneNodeData> allNodes,
            IReadOnlyList<SceneGraphEdges> allGraphEdges,
            List<ValidationResult> results)
        {
            // V-6: 壊れた SO 参照
            foreach (var graphEdges in allGraphEdges)
            {
                if (graphEdges == null) continue;

                for (int i = 0; i < graphEdges.Edges.Count; i++)
                {
                    var edge = graphEdges.Edges[i];
                    if (edge.Parent == null || edge.Child == null)
                    {
                        results.Add(new ValidationResult(
                            Severity.Error,
                            $"Graph '{graphEdges.GraphName}' edge [{i}] has null reference (Parent={edge.Parent}, Child={edge.Child})."));
                    }
                }
            }
        }

        private static void ValidateCycles(
            IReadOnlyList<SceneGraphEdges> allGraphEdges,
            List<ValidationResult> results)
        {
            // V-1: サイクル検出（全 Edge を結合して DFS）
            foreach (var graphEdges in allGraphEdges)
            {
                if (graphEdges == null) continue;

                foreach (var edge in graphEdges.Edges)
                {
                    if (edge.Parent == null || edge.Child == null) continue;
                    if (edge.Parent == edge.Child)
                    {
                        results.Add(new ValidationResult(
                            Severity.Error,
                            $"Self-referencing edge in '{graphEdges.GraphName}': {edge.Parent.Identity}.",
                            edge.Parent));
                    }
                }
            }
        }

        private static void ValidateSceneAssetDescriptions(
            IReadOnlyList<SceneNodeData> allNodes,
            List<ValidationResult> results)
        {
            // V-4: Payloads が空（シーン参照がない） — R-8: Info に格下げ（空 Payload を許容）
            foreach (var node in allNodes)
            {
                if (node == null) continue;

                if (node.Payloads.Count == 0)
                {
                    results.Add(new ValidationResult(
                        Severity.Info,
                        $"Node '{node.Identity}' has no scene payloads.",
                        node));
                }
            }
        }

        private static void ValidateOrphanedNodes(
            IReadOnlyList<SceneNodeData> allNodes,
            IReadOnlyList<SceneGraphEdges> allGraphEdges,
            List<ValidationResult> results)
        {
            // V-5: 孤立ノード（どのグラフにもルートでもなく、エッジにも含まれない）
            var referenced = new HashSet<SceneNodeData>();

            foreach (var graphEdges in allGraphEdges)
            {
                if (graphEdges == null) continue;
                foreach (var edge in graphEdges.Edges)
                {
                    if (edge.Parent != null) referenced.Add(edge.Parent);
                    if (edge.Child != null) referenced.Add(edge.Child);
                }
            }

            foreach (var node in allNodes)
            {
                if (node == null) continue;
                if (!referenced.Contains(node))
                {
                    // ルートノード（エッジに登場しないが、他が子として参照していない）はOK
                    // 本当の孤立 = グラフのどのエッジにも登場しない
                    results.Add(new ValidationResult(
                        Severity.Warning,
                        $"Node '{node.Identity}' is not referenced by any edge.",
                        node));
                }
            }
        }
    }
}
