#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// 宣言（root・filter・表現）から、graph のどの node が必要かを決定的に導き、
    /// (logical key, 表現) ごとの requirement と到達経路を作って <see cref="BuildTagSelector"/> に渡す pure core。
    /// <para>
    /// 到達規則: required(n) ⇔ n ∈ roots ∨ (required(parent(n)) ∧ (Load(n) ≠ OnDemand ∨ filter(tags(n)))。
    /// filter の意味は selector の request と同じ（dimension 内 OR、dimension 間 AND、tag の無い dimension は中立、
    /// 要求されていない dimension の tag は拒否）で、述語の実装は selector と共有する。
    /// </para>
    /// <para>
    /// graph は identity の重複・root の存在・orphan・親子リンクの相互一致（多親の拒否）に加えて、
    /// 季節などの語彙に依存しない明示的なサイクル検査を通す。到達しないサイクルの島も issue で止め、Unreachable で通さない。
    /// Scene・AssetDatabase・Unity API は読まない。
    /// </para>
    /// </summary>
    public sealed class BuildPurposeResolver
    {
        public BuildPurposeResolution Resolve(
            BuildPurposeDeclaration declaration,
            IEnumerable<BuildGraphNode> graph,
            IEnumerable<BuildContentCandidate> candidates,
            IEnumerable<IBuildTagProvider> candidateTagProviders,
            IBuildNodeTagProvider nodeTagProvider)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (candidateTagProviders == null) throw new ArgumentNullException(nameof(candidateTagProviders));
            if (nodeTagProvider == null) throw new ArgumentNullException(nameof(nodeTagProvider));
            if (nodeTagProvider.Schema.ContainsDimension(BuildRepresentationSelection.Dimension))
                throw new ArgumentException("The node tag schema must not declare the representation dimension; the resolver adds it.", nameof(nodeTagProvider));

            var issues = new List<BuildValidationIssue>();
            var candidateSnapshot = candidates.Select(candidate => candidate
                    ?? throw new ArgumentException("Candidates cannot contain null.", nameof(candidates)))
                .OrderBy(candidate => candidate.StableKey, StringComparer.Ordinal)
                .ToArray();
            var providerSnapshot = candidateTagProviders.Select(provider => provider
                    ?? throw new ArgumentException("Providers cannot contain null.", nameof(candidateTagProviders)))
                .OrderBy(provider => provider.StableProviderKey, StringComparer.Ordinal)
                .ToArray();
            var request = BuildSelectorRequest(declaration);

            // 入力順によらず同じ結果になるよう、node は identity 順で持つ。
            var nodes = ValidateGraph(declaration, graph, issues);
            if (nodes == null || issues.Any(issue => issue.Severity == BuildValidationSeverity.Error))
                return new BuildPurposeResolution(new BuildPlanResult(null, issues), Array.Empty<BuildPurposeRequirement>(), request);

            var nodeTags = AttributeNodes(nodes, nodeTagProvider, issues);
            var filter = BuildTagSelector.ValidateRequest(declaration.Filter, nodeTagProvider.Schema, issues, allowEmpty: true);
            if (issues.Any(issue => issue.Severity == BuildValidationSeverity.Error))
                return new BuildPurposeResolution(new BuildPlanResult(null, issues), Array.Empty<BuildPurposeRequirement>(), request);

            var paths = Reach(declaration, nodes, nodeTags, filter);
            var requirements = DeriveRequirements(declaration, nodes, paths, issues);
            if (issues.Any(issue => issue.Severity == BuildValidationSeverity.Error))
                return new BuildPurposeResolution(new BuildPlanResult(null, issues), Array.Empty<BuildPurposeRequirement>(), request);

            var schema = ComposeSchema(declaration, nodeTagProvider.Schema, nodes, candidateSnapshot, providerSnapshot);
            var policy = new BuildSelectionPolicy(schema, requirements.Select(item => item.ToContentRequirement()),
                excludeUnrequired: true, representationDimension: BuildRepresentationSelection.Dimension);
            var selection = new BuildTagSelector().Select(request, candidateSnapshot, providerSnapshot, policy);
            // node 水準の検査で出た Warning（重複 tag など）も、同じ結果面で読めるように合流させる。
            var merged = issues.Count == 0 ? selection : new BuildPlanResult(selection.Plan, selection.Issues.Concat(issues));
            return new BuildPurposeResolution(merged, requirements, request);
        }

        private static BuildRequest BuildSelectorRequest(BuildPurposeDeclaration declaration) =>
            new BuildRequest(declaration.Filter.Selections.Concat(
                declaration.Representation.Preference.Select(value => new BuildTag(BuildRepresentationSelection.Dimension, value))));

        private static SortedDictionary<string, BuildGraphNode>? ValidateGraph(
            BuildPurposeDeclaration declaration,
            IEnumerable<BuildGraphNode> graph,
            ICollection<BuildValidationIssue> issues)
        {
            var nodes = new SortedDictionary<string, BuildGraphNode>(StringComparer.Ordinal);
            foreach (var node in graph)
            {
                if (node == null) throw new ArgumentException("Graph cannot contain null.", nameof(graph));
                if (nodes.ContainsKey(node.Identity))
                    AddError(issues, BuildValidationCode.GraphDuplicateNode, BuildValidationSubject.Node, node.Identity, "Node identity is declared more than once.");
                else
                    nodes.Add(node.Identity, node);
            }
            foreach (var root in declaration.Roots)
                if (!nodes.ContainsKey(root))
                    AddError(issues, BuildValidationCode.GraphMissingRoot, BuildValidationSubject.Node, root, "Declared root is not in the graph.");
            if (issues.Any(issue => issue.Severity == BuildValidationSeverity.Error)) return null;

            // 片側だけのリンク・多親・root でない親無し node を、到達規則へ渡す前に止める。
            var linksValid = true;
            foreach (var node in nodes.Values)
            {
                if (node.Parent == null)
                {
                    if (!declaration.Roots.Contains(node.Identity, StringComparer.Ordinal))
                    {
                        AddError(issues, BuildValidationCode.GraphOrphan, BuildValidationSubject.Node, node.Identity, "Node has no parent and is not a declared root.");
                        linksValid = false;
                    }
                }
                else if (!nodes.TryGetValue(node.Parent, out var parent) || parent.Children.Count(child => string.Equals(child, node.Identity, StringComparison.Ordinal)) != 1)
                {
                    AddError(issues, BuildValidationCode.GraphLinkMismatch, BuildValidationSubject.Node, node.Identity, "Parent does not list this node exactly once as a child.");
                    linksValid = false;
                }
                foreach (var child in node.Children)
                {
                    if (!nodes.TryGetValue(child, out var childNode) || !string.Equals(childNode.Parent, node.Identity, StringComparison.Ordinal)
                        || node.Children.Count(item => string.Equals(item, child, StringComparison.Ordinal)) != 1)
                    {
                        AddError(issues, BuildValidationCode.GraphLinkMismatch, BuildValidationSubject.Node, node.Identity, "Child does not name this node as its parent exactly once: " + child);
                        linksValid = false;
                    }
                }
            }
            if (!linksValid) return null;

            // 季節判定の祖先走査に紛れていたサイクル検査を、語彙に依存しない形でここに置く。
            // 相互一致したリンクでも、root から切り離された island は親の鎖が閉じるので、全 node から祖先をたどって見つける。
            foreach (var node in nodes.Values)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                for (var cursor = node; cursor != null; cursor = cursor.Parent == null ? null : nodes[cursor.Parent])
                {
                    if (visited.Add(cursor.Identity)) continue;
                    AddError(issues, BuildValidationCode.GraphCycle, BuildValidationSubject.Node, node.Identity, "Parent chain forms a cycle.");
                    break;
                }
            }
            return issues.Any(issue => issue.Severity == BuildValidationSeverity.Error) ? null : nodes;
        }

        private static Dictionary<string, IReadOnlyList<BuildTag>> AttributeNodes(
            IReadOnlyDictionary<string, BuildGraphNode> nodes,
            IBuildNodeTagProvider provider,
            ICollection<BuildValidationIssue> issues)
        {
            var result = new Dictionary<string, IReadOnlyList<BuildTag>>(StringComparer.Ordinal);
            var schema = provider.Schema;
            foreach (var node in nodes.Values)
            {
                var returned = provider.GetTags(node, nodes) ?? throw new InvalidOperationException("A node tag provider returned null.");
                var valid = new List<BuildTag>();
                foreach (var tag in returned)
                {
                    if (tag == null || !IsStable(tag.Dimension))
                        AddError(issues, BuildValidationCode.UnknownDimension, BuildValidationSubject.Node, node.Identity, "Node dimension is null, empty, or has surrounding whitespace.", tag?.Dimension, tag?.Value, provider.StableProviderKey);
                    else if (!schema.ContainsDimension(tag.Dimension!))
                        AddError(issues, BuildValidationCode.UnknownDimension, BuildValidationSubject.Node, node.Identity, "Node dimension is not in the schema.", tag.Dimension, tag.Value, provider.StableProviderKey);
                    else if (!IsStable(tag.Value) || !schema.ContainsValue(tag.Dimension!, tag.Value!))
                        AddError(issues, BuildValidationCode.UnknownValue, BuildValidationSubject.Node, node.Identity, "Node value is invalid or not in the schema.", tag.Dimension, tag.Value, provider.StableProviderKey);
                    else
                        valid.Add(new BuildTag(tag.Dimension, tag.Value));
                }
                foreach (var dimension in valid.GroupBy(tag => tag.Dimension!, StringComparer.Ordinal))
                {
                    if (dimension.Select(tag => tag.Value).Distinct(StringComparer.Ordinal).Count() > 1)
                        AddError(issues, BuildValidationCode.ConflictingCandidateValues, BuildValidationSubject.Node, node.Identity, "Node has different values for the same dimension.", dimension.Key, null, provider.StableProviderKey);
                }
                result[node.Identity] = valid.Distinct()
                    .OrderBy(tag => tag.Dimension, StringComparer.Ordinal)
                    .ThenBy(tag => tag.Value, StringComparer.Ordinal)
                    .ToArray();
            }
            return result;
        }

        /// <summary>root から到達する node と、その到達経路（root → node の identity の列）。</summary>
        private static SortedDictionary<string, IReadOnlyList<string>> Reach(
            BuildPurposeDeclaration declaration,
            IReadOnlyDictionary<string, BuildGraphNode> nodes,
            IReadOnlyDictionary<string, IReadOnlyList<BuildTag>> nodeTags,
            IReadOnlyDictionary<string, HashSet<string>> filter)
        {
            var paths = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            foreach (var root in declaration.Roots)
            {
                if (paths.ContainsKey(root)) continue;
                paths[root] = new[] { root };
                queue.Enqueue(root);
            }
            while (queue.Count != 0)
            {
                var current = nodes[queue.Dequeue()];
                var path = paths[current.Identity];
                foreach (var childIdentity in current.Children.OrderBy(child => child, StringComparer.Ordinal))
                {
                    // 多親は検証で拒否済みなので、各 node は 1 つの経路でしか到達しない。root が別の root の子孫でも経路は最初の 1 つを保つ。
                    if (paths.ContainsKey(childIdentity)) continue;
                    var child = nodes[childIdentity];
                    var required = child.Load != BuildGraphLoad.OnDemand
                        || BuildTagSelector.FindRejection(nodeTags[childIdentity], filter) == null;
                    if (!required) continue;
                    paths[childIdentity] = path.Concat(new[] { childIdentity }).ToArray();
                    queue.Enqueue(childIdentity);
                }
            }
            return paths;
        }

        private static List<BuildPurposeRequirement> DeriveRequirements(
            BuildPurposeDeclaration declaration,
            IReadOnlyDictionary<string, BuildGraphNode> nodes,
            IReadOnlyDictionary<string, IReadOnlyList<string>> paths,
            ICollection<BuildValidationIssue> issues)
        {
            var requirements = new List<BuildPurposeRequirement>();
            var representation = declaration.Representation;
            foreach (var pair in paths)
            {
                var node = nodes[pair.Key];
                if (node.SceneRepresentations.Count != 0)
                {
                    // 期待集合は node の宣言済み payload から作る。候補が欠けていても「存在しない表現」として合格させない
                    // （存在と一意性は selector が materialize 済み候補で検査する）。
                    var declared = representation.Preference.Where(value => node.SceneRepresentations.Contains(value, StringComparer.Ordinal));
                    var chosen = representation.Mode == BuildRepresentationMode.First ? declared.Take(1).ToArray() : declared.ToArray();
                    if (chosen.Length == 0)
                        AddError(issues, BuildValidationCode.RequiredGroupMissing, BuildValidationSubject.Requirement, node.Identity,
                            "Reached node declares no representation in the preference.", BuildRepresentationSelection.Dimension, null, null);
                    foreach (var value in chosen)
                        requirements.Add(new BuildPurposeRequirement(node.Identity, value, pair.Value));
                }
                // 付随 asset は宣言済みの表現をそのまま要求する。表現の fallback は Runtime の読み口が担う。
                foreach (var asset in node.AttachedAssets)
                    foreach (var value in asset.Representations)
                        requirements.Add(new BuildPurposeRequirement(node.Identity + ":" + asset.Name, value, pair.Value));
            }
            return requirements;
        }

        /// <summary>
        /// node 水準の schema に表現の dimension を足す。優先順に無い表現の候補も「未知の値」ではなく
        /// RepresentationNotPreferred として記録されるよう、宣言・node・候補のどこかに現れる表現を全て許可する。
        /// </summary>
        private static BuildTagSchema ComposeSchema(
            BuildPurposeDeclaration declaration,
            BuildTagSchema nodeSchema,
            IReadOnlyDictionary<string, BuildGraphNode> nodes,
            IReadOnlyList<BuildContentCandidate> candidates,
            IReadOnlyList<IBuildTagProvider> providers)
        {
            var values = new HashSet<string>(declaration.Representation.Preference, StringComparer.Ordinal);
            foreach (var node in nodes.Values)
            {
                values.UnionWith(node.SceneRepresentations);
                foreach (var asset in node.AttachedAssets) values.UnionWith(asset.Representations);
            }
            foreach (var candidate in candidates)
                foreach (var provider in providers)
                    foreach (var tag in provider.GetTags(candidate) ?? Array.Empty<BuildTag>())
                        if (tag != null && string.Equals(tag.Dimension, BuildRepresentationSelection.Dimension, StringComparison.Ordinal) && IsStable(tag.Value))
                            values.Add(tag.Value!);
            var dimensions = nodeSchema.Dimensions
                .Select(pair => new KeyValuePair<string, IEnumerable<string>>(pair.Key, pair.Value))
                .Concat(new[] { new KeyValuePair<string, IEnumerable<string>>(BuildRepresentationSelection.Dimension, values) });
            return new BuildTagSchema(dimensions);
        }

        private static bool IsStable(string? value) =>
            !string.IsNullOrEmpty(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal);

        private static void AddError(ICollection<BuildValidationIssue> issues, BuildValidationCode code,
            BuildValidationSubject subject, string subjectKey, string message,
            string? dimension = null, string? value = null, string? providerKey = null) =>
            issues.Add(new BuildValidationIssue(BuildValidationSeverity.Error, code, subject, subjectKey, dimension, value, providerKey, message));
    }
}
