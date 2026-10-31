#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Build.Selection;
using SampleGame.InGame.Streaming;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// SampleGame の語彙を node 水準の tag にする。Framework の選択 core は季節名も職種も知らないので、
    /// 宣言の filter が参照する dimension（<c>Season</c>・<c>Role</c>）はここだけが付ける。
    /// <list type="bullet">
    /// <item><description><c>Season</c>: 祖先（自分を含む）に <c>Season_{季節}</c> があればその季節。2 つの季節の入れ子は宣言の誤りとして止める。</description></item>
    /// <item><description><c>Role</c>: Runtime の companion の分類（<see cref="CellCompanionRoleClassifier"/>）と同じ規則。規則を複製せず、Runtime の判定をそのまま使う。</description></item>
    /// </list>
    /// 表現（<c>Representation</c>）は候補水準の tag で、ここでは扱わない。
    /// </summary>
    public sealed class SeasonSceneTagPolicy : IBuildNodeTagProvider
    {
        public const string SeasonDimension = "Season";
        public const string RoleDimension = "Role";
        public static readonly IReadOnlyList<string> Seasons = Array.AsReadOnly(new[] { "Spring", "Summer", "Autumn", "Winter" });
        public static readonly IReadOnlyList<string> Roles = Array.AsReadOnly(
            Enum.GetNames(typeof(CellCompanionRole)).OrderBy(name => name, StringComparer.Ordinal).ToArray());

        private static readonly BuildTagSchema SchemaValue = new(new[]
        {
            new KeyValuePair<string, IEnumerable<string>>(SeasonDimension, Seasons),
            new KeyValuePair<string, IEnumerable<string>>(RoleDimension, Roles),
        });

        public string StableProviderKey => "samplegame.season-scene-tags.v1";

        public BuildTagSchema Schema => SchemaValue;

        public IReadOnlyList<BuildTag> GetTags(BuildGraphNode node, IReadOnlyDictionary<string, BuildGraphNode> graph)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            var tags = new List<BuildTag>(2);
            var season = FindSeason(node, graph);
            if (season != null) tags.Add(new BuildTag(SeasonDimension, season));
            if (CellCompanionRoleClassifier.TryClassify(node.Identity, out var role))
                tags.Add(new BuildTag(RoleDimension, role.ToString()));
            return tags;
        }

        /// <summary>Runtime の companion set が読む Role の集合。宣言の filter はこの表と一致していなければならない（AC20）。</summary>
        public static IReadOnlyList<string> RolesIncludedBy(string cellCompanionSet)
        {
            var set = CellCompanionSetParser.Parse(cellCompanionSet, keyExists: true);
            return Enum.GetValues(typeof(CellCompanionRole)).Cast<CellCompanionRole>()
                .Where(role => CellCompanionPolicy.Includes(set, role))
                .Select(role => role.ToString())
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        private static string? FindSeason(BuildGraphNode node, IReadOnlyDictionary<string, BuildGraphNode> graph)
        {
            // 最も近い季節名を拾うだけでは異なる季節の入れ子を見逃すため祖先を最後まで調べる。サイクルは resolver が先に止めている。
            string? season = null;
            for (var cursor = node; cursor != null; cursor = cursor.Parent == null ? null : graph[cursor.Parent])
            {
                foreach (var value in Seasons)
                {
                    if (!string.Equals(cursor.Identity, "Season_" + value, StringComparison.Ordinal)) continue;
                    if (season != null && season != value)
                        throw new InvalidOperationException("Multiple season membership: " + node.Identity);
                    season = value;
                }
            }
            return season;
        }
    }
}
