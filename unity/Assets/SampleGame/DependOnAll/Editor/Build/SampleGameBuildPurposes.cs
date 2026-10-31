#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Build.Selection;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// SampleGame の content build の宣言の表。menu・BS4 Player・判定の照合はこの 6 件だけを使う。
    /// <para>
    /// root は OutGame と InGame の 2 つ。filter は Season と Role。Role の集合は runtime 既定値 <c>world:cellCompanionSet</c> が
    /// Runtime で読む companion の集合と同じでなければならない（Planner の directory を Full で Play すると欠け、逆は無駄になる）。
    /// 対応は <see cref="SeasonSceneTagPolicy.RolesIncludedBy"/> から引き、宣言を足すときはテストがその一致を要求する。
    /// </para>
    /// </summary>
    public static class SampleGameBuildPurposes
    {
        public const string CellCompanionSetKey = "world:cellCompanionSet";
        private static readonly string[] Roots = { "OutGameScene", "InGameScene" };

        public static readonly BuildPurposeDeclaration AllFull = Declare("all-full", SeasonSceneTagPolicy.Seasons, "Full", First("Full"));
        public static readonly BuildPurposeDeclaration SpringFull = Declare("spring-full", new[] { "Spring" }, "Full", First("Full"));
        public static readonly BuildPurposeDeclaration SpringWhitebox = Declare("spring-whitebox", new[] { "Spring" }, "Full", First("Whitebox", "Full"));
        public static readonly BuildPurposeDeclaration SpringFullWhitebox = Declare("spring-full-whitebox", new[] { "Spring" }, "Full",
            new BuildRepresentationSelection(new[] { "Full", "Whitebox" }, BuildRepresentationMode.All));
        // BS4 の Player は spring-full と同じ選択に probe 1 件を合成する。名前（contentSet）だけが違う。
        public static readonly BuildPurposeDeclaration Bs4SpringFull = Declare("bs4-spring-full", new[] { "Spring" }, "Full", First("Full"));
        // Planner profile: Environment・Cell Lighting・Cell VFX を持たず、Events と Season の NecessaryAlways だけを Whitebox で持つ。
        public static readonly BuildPurposeDeclaration SpringPlannerWhitebox = Declare("spring-planner-whitebox", new[] { "Spring" }, "Planner", First("Whitebox", "Full"));

        /// <summary>menu と照合が使う宣言の全件。名前の ordinal 順。</summary>
        public static IReadOnlyList<BuildPurposeDeclaration> All { get; } = Array.AsReadOnly(new[]
        {
            AllFull, SpringFull, SpringWhitebox, SpringFullWhitebox, Bs4SpringFull, SpringPlannerWhitebox,
        }.OrderBy(declaration => declaration.Name, StringComparer.Ordinal).ToArray());

        public static BuildPurposeDeclaration Find(string name) =>
            All.FirstOrDefault(declaration => string.Equals(declaration.Name, name, StringComparison.Ordinal))
            ?? throw new ArgumentException("Unknown build purpose: " + name, nameof(name));

        /// <summary>宣言の runtime 既定値から companion set を読む。宣言が持たなければ Runtime の既定（Full）と同じ。</summary>
        public static string CellCompanionSetOf(BuildPurposeDeclaration declaration) =>
            declaration.FindRuntimeDefault(CellCompanionSetKey) ?? "Full";

        private static BuildRepresentationSelection First(params string[] preference) =>
            new(preference, BuildRepresentationMode.First);

        private static BuildPurposeDeclaration Declare(string name, IEnumerable<string> seasons, string cellCompanionSet, BuildRepresentationSelection representation)
        {
            var filter = seasons.Select(season => new BuildTag(SeasonSceneTagPolicy.SeasonDimension, season))
                .Concat(SeasonSceneTagPolicy.RolesIncludedBy(cellCompanionSet).Select(role => new BuildTag(SeasonSceneTagPolicy.RoleDimension, role)));
            return new BuildPurposeDeclaration(name, Roots, new BuildRequest(filter), representation,
                new[] { new KeyValuePair<string, string>(CellCompanionSetKey, cellCompanionSet) });
        }
    }
}
