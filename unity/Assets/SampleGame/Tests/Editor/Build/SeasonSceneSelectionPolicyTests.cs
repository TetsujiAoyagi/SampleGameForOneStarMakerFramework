#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using SampleGame.DependOnAll.Editor.Build;

namespace SampleGame.Tests.Editor.Build
{
    public sealed class SeasonSceneSelectionPolicyTests
    {
        private static readonly SeasonSceneNode[] Graph =
        {
            new("OutGameScene", null, new[] { "Title" }, false),
            new("Title", "OutGameScene", Array.Empty<string>(), true),
            new("InGameScene", null, new[] { "Season_Spring", "Season_Summer" }, false),
            new("Season_Spring", "InGameScene", new[] { "Spring_Cell", "Spring_Environment" }, false),
            new("Spring_Cell", "Season_Spring", Array.Empty<string>(), true),
            new("Spring_Environment", "Season_Spring", Array.Empty<string>(), true),
            new("Season_Summer", "InGameScene", new[] { "Summer_Cell" }, false),
            new("Summer_Cell", "Season_Summer", Array.Empty<string>(), true)
        };

        [TestCase(SeasonContentMode.Full, "Spring_Cell,Spring_Environment,Title")]
        [TestCase(SeasonContentMode.Whitebox, "Spring_Cell,Spring_Environment,Title")]
        [TestCase(SeasonContentMode.FullAndWhitebox, "Spring_Cell,Spring_Cell,Spring_Environment,Title")]
        public void SpringSelection_KeepsCommonAndCompanion(SeasonContentMode mode, string expected)
        {
            var result = Select(Graph, mode, "Spring");
            Assert.That(result.IsSuccess, Is.True, Format(result));
            Assert.That(string.Join(",", result.Plan!.SelectedContent.Select(x => x.LogicalKey)), Is.EqualTo(expected));
            var cellVariants = result.Plan.SelectedContent.Where(x => x.LogicalKey == "Spring_Cell")
                .Select(x => x.StableKey.Split('/').Last()).ToArray();
            Assert.That(cellVariants, Is.EqualTo(mode == SeasonContentMode.Full ? new[] { "Full" }
                : mode == SeasonContentMode.Whitebox ? new[] { "Whitebox" } : new[] { "Full", "Whitebox" }));
            Assert.That(result.Plan.SelectedContent.Count + result.Plan.ExcludedContent.Count, Is.EqualTo(5));
            Assert.That(result.Plan.ExcludedContent.Any(x => x.Candidate.LogicalKey == "Summer_Cell" && x.Dimension == "Season"), Is.True);
        }

        [Test]
        public void AllSeasonFull_IncludesBothSeasonsButNotWhitebox()
        {
            var result = Select(Graph, SeasonContentMode.Full, "Spring", "Summer");
            Assert.That(result.IsSuccess, Is.True, Format(result));
            Assert.That(result.Plan!.SelectedContent.Select(x => x.LogicalKey), Does.Contain("Summer_Cell"));
            Assert.That(result.Plan.SelectedContent.Any(x => x.StableKey.EndsWith("/Whitebox", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void BrokenParentChildLink_FailsBeforeSelection()
        {
            var broken = Graph.Select(x => x.Identity == "Spring_Cell"
                ? new SeasonSceneNode(x.Identity, "Season_Summer", x.Children, x.HasPayload) : x).ToArray();
            Assert.Throws<InvalidOperationException>(() => Select(broken, SeasonContentMode.Full, "Spring"));
        }

        [Test]
        public void DeclaredPayloadWithoutCandidate_FailsBeforeSelection()
        {
            var candidates = Candidates().Where(x => x.LogicalKey != "Spring_Environment").ToArray();
            Assert.Throws<InvalidOperationException>(() => new SeasonSceneSelectionPolicy().Select(Graph, candidates,
                new[] { new RepresentationProvider() }, new[] { "Spring" }, SeasonContentMode.Full));
        }

        [Test]
        public void ExplicitSeasonOverride_ChangesProjectSelectionOnly()
        {
            var result = new SeasonSceneSelectionPolicy().Select(Graph, Candidates(),
                new[] { new RepresentationProvider() }, new[] { "Spring" }, SeasonContentMode.Full,
                new Dictionary<string, string> { ["Summer_Cell"] = "Spring" });
            Assert.That(result.IsSuccess, Is.True, Format(result));
            Assert.That(result.Plan!.SelectedContent.Select(x => x.LogicalKey), Does.Contain("Summer_Cell"));
        }

        private static BuildPlanResult Select(SeasonSceneNode[] graph, SeasonContentMode mode, params string[] seasons) =>
            new SeasonSceneSelectionPolicy().Select(graph, Candidates(), new[] { new RepresentationProvider() }, seasons, mode);

        private static BuildContentCandidate[] Candidates() => new[]
        {
            Candidate("Title", "Full"), Candidate("Spring_Cell", "Full"), Candidate("Spring_Cell", "Whitebox"),
            Candidate("Spring_Environment", "Full"), Candidate("Summer_Cell", "Full")
        };

        private static BuildContentCandidate Candidate(string logical, string representation) =>
            new(logical + "/" + representation, logical, logical + "-" + representation,
                new BuildProvenance("test", logical));

        private static string Format(BuildPlanResult result) =>
            string.Join(";", result.Issues.Select(x => x.Code + ":" + x.SubjectKey));

        private sealed class RepresentationProvider : IBuildTagProvider
        {
            public string StableProviderKey => "test.representation";
            public IReadOnlyList<BuildTag> GetTags(BuildContentCandidate candidate) =>
                new[] { new BuildTag("Representation", candidate.StableKey.Split('/').Last()) };
        }
    }
}
