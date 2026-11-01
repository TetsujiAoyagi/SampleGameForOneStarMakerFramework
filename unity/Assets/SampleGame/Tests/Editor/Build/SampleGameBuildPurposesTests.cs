#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using SampleGame.DependOnAll.Editor.Build;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// 宣言の表と、実 SceneResourceMap に対する選択の性質。選択だけを見るので content build は行わない。
    /// bootstrap 2 件は resolver の外（composer）なので選択に含まれない。
    /// </summary>
    public sealed class SampleGameBuildPurposesTests
    {
        private const int BootstrapCount = 2;

        private static readonly string[] AttachedAssets =
        {
            "Spring_Events_4_2:Burst", "Spring_Event_MultiCellProof:Burst", "InGameSession:Lit",
        };

        private static readonly Regex[] ForbiddenForPlanner =
        {
            new("_Environment_"), new(@"_Lighting_\d+_\d+$"), new(@"_VFX_\d+_\d+$"),
        };

        // ═══ AC20: 宣言の表と Role の対応 ═══

        [Test]
        public void Table_HasSixDeclarations_WithSharedRootsAndCompanionSetDefaults()
        {
            Assert.That(SampleGameBuildPurposes.All.Select(d => d.Name), Is.EqualTo(new[]
            {
                "all-full", "bs4-spring-full", "spring-full", "spring-full-whitebox", "spring-planner-whitebox", "spring-whitebox",
            }));
            foreach (var declaration in SampleGameBuildPurposes.All)
            {
                Assert.That(declaration.Roots, Is.EqualTo(new[] { "InGameScene", "OutGameScene" }), declaration.Name);
                Assert.That(declaration.FindRuntimeDefault(SampleGameBuildPurposes.CellCompanionSetKey), Is.Not.Null, declaration.Name);
                Assert.That(declaration.RuntimeDefaults.Count, Is.EqualTo(1), declaration.Name);
            }
        }

        [Test]
        public void EveryDeclaration_FiltersRolesExactlyAsItsCompanionSetReads()
        {
            foreach (var declaration in SampleGameBuildPurposes.All)
            {
                var roles = declaration.Filter.Selections
                    .Where(tag => tag.Dimension == SeasonSceneTagPolicy.RoleDimension)
                    .Select(tag => tag.Value!).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                var expected = SeasonSceneTagPolicy.RolesIncludedBy(SampleGameBuildPurposes.CellCompanionSetOf(declaration));
                Assert.That(roles, Is.EqualTo(expected), declaration.Name + " の Role filter が companion set と違う");
            }
            Assert.That(SampleGameBuildPurposes.CellCompanionSetOf(SampleGameBuildPurposes.SpringPlannerWhitebox), Is.EqualTo("Planner"));
            Assert.That(SampleGameBuildPurposes.SpringPlannerWhitebox.Filter.Selections.Where(t => t.Dimension == "Role").Select(t => t.Value),
                Is.EqualTo(new[] { "Events" }));
            Assert.That(SampleGameBuildPurposes.SpringFull.Filter.Selections.Where(t => t.Dimension == "Role").Count(), Is.EqualTo(4));
        }

        [Test]
        public void Representations_MatchTheLegacyModes()
        {
            Assert.That(SampleGameBuildPurposes.SpringFull.Representation.Preference, Is.EqualTo(new[] { "Full" }));
            Assert.That(SampleGameBuildPurposes.SpringWhitebox.Representation.Preference, Is.EqualTo(new[] { "Whitebox", "Full" }));
            Assert.That(SampleGameBuildPurposes.SpringWhitebox.Representation.Mode, Is.EqualTo(BuildRepresentationMode.First));
            Assert.That(SampleGameBuildPurposes.SpringFullWhitebox.Representation.Mode, Is.EqualTo(BuildRepresentationMode.All));
            Assert.That(SampleGameBuildPurposes.SpringFullWhitebox.Representation.Primary, Is.EqualTo("Full"));
            Assert.That(SampleGameBuildPurposes.SpringPlannerWhitebox.Representation.Primary, Is.EqualTo("Whitebox"));
            Assert.That(SampleGameBuildPurposes.Bs4SpringFull.Representation.Primary, Is.EqualTo("Full"));
            Assert.That(SampleGameBuildPurposes.AllFull.Filter.Selections.Where(t => t.Dimension == "Season").Count(), Is.EqualTo(4));
        }

        // ═══ 既存の宣言: 付随 asset を含み、全 requirement が root から到達する ═══

        [TestCase("all-full")]
        [TestCase("spring-full")]
        [TestCase("spring-whitebox")]
        [TestCase("spring-full-whitebox")]
        [TestCase("bs4-spring-full")]
        public void ExistingDeclarations_SelectAttachedAssetsAndReachEveryRequirementFromARoot(string name)
        {
            var resolution = SampleGameContentBuild.Resolve(SampleGameBuildPurposes.Find(name));
            Assert.That(resolution.Selection.Plan, Is.Not.Null, Issues(resolution));
            var logical = resolution.Selection.Plan!.SelectedContent.Select(x => x.LogicalKey).ToArray();
            foreach (var attached in AttachedAssets)
                Assert.That(logical, Does.Contain(attached), name);
            Assert.That(resolution.Requirements.All(r => r.Path.Count > 0 && (r.Path[0] == "OutGameScene" || r.Path[0] == "InGameScene")), Is.True);
        }

        [Test]
        public void Bs4SpringFull_SelectsTheSameContentAsSpringFull()
        {
            Assert.That(SelectedStableKeys(SampleGameBuildPurposes.Bs4SpringFull), Is.EqualTo(SelectedStableKeys(SampleGameBuildPurposes.SpringFull)));
        }

        // ═══ spring-planner-whitebox は 70 件（bootstrap 2 件を含む）で、Environment・Cell Lighting・Cell VFX を含まない ═══

        [Test]
        public void SpringPlannerWhitebox_SelectsSeventyWithoutEnvironmentLightingOrVfx()
        {
            var resolution = SampleGameContentBuild.Resolve(SampleGameBuildPurposes.SpringPlannerWhitebox);
            Assert.That(resolution.Selection.Plan, Is.Not.Null, Issues(resolution));
            var selected = resolution.Selection.Plan!.SelectedContent;
            Assert.That(selected.Count + BootstrapCount, Is.EqualTo(70));

            var logical = selected.Select(x => x.LogicalKey).ToArray();
            foreach (var pattern in ForbiddenForPlanner)
                Assert.That(logical.Where(key => pattern.IsMatch(key)), Is.Empty, "禁止 pattern " + pattern);
            Assert.That(logical.Count(key => Regex.IsMatch(key, @"^Spring_Cell_\d+_\d+$")), Is.EqualTo(54));
            Assert.That(selected.Where(x => Regex.IsMatch(x.LogicalKey, @"^Spring_Cell_\d+_\d+$"))
                .All(x => x.StableKey.Contains("8:Whitebox")), Is.True, "Cell は全て Whitebox");
            foreach (var required in new[] { "Spring_Events_4_2", "Spring_Event_MultiCellProof", "Spring_Lighting", "Spring_AtmosphereVFX" }.Concat(AttachedAssets))
                Assert.That(logical, Does.Contain(required));
            Assert.That(logical.Any(key => key.StartsWith("Summer_", StringComparison.Ordinal) || key.StartsWith("Autumn_", StringComparison.Ordinal)
                || key.StartsWith("Winter_", StringComparison.Ordinal)), Is.False);
        }

        private static string[] SelectedStableKeys(BuildPurposeDeclaration declaration)
        {
            var resolution = SampleGameContentBuild.Resolve(declaration);
            Assert.That(resolution.Selection.Plan, Is.Not.Null, Issues(resolution));
            return resolution.Selection.Plan!.SelectedContent.Select(x => x.StableKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        private static string Issues(BuildPurposeResolution resolution) =>
            string.Join("; ", resolution.Selection.Issues.Select(x => x.Code + ":" + x.Subject + ":" + x.SubjectKey + ":" + x.Dimension + "=" + x.Value));
    }
}
