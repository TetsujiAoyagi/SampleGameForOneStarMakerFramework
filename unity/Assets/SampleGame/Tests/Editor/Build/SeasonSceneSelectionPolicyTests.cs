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

        [Test]
        public void ReversedInputs_ProduceTheSameSelectionAndRequirements()
        {
            var policy = new SeasonSceneSelectionPolicy();
            var forward = policy.SelectDetailed(Graph, Candidates(), new[] { new RepresentationProvider() },
                new[] { "Spring", "Summer" }, SeasonContentMode.FullAndWhitebox);
            var reversed = policy.SelectDetailed(Graph.Reverse(), Candidates().Reverse(),
                new[] { new RepresentationProvider() }, new[] { "Summer", "Spring" },
                SeasonContentMode.FullAndWhitebox);
            Assert.That(forward.Selection.IsSuccess, Is.True, Format(forward.Selection));
            Assert.That(reversed.Selection.IsSuccess, Is.True, Format(reversed.Selection));
            Assert.That(reversed.Selection.Plan!.SelectedContent.Select(x => x.StableKey),
                Is.EqualTo(forward.Selection.Plan!.SelectedContent.Select(x => x.StableKey)));
            Assert.That(reversed.Selection.Plan.ExcludedContent.Select(x => x.Candidate.StableKey),
                Is.EqualTo(forward.Selection.Plan.ExcludedContent.Select(x => x.Candidate.StableKey)));
            Assert.That(reversed.Requirements.Select(x => x.LogicalKey + ":" + x.Cardinality),
                Is.EqualTo(forward.Requirements.Select(x => x.LogicalKey + ":" + x.Cardinality)));
        }

        [Test]
        public void Requirements_AreScopedAndDualCellAllowsBothRepresentations()
        {
            var detail = new SeasonSceneSelectionPolicy().SelectDetailed(Graph, Candidates(),
                new[] { new RepresentationProvider() }, new[] { "Spring" }, SeasonContentMode.FullAndWhitebox);
            Assert.That(detail.Selection.IsSuccess, Is.True, Format(detail.Selection));
            Assert.That(detail.Requirements.Select(x => x.LogicalKey),
                Is.EqualTo(new[] { "Spring_Cell", "Spring_Environment", "Title" }));
            Assert.That(detail.Requirements.Select(x => x.Cardinality),
                Is.EqualTo(new[] { BuildContentCardinality.OneOrMore,
                    BuildContentCardinality.ExactlyOne, BuildContentCardinality.ExactlyOne }));
        }

        [Test]
        public void TwoFullCandidatesWithoutWhitebox_KeepExactlyOneRequirement()
        {
            var candidates = Candidates().Where(x => x.StableKey != "Spring_Cell/Whitebox")
                .Concat(new[] { Candidate("Spring_Cell", "Full2") }).ToArray();
            var detail = new SeasonSceneSelectionPolicy().SelectDetailed(Graph, candidates,
                new[] { new RepresentationProvider() }, new[] { "Spring" }, SeasonContentMode.FullAndWhitebox);
            Assert.That(detail.Requirements.Single(x => x.LogicalKey == "Spring_Cell").Cardinality,
                Is.EqualTo(BuildContentCardinality.ExactlyOne));
            Assert.That(detail.Selection.IsSuccess, Is.False);
        }

        [Test]
        public void UnknownRepresentation_FailsBeforeSelection()
        {
            var candidates = Candidates().Select(x => x.StableKey == "Spring_Cell/Whitebox"
                ? Candidate("Spring_Cell", "Unknown") : x).ToArray();
            Assert.Throws<InvalidOperationException>(() => new SeasonSceneSelectionPolicy().Select(Graph,
                candidates, new[] { new RepresentationProvider() }, new[] { "Spring" }, SeasonContentMode.Full));
        }

        [Test]
        public void DuplicateSceneIdentity_FailsBeforeSelection()
        {
            Assert.Throws<InvalidOperationException>(() => Select(Graph.Concat(new[] { Graph[0] }).ToArray(),
                SeasonContentMode.Full, "Spring"));
        }

        [Test]
        public void OrphanScene_FailsBeforeSelection()
        {
            var orphan = Graph.Concat(new[] { new SeasonSceneNode("Orphan", null, Array.Empty<string>(), false) });
            Assert.Throws<InvalidOperationException>(() => Select(orphan.ToArray(), SeasonContentMode.Full, "Spring"));
        }

        [Test]
        public void ParentCycle_FailsBeforeSelection()
        {
            var cycle = Graph.Select(x => x.Identity == "Season_Spring"
                ? new SeasonSceneNode(x.Identity, "Spring_Cell", x.Children, x.HasPayload)
                : x.Identity == "Spring_Cell"
                    ? new SeasonSceneNode(x.Identity, "Season_Spring", new[] { "Season_Spring" }, x.HasPayload)
                    : x.Identity == "InGameScene"
                        ? new SeasonSceneNode(x.Identity, null, new[] { "Season_Summer" }, x.HasPayload)
                        : x).ToArray();
            Assert.Throws<InvalidOperationException>(() => Select(cycle, SeasonContentMode.Full, "Spring"));
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
                new[] { new BuildTag("Representation", candidate.StableKey.EndsWith("/Full2", StringComparison.Ordinal)
                    ? "Full" : candidate.StableKey.Split('/').Last()) };
        }
    }
}
