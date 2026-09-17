#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Build.Selection;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>季節ごとの見た目を、完成版・仮素材版・両方から選ぶ。</summary>
    public enum SeasonContentMode { Full, Whitebox, FullAndWhitebox }

    /// <summary>選択結果と、その選択で満たすべきコンテンツ群を一緒に返す。</summary>
    public sealed class SeasonSelectionResult
    {
        public SeasonSelectionResult(BuildPlanResult selection, IReadOnlyList<BuildContentRequirement> requirements)
        {
            Selection = selection;
            Requirements = requirements;
        }

        public BuildPlanResult Selection { get; }
        public IReadOnlyList<BuildContentRequirement> Requirements { get; }
    }

    /// <summary>SceneResource の親子関係と内容の有無を、Unity オブジェクトから切り離して保持する。</summary>
    public sealed class SeasonSceneNode
    {
        public SeasonSceneNode(string identity, string? parent, IEnumerable<string> children, bool hasPayload)
        {
            Identity = identity;
            Parent = parent;
            Children = Array.AsReadOnly(children.ToArray());
            HasPayload = hasPayload;
        }
        public string Identity { get; }
        public string? Parent { get; }
        public IReadOnlyList<string> Children { get; }
        public bool HasPayload { get; }
    }

    /// <summary>SampleGame のシーン階層から季節を判定し、ビルド対象と必須条件を決める。</summary>
    public sealed class SeasonSceneSelectionPolicy
    {
        private static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Winter" };
        private static readonly string[] Roots = { "InGameScene", "OutGameScene" };

        /// <summary>選択計画だけが必要な呼び出し向けの入口。</summary>
        public BuildPlanResult Select(IEnumerable<SeasonSceneNode> nodes,
            IEnumerable<BuildContentCandidate> candidates, IEnumerable<IBuildTagProvider> sourceProviders,
            IEnumerable<string> selectedSeasons, SeasonContentMode mode,
            IReadOnlyDictionary<string, string>? seasonOverrides = null)
            => SelectDetailed(nodes, candidates, sourceProviders, selectedSeasons, mode, seasonOverrides).Selection;

        /// <summary>選択計画に加え、診断表示に使う必須コンテンツ群も返す。</summary>
        public SeasonSelectionResult SelectDetailed(IEnumerable<SeasonSceneNode> nodes,
            IEnumerable<BuildContentCandidate> candidates, IEnumerable<IBuildTagProvider> sourceProviders,
            IEnumerable<string> selectedSeasons, SeasonContentMode mode,
            IReadOnlyDictionary<string, string>? seasonOverrides = null)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (sourceProviders == null) throw new ArgumentNullException(nameof(sourceProviders));
            if (selectedSeasons == null) throw new ArgumentNullException(nameof(selectedSeasons));
            var graph = ValidateGraph(nodes);
            var chosen = selectedSeasons.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (chosen.Length == 0 || chosen.Any(x => !Seasons.Contains(x, StringComparer.Ordinal)))
                throw new ArgumentException("At least one known season is required.", nameof(selectedSeasons));
            if (!Enum.IsDefined(typeof(SeasonContentMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));

            var candidateSnapshot = candidates.ToArray();
            var providerSnapshot = sourceProviders.ToArray();
            var byLogical = candidateSnapshot.GroupBy(x => x.LogicalKey, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
            // 宣言された内容と生成候補が食い違うと、必須シーンが静かに欠落するため先に止める。
            foreach (var node in graph.Values)
            {
                var hasCandidate = byLogical.TryGetValue(node.Identity, out var values) && values.Length != 0;
                if (node.HasPayload != hasCandidate)
                    throw new InvalidOperationException("Payload/candidate mismatch: " + node.Identity);
            }
            foreach (var key in byLogical.Keys)
                if (!graph.ContainsKey(key)) throw new InvalidOperationException("Candidate without graph node: " + key);

            var memberships = graph.Values.ToDictionary(x => x.Identity,
                x => FindSeason(x, graph), StringComparer.Ordinal);
            // 親子階層だけで表せない所属は明示入力に限定し、シーン名の推測で補わない。
            if (seasonOverrides != null)
                foreach (var pair in seasonOverrides)
                {
                    if (!graph.ContainsKey(pair.Key) || !Seasons.Contains(pair.Value, StringComparer.Ordinal))
                        throw new InvalidOperationException("Invalid season override: " + pair.Key);
                    memberships[pair.Key] = pair.Value;
                }
            var projectTags = new Dictionary<string, IReadOnlyList<BuildTag>>(StringComparer.Ordinal);
            var requirements = new List<BuildContentRequirement>();
            foreach (var node in graph.Values.OrderBy(x => x.Identity, StringComparer.Ordinal))
            {
                if (!byLogical.TryGetValue(node.Identity, out var nodeCandidates)) continue;
                var season = memberships[node.Identity];
                var inScope = season == null || chosen.Contains(season, StringComparer.Ordinal);
                var representationsForGroup = nodeCandidates
                    .Select(x => Representation(x, providerSnapshot)).Distinct(StringComparer.Ordinal).ToArray();
                var hasBothRepresentations = representationsForGroup.Contains("Full", StringComparer.Ordinal) &&
                    representationsForGroup.Contains("Whitebox", StringComparer.Ordinal);
                // 両表現を同梱する群だけ複数選択を許す。Full が二候補あるだけなら ExactlyOne のまま。
                if (inScope)
                    requirements.Add(new BuildContentRequirement(node.Identity,
                        mode == SeasonContentMode.FullAndWhitebox && season != null && hasBothRepresentations
                            ? BuildContentCardinality.OneOrMore : BuildContentCardinality.ExactlyOne));

                var switchable = season != null && representationsForGroup.Contains("Whitebox", StringComparer.Ordinal);
                foreach (var candidate in nodeCandidates)
                {
                    var tags = new List<BuildTag>();
                    if (season != null)
                    {
                        tags.Add(new BuildTag("Season", season));
                        // Full のみで提供される補助シーンは、どちらの見た目でも共通して必要。
                        if (switchable)
                            tags.Add(new BuildTag("SeasonalMode", Representation(candidate, providerSnapshot)));
                    }
                    projectTags.Add(candidate.StableKey, tags);
                }
            }

            // Representation は候補自体の種類、SeasonalMode は季節内の切替対象だけに効く。
            // 仮素材版でも共通シーンや Full 専用の補助シーンを残すため、二つを分けて指定する。
            var representations = mode == SeasonContentMode.Full
                ? new[] { "Full" } : new[] { "Full", "Whitebox" };
            var modes = mode == SeasonContentMode.Full ? new[] { "Full" }
                : mode == SeasonContentMode.Whitebox ? new[] { "Whitebox" }
                : new[] { "Full", "Whitebox" };
            var request = new BuildRequest(chosen.Select(x => new BuildTag("Season", x))
                .Concat(representations.Select(x => new BuildTag("Representation", x)))
                .Concat(modes.Select(x => new BuildTag("SeasonalMode", x))));
            var schema = new BuildTagSchema(new[]
            {
                Dimension("Season", Seasons), Dimension("Representation", new[] { "Full", "Whitebox" }),
                Dimension("SeasonalMode", new[] { "Full", "Whitebox" })
            });
            var providers = providerSnapshot.Concat(new IBuildTagProvider[] { new ProjectTags(projectTags) });
            var result = new BuildTagSelector().Select(request, candidateSnapshot, providers,
                new BuildSelectionPolicy(schema, requirements));
            // OneOrMore だけでは両表現の採用を保証できないため、同梱時は個別に照合する。
            if (result.IsSuccess && mode == SeasonContentMode.FullAndWhitebox)
                VerifyDual(result.Plan!, byLogical, memberships, chosen, providerSnapshot);
            return new SeasonSelectionResult(result, Array.AsReadOnly(requirements.ToArray()));
        }

        private static KeyValuePair<string, IEnumerable<string>> Dimension(string key, IEnumerable<string> values) =>
            new(key, values);

        private static string Representation(BuildContentCandidate candidate, IEnumerable<IBuildTagProvider> providers)
        {
            var value = providers.SelectMany(x => x.GetTags(candidate))
                .Single(x => x.Dimension == "Representation").Value;
            if (value != "Full" && value != "Whitebox")
                throw new InvalidOperationException("Unknown representation: " + candidate.StableKey);
            return value;
        }

        private static void VerifyDual(BuildPlan plan,
            IReadOnlyDictionary<string, BuildContentCandidate[]> byLogical,
            IReadOnlyDictionary<string, string?> memberships, IReadOnlyList<string> chosen,
            IEnumerable<IBuildTagProvider> providers)
        {
            var selected = new HashSet<string>(plan.SelectedContent.Select(x => x.StableKey), StringComparer.Ordinal);
            foreach (var pair in byLogical)
            {
                var season = memberships[pair.Key];
                if (season == null || !chosen.Contains(season, StringComparer.Ordinal)) continue;
                var available = pair.Value.Select(x => Representation(x, providers)).Distinct(StringComparer.Ordinal).ToArray();
                if (!available.Contains("Whitebox", StringComparer.Ordinal)) continue;
                foreach (var candidate in pair.Value)
                    if (!selected.Contains(candidate.StableKey))
                        throw new InvalidOperationException("Dual mode omitted candidate: " + candidate.StableKey);
            }
        }

        private static Dictionary<string, SeasonSceneNode> ValidateGraph(IEnumerable<SeasonSceneNode> nodes)
        {
            // 親子を相互に検査してから所属をたどる。片側だけのリンクや循環を季節判定へ渡さない。
            var graph = new Dictionary<string, SeasonSceneNode>(StringComparer.Ordinal);
            foreach (var node in nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Identity) ||
                    !graph.TryAdd(node.Identity, node))
                    throw new InvalidOperationException("Invalid or duplicate scene identity.");
            }
            foreach (var root in Roots)
                if (!graph.ContainsKey(root)) throw new InvalidOperationException("Missing scene root: " + root);
            foreach (var node in graph.Values)
            {
                if (node.Parent == null)
                {
                    if (!Roots.Contains(node.Identity, StringComparer.Ordinal))
                        throw new InvalidOperationException("Orphan scene: " + node.Identity);
                }
                else
                {
                    if (!graph.TryGetValue(node.Parent, out var parent) ||
                        parent.Children.Count(x => x == node.Identity) != 1)
                        throw new InvalidOperationException("Invalid parent link: " + node.Identity);
                }
                foreach (var child in node.Children)
                    if (!graph.TryGetValue(child, out var value) || value.Parent != node.Identity ||
                        node.Children.Count(x => x == child) != 1)
                        throw new InvalidOperationException("Invalid child link: " + node.Identity);
            }
            foreach (var node in graph.Values) FindSeason(node, graph);
            return graph;
        }

        private static string? FindSeason(SeasonSceneNode node, IReadOnlyDictionary<string, SeasonSceneNode> graph)
        {
            // 最も近い季節名を拾うだけでは、異なる季節の入れ子を見逃すため祖先を最後まで調べる。
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string? season = null;
            for (var cursor = node; cursor != null; cursor = cursor.Parent == null ? null : graph[cursor.Parent])
            {
                if (!visited.Add(cursor.Identity)) throw new InvalidOperationException("Scene parent cycle: " + node.Identity);
                foreach (var value in Seasons)
                    if (cursor.Identity == "Season_" + value)
                    {
                        if (season != null && season != value)
                            throw new InvalidOperationException("Multiple season membership: " + node.Identity);
                        season = value;
                    }
            }
            return season;
        }

        private sealed class ProjectTags : IBuildTagProvider
        {
            private readonly IReadOnlyDictionary<string, IReadOnlyList<BuildTag>> _tags;
            public ProjectTags(IReadOnlyDictionary<string, IReadOnlyList<BuildTag>> tags) => _tags = tags;
            public string StableProviderKey => "samplegame.season-selection.v1";
            public IReadOnlyList<BuildTag> GetTags(BuildContentCandidate candidate) =>
                _tags.TryGetValue(candidate.StableKey, out var tags) ? tags : Array.Empty<BuildTag>();
        }
    }
}
