#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 宣言からの到達規則（AC1）、到達しない候補と graph の失敗経路（AC2）、表現ごとの ExactlyOne（AC3）、決定性（AC6）。
    /// graph は合成で、Season と Role は test の node tag provider が辞書から付ける（SampleGame の規則は使わない）。
    /// </summary>
    public sealed class BuildPurposeResolverTests
    {
        private const string Representation = "Representation";

        private static readonly BuildTagSchema NodeSchema = new(new[]
        {
            new KeyValuePair<string, IEnumerable<string>>("Season", new[] { "Spring", "Summer" }),
            new KeyValuePair<string, IEnumerable<string>>("Role", new[] { "Environment", "Events" }),
        });

        // OutGameScene ─ Title(OnDemand) / Hub(OnDemand, payload 無し) ─ HubUi(Always)
        // InGameScene ─ InGameSession(Always, payload 無し, 付随 Lit) ─ Season_Spring(OnDemand) ─ Spring_Lighting(Always)
        //                                                                                   ├ Spring_Cell(OnDemand, Full/Whitebox) ─ Spring_Environment(Role) / Spring_Events(Role, 付随 Burst)
        //                                                                                   └ Spring_Event(OnDemand, 付随 Burst)
        //                                                  └ Season_Summer(OnDemand) ─ Summer_Cell(OnDemand, Full/Whitebox)
        private static BuildGraphNode[] Graph() => new[]
        {
            Node("OutGameScene", null, new[] { "Hub", "Title" }, BuildGraphLoad.Always, "Full"),
            Node("Title", "OutGameScene", Array.Empty<string>(), BuildGraphLoad.OnDemand, "Full"),
            Node("Hub", "OutGameScene", new[] { "HubUi" }, BuildGraphLoad.OnDemand),
            Node("HubUi", "Hub", Array.Empty<string>(), BuildGraphLoad.Always, "Full"),
            Node("InGameScene", null, new[] { "InGameSession" }, BuildGraphLoad.Always),
            new BuildGraphNode("InGameSession", "InGameScene", new[] { "Season_Spring", "Season_Summer" }, BuildGraphLoad.Always,
                Array.Empty<string>(), new[] { new BuildGraphAttachedAsset("Lit", new[] { "Full" }) }),
            Node("Season_Spring", "InGameSession", new[] { "Spring_Lighting", "Spring_Cell", "Spring_Event" }, BuildGraphLoad.OnDemand),
            Node("Spring_Lighting", "Season_Spring", Array.Empty<string>(), BuildGraphLoad.Always, "Full"),
            Node("Spring_Cell", "Season_Spring", new[] { "Spring_Environment", "Spring_Events" }, BuildGraphLoad.OnDemand, "Full", "Whitebox"),
            Node("Spring_Environment", "Spring_Cell", Array.Empty<string>(), BuildGraphLoad.OnDemand, "Full"),
            new BuildGraphNode("Spring_Events", "Spring_Cell", Array.Empty<string>(), BuildGraphLoad.OnDemand, new[] { "Full" },
                new[] { new BuildGraphAttachedAsset("Burst", new[] { "Full" }) }),
            new BuildGraphNode("Spring_Event", "Season_Spring", Array.Empty<string>(), BuildGraphLoad.OnDemand, new[] { "Full" },
                new[] { new BuildGraphAttachedAsset("Burst", new[] { "Full" }) }),
            Node("Season_Summer", "InGameSession", new[] { "Summer_Cell" }, BuildGraphLoad.OnDemand),
            Node("Summer_Cell", "Season_Summer", Array.Empty<string>(), BuildGraphLoad.OnDemand, "Full", "Whitebox"),
        };

        private static readonly Dictionary<string, BuildTag[]> NodeTags = new(StringComparer.Ordinal)
        {
            ["Season_Spring"] = new[] { new BuildTag("Season", "Spring") },
            ["Spring_Lighting"] = new[] { new BuildTag("Season", "Spring") },
            ["Spring_Cell"] = new[] { new BuildTag("Season", "Spring") },
            ["Spring_Event"] = new[] { new BuildTag("Season", "Spring") },
            ["Spring_Environment"] = new[] { new BuildTag("Season", "Spring"), new BuildTag("Role", "Environment") },
            ["Spring_Events"] = new[] { new BuildTag("Season", "Spring"), new BuildTag("Role", "Events") },
            ["Season_Summer"] = new[] { new BuildTag("Season", "Summer") },
            ["Summer_Cell"] = new[] { new BuildTag("Season", "Summer") },
        };

        private static BuildContentCandidate[] Candidates() => new[]
        {
            Candidate("OutGameScene", "Full"), Candidate("Title", "Full"), Candidate("HubUi", "Full"),
            Candidate("InGameSession:Lit", "Full", owner: "InGameSession"),
            Candidate("Spring_Lighting", "Full"), Candidate("Spring_Cell", "Full"), Candidate("Spring_Cell", "Whitebox"),
            Candidate("Spring_Environment", "Full"), Candidate("Spring_Events", "Full"),
            Candidate("Spring_Events:Burst", "Full", owner: "Spring_Events"),
            Candidate("Spring_Event", "Full"), Candidate("Spring_Event:Burst", "Full", owner: "Spring_Event"),
            Candidate("Summer_Cell", "Full"), Candidate("Summer_Cell", "Whitebox"),
        };

        private static BuildPurposeDeclaration Planner(BuildRepresentationMode mode = BuildRepresentationMode.First, params string[] preference) =>
            new("spring-planner-whitebox", new[] { "OutGameScene", "InGameScene" },
                new BuildRequest(new[] { new BuildTag("Season", "Spring"), new BuildTag("Role", "Events") }),
                new BuildRepresentationSelection(preference.Length == 0 ? new[] { "Whitebox", "Full" } : preference, mode),
                new[] { new KeyValuePair<string, string>("world:cellCompanionSet", "Planner") });

        // ═══ AC1: 到達規則と requirement、到達経路 ═══

        [Test]
        public void Planner_ReachesAlwaysChildrenAndFilteredOnDemandChildren_WithPaths()
        {
            var result = Resolve(Planner(), Graph(), Candidates());

            Assert.That(result.IsSuccess, Is.True, Issues(result));
            Assert.That(result.Requirements.Select(item => item.LogicalKey + "/" + item.Representation), Is.EqualTo(new[]
            {
                "HubUi/Full", "InGameSession:Lit/Full", "OutGameScene/Full", "Spring_Cell/Whitebox", "Spring_Event/Full",
                "Spring_Event:Burst/Full", "Spring_Events/Full", "Spring_Events:Burst/Full", "Spring_Lighting/Full", "Title/Full",
            }));
            // 付随 asset の経路は所有 node まで。payload の無い中間 node（Hub、InGameSession、Season_Spring）も経路になる。
            Assert.That(Requirement(result, "Spring_Events:Burst").Path,
                Is.EqualTo(new[] { "InGameScene", "InGameSession", "Season_Spring", "Spring_Cell", "Spring_Events" }));
            Assert.That(Requirement(result, "HubUi").Path, Is.EqualTo(new[] { "OutGameScene", "Hub", "HubUi" }));
            Assert.That(Requirement(result, "InGameSession:Lit").Path, Is.EqualTo(new[] { "InGameScene", "InGameSession" }));
            Assert.That(result.Selection.Plan!.SelectedContent.Select(item => item.StableKey), Is.EqualTo(new[]
            {
                "HubUi/Full", "InGameSession:Lit/Full", "OutGameScene/Full", "Spring_Cell/Whitebox", "Spring_Event/Full",
                "Spring_Event:Burst/Full", "Spring_Events/Full", "Spring_Events:Burst/Full", "Spring_Lighting/Full", "Title/Full",
            }));
        }

        [Test]
        public void Filter_IsOrWithinDimension_AndWithinAcross_AndNeutralWithoutTag()
        {
            // Season の OR: 両季節に到達。Role は Environment だけ: Spring_Events は Role が合わず落ち、その付随 asset も落ちる。
            var declaration = new BuildPurposeDeclaration("both", new[] { "OutGameScene", "InGameScene" },
                new BuildRequest(new[] { new BuildTag("Season", "Spring"), new BuildTag("Season", "Summer"), new BuildTag("Role", "Environment") }),
                new BuildRepresentationSelection(new[] { "Full" }, BuildRepresentationMode.First));
            var result = Resolve(declaration, Graph(), Candidates());

            Assert.That(result.IsSuccess, Is.True, Issues(result));
            var keys = result.Requirements.Select(item => item.LogicalKey).ToArray();
            Assert.That(keys, Does.Contain("Summer_Cell"));
            Assert.That(keys, Does.Contain("Spring_Environment"));
            Assert.That(keys, Does.Not.Contain("Spring_Events"));
            Assert.That(keys, Does.Not.Contain("Spring_Events:Burst"));
            // tag の無い OnDemand（Title）は中立で到達する。
            Assert.That(keys, Does.Contain("Title"));
        }

        [Test]
        public void UnrequestedDimensionTag_RejectsOnDemandChild()
        {
            // filter に Role が無い: Role tag を持つ OnDemand の子は「要求されていない dimension」で落ちる。
            var declaration = new BuildPurposeDeclaration("season-only", new[] { "OutGameScene", "InGameScene" },
                new BuildRequest(new[] { new BuildTag("Season", "Spring") }),
                new BuildRepresentationSelection(new[] { "Full" }, BuildRepresentationMode.First));
            var result = Resolve(declaration, Graph(), Candidates());

            Assert.That(result.IsSuccess, Is.True, Issues(result));
            var keys = result.Requirements.Select(item => item.LogicalKey).ToArray();
            Assert.That(keys, Does.Contain("Spring_Cell"));
            Assert.That(keys, Does.Not.Contain("Spring_Environment"));
            Assert.That(keys, Does.Not.Contain("Spring_Events"));
        }

        // ═══ AC2: 到達しない候補は Unreachable。graph の失敗は plan を公開しない ═══

        [Test]
        public void UnreachableCandidates_AreExcludedWithReason()
        {
            var result = Resolve(Planner(), Graph(), Candidates().Concat(new[] { Candidate("NotANode", "Full") }));

            Assert.That(result.IsSuccess, Is.True, Issues(result));
            var excluded = result.Selection.Plan!.ExcludedContent.ToDictionary(item => item.Candidate.StableKey, item => item.ReasonCode);
            // 到達しない subtree（Summer）、filter で落ちた companion（Environment）、graph に無い key: どれも Unreachable。
            Assert.That(excluded["Summer_Cell/Full"], Is.EqualTo(BuildExclusionReasonCode.Unreachable));
            Assert.That(excluded["Summer_Cell/Whitebox"], Is.EqualTo(BuildExclusionReasonCode.Unreachable));
            Assert.That(excluded["Spring_Environment/Full"], Is.EqualTo(BuildExclusionReasonCode.Unreachable));
            Assert.That(excluded["NotANode/Full"], Is.EqualTo(BuildExclusionReasonCode.Unreachable));
            // 到達した key の、優先順で選ばれなかった表現。
            Assert.That(excluded["Spring_Cell/Full"], Is.EqualTo(BuildExclusionReasonCode.RepresentationNotPreferred));
        }

        [Test]
        public void UnreachedAttachedAsset_IsExcluded()
        {
            // Spring_Event は到達するが、Role=Environment だけの宣言では Spring_Events が落ち、その Burst も落ちる。
            var declaration = new BuildPurposeDeclaration("env", new[] { "OutGameScene", "InGameScene" },
                new BuildRequest(new[] { new BuildTag("Season", "Spring"), new BuildTag("Role", "Environment") }),
                new BuildRepresentationSelection(new[] { "Full" }, BuildRepresentationMode.First));
            var result = Resolve(declaration, Graph(), Candidates());

            Assert.That(result.IsSuccess, Is.True, Issues(result));
            var burst = result.Selection.Plan!.ExcludedContent.Single(item => item.Candidate.StableKey == "Spring_Events:Burst/Full");
            Assert.That(burst.ReasonCode, Is.EqualTo(BuildExclusionReasonCode.Unreachable));
            Assert.That(result.Selection.Plan.SelectedContent.Any(item => item.StableKey == "Spring_Event:Burst/Full"), Is.True);
        }

        [Test]
        public void MissingRepresentationCandidate_IsRequiredGroupMissing_NotPassedAsAbsent()
        {
            // Spring_Cell は Full と Whitebox を宣言するが、materializer の Whitebox 候補だけが欠けている。
            var candidates = Candidates().Where(item => item.StableKey != "Spring_Cell/Whitebox");
            var result = Resolve(Planner(), Graph(), candidates);

            Assert.That(result.IsSuccess, Is.False);
            var issue = result.Selection.Issues.Single(item => item.Code == BuildValidationCode.RequiredGroupMissing);
            Assert.That(issue.SubjectKey, Is.EqualTo("Spring_Cell"));
            Assert.That(issue.Value, Is.EqualTo("Whitebox"));
        }

        [Test]
        public void DuplicateRepresentationCandidate_IsCardinalityViolation()
        {
            var candidates = Candidates().Concat(new[] { Candidate("Spring_Cell", "Whitebox", physical: "Spring_Cell-Whitebox-2", stable: "Spring_Cell/Whitebox#2") });
            var result = Resolve(Planner(), Graph(), candidates);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Selection.Issues.Any(item => item.Code == BuildValidationCode.CardinalityViolation && item.SubjectKey == "Spring_Cell"), Is.True);
        }

        [Test]
        public void MissingAttachedAssetCandidate_OnPayloadlessNode_IsRequiredGroupMissing()
        {
            // InGameSession は payload が無い到達 node。Lit の requirement は宣言から残り、候補が無ければ失敗する。
            var candidates = Candidates().Where(item => item.StableKey != "InGameSession:Lit/Full");
            var result = Resolve(Planner(), Graph(), candidates);

            Assert.That(result.IsSuccess, Is.False);
            var issue = result.Selection.Issues.Single(item => item.Code == BuildValidationCode.RequiredGroupMissing);
            Assert.That(issue.SubjectKey, Is.EqualTo("InGameSession:Lit"));
        }

        [Test]
        public void GraphFailures_StopWithoutPlan()
        {
            // 2 node の相互親サイクル（root から到達しない island）。リンクは相互一致しているので、サイクル検査だけが捕まえる。
            var island = Graph().Concat(new[]
            {
                Node("IslandA", "IslandB", new[] { "IslandB" }, BuildGraphLoad.OnDemand, "Full"),
                Node("IslandB", "IslandA", new[] { "IslandA" }, BuildGraphLoad.OnDemand, "Full"),
            });
            AssertGraphError(Resolve(Planner(), island, Candidates()), BuildValidationCode.GraphCycle);

            // 多親: 2 つの親が同じ子を列挙する（子の Parent は片方だけ）。
            var multiParent = Graph().Select(node => node.Identity == "Season_Summer"
                ? Node("Season_Summer", "InGameSession", new[] { "Summer_Cell", "Spring_Cell" }, BuildGraphLoad.OnDemand) : node);
            AssertGraphError(Resolve(Planner(), multiParent, Candidates()), BuildValidationCode.GraphLinkMismatch);

            // 片側リンク: 子が親を名乗るが、親が子を列挙しない。
            var oneSided = Graph().Select(node => node.Identity == "Season_Spring"
                ? Node("Season_Spring", "InGameSession", new[] { "Spring_Lighting", "Spring_Event" }, BuildGraphLoad.OnDemand) : node);
            AssertGraphError(Resolve(Planner(), oneSided, Candidates()), BuildValidationCode.GraphLinkMismatch);

            // orphan: 親が無く root でもない。
            var orphan = Graph().Concat(new[] { Node("Loose", null, Array.Empty<string>(), BuildGraphLoad.OnDemand, "Full") });
            AssertGraphError(Resolve(Planner(), orphan, Candidates()), BuildValidationCode.GraphOrphan);

            // root の欠落と identity の重複。
            AssertGraphError(Resolve(Planner(), Graph().Where(node => node.Identity != "OutGameScene"), Candidates()), BuildValidationCode.GraphMissingRoot);
            AssertGraphError(Resolve(Planner(), Graph().Concat(new[] { Graph()[1] }), Candidates()), BuildValidationCode.GraphDuplicateNode);
        }

        // ═══ AC3: First / All と優先順 ═══

        [Test]
        public void First_PicksFirstDeclaredPreference_All_PicksEveryDeclaredPreference()
        {
            var first = Resolve(Planner(BuildRepresentationMode.First, "Whitebox", "Full"), Graph(), Candidates());
            Assert.That(first.IsSuccess, Is.True, Issues(first));
            Assert.That(first.Requirements.Where(item => item.LogicalKey == "Spring_Cell").Select(item => item.Representation), Is.EqualTo(new[] { "Whitebox" }));
            Assert.That(first.Requirements.Where(item => item.LogicalKey == "Title").Select(item => item.Representation), Is.EqualTo(new[] { "Full" }));

            var all = Resolve(Planner(BuildRepresentationMode.All, "Full", "Whitebox"), Graph(), Candidates());
            Assert.That(all.IsSuccess, Is.True, Issues(all));
            Assert.That(all.Requirements.Where(item => item.LogicalKey == "Spring_Cell").Select(item => item.Representation), Is.EqualTo(new[] { "Full", "Whitebox" }));
            Assert.That(all.Requirements.Where(item => item.LogicalKey == "Title").Select(item => item.Representation), Is.EqualTo(new[] { "Full" }));
            Assert.That(all.Selection.Plan!.SelectedContent.Count(item => item.LogicalKey == "Spring_Cell"), Is.EqualTo(2));
        }

        [Test]
        public void RepresentationOutsidePreference_IsExcludedNotPreferred_AndNodeWithoutPreferredRepresentationFails()
        {
            // Title が Lowpoly 候補も持つ: 優先順に無いので RepresentationNotPreferred。
            var lowpoly = Graph().Select(node => node.Identity == "Title"
                ? Node("Title", "OutGameScene", Array.Empty<string>(), BuildGraphLoad.OnDemand, "Full", "Lowpoly") : node);
            var result = Resolve(Planner(), lowpoly, Candidates().Concat(new[] { Candidate("Title", "Lowpoly") }));
            Assert.That(result.IsSuccess, Is.True, Issues(result));
            Assert.That(result.Selection.Plan!.ExcludedContent.Single(item => item.Candidate.StableKey == "Title/Lowpoly").ReasonCode,
                Is.EqualTo(BuildExclusionReasonCode.RepresentationNotPreferred));

            // 到達した node が優先順のどの表現も宣言しない: requirement を作れず、plan を公開しない。
            var onlyLowpoly = Graph().Select(node => node.Identity == "Title"
                ? Node("Title", "OutGameScene", Array.Empty<string>(), BuildGraphLoad.OnDemand, "Lowpoly") : node);
            var failed = Resolve(Planner(), onlyLowpoly, Candidates());
            Assert.That(failed.IsSuccess, Is.False);
            Assert.That(failed.Selection.Issues.Single(item => item.Code == BuildValidationCode.RequiredGroupMissing).SubjectKey, Is.EqualTo("Title"));
        }

        [Test]
        public void ContentPrefixedFilterOrUnknownNodeTag_IsIssue()
        {
            var unknown = new BuildPurposeDeclaration("x", new[] { "OutGameScene", "InGameScene" },
                new BuildRequest(new[] { new BuildTag("Weather", "Rain") }),
                new BuildRepresentationSelection(new[] { "Full" }, BuildRepresentationMode.First));
            var result = Resolve(unknown, Graph(), Candidates());
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Selection.Issues.Any(item => item.Code == BuildValidationCode.UnknownDimension && item.Subject == BuildValidationSubject.Request), Is.True);

            var badTags = new Dictionary<string, BuildTag[]>(NodeTags, StringComparer.Ordinal) { ["Title"] = new[] { new BuildTag("Season", "Autumn") } };
            var tagged = new BuildPurposeResolver().Resolve(Planner(), Graph(), Candidates(), new[] { new RepresentationProvider() }, new DictionaryNodeTags(badTags));
            Assert.That(tagged.IsSuccess, Is.False);
            Assert.That(tagged.Selection.Issues.Any(item => item.Code == BuildValidationCode.UnknownValue && item.Subject == BuildValidationSubject.Node), Is.True);
        }

        // ═══ AC6: 入力順に依存しない ═══

        [Test]
        public void ReversedAndShuffledInputs_ProduceTheSameSelectionAndPaths()
        {
            var forward = Resolve(Planner(), Graph(), Candidates());
            var reversed = Resolve(Planner(), Graph().Reverse(), Candidates().Reverse());
            var random = new Random(20261031);
            var shuffled = Resolve(Planner(), Graph().OrderBy(_ => random.Next()), Candidates().OrderBy(_ => random.Next()));

            foreach (var other in new[] { reversed, shuffled })
            {
                Assert.That(Snapshot(other), Is.EqualTo(Snapshot(forward)));
            }
        }

        private static string Snapshot(BuildPurposeResolution resolution) =>
            string.Join("|", resolution.Selection.Plan!.SelectedContent.Select(item => "S:" + item.StableKey))
            + "|" + string.Join("|", resolution.Selection.Plan.ExcludedContent.Select(item => "X:" + item.StableKey() + ":" + item.ReasonCode))
            + "|" + string.Join("|", resolution.Requirements.Select(item => "R:" + item.LogicalKey + "/" + item.Representation + "=" + string.Join(">", item.Path)))
            + "|" + Issues(resolution);

        private static void AssertGraphError(BuildPurposeResolution resolution, BuildValidationCode code)
        {
            Assert.That(resolution.Selection.Plan, Is.Null);
            Assert.That(resolution.Requirements, Is.Empty);
            Assert.That(resolution.Selection.Issues.Any(item => item.Code == code && item.Severity == BuildValidationSeverity.Error), Is.True, Issues(resolution));
        }

        private static BuildPurposeResolution Resolve(BuildPurposeDeclaration declaration, IEnumerable<BuildGraphNode> graph, IEnumerable<BuildContentCandidate> candidates) =>
            new BuildPurposeResolver().Resolve(declaration, graph, candidates, new[] { new RepresentationProvider() }, new DictionaryNodeTags(NodeTags));

        private static BuildPurposeRequirement Requirement(BuildPurposeResolution resolution, string logicalKey) =>
            resolution.Requirements.Single(item => item.LogicalKey == logicalKey);

        private static string Issues(BuildPurposeResolution resolution) =>
            string.Join(";", resolution.Selection.Issues.Select(item => item.Severity + ":" + item.Code + ":" + item.Subject + ":" + item.SubjectKey + ":" + item.Dimension + "=" + item.Value));

        private static BuildGraphNode Node(string identity, string? parent, string[] children, BuildGraphLoad load, params string[] representations) =>
            new(identity, parent, children, load, representations);

        private static BuildContentCandidate Candidate(string logical, string representation, string? owner = null, string? physical = null, string? stable = null) =>
            new(stable ?? logical + "/" + representation, logical, physical ?? logical + "-" + representation, new BuildProvenance("test", logical), owner);

        private sealed class RepresentationProvider : IBuildTagProvider
        {
            public string StableProviderKey => "test.representation";
            public IReadOnlyList<BuildTag> GetTags(BuildContentCandidate candidate)
            {
                var stable = candidate.StableKey;
                var hash = stable.IndexOf('#');
                if (hash >= 0) stable = stable.Substring(0, hash);
                return new[] { new BuildTag(Representation, stable.Substring(stable.LastIndexOf('/') + 1)) };
            }
        }

        private sealed class DictionaryNodeTags : IBuildNodeTagProvider
        {
            private readonly IReadOnlyDictionary<string, BuildTag[]> _tags;
            public DictionaryNodeTags(IReadOnlyDictionary<string, BuildTag[]> tags) => _tags = tags;
            public string StableProviderKey => "test.node-tags";
            public BuildTagSchema Schema => NodeSchema;
            public IReadOnlyList<BuildTag> GetTags(BuildGraphNode node, IReadOnlyDictionary<string, BuildGraphNode> graph) =>
                _tags.TryGetValue(node.Identity, out var tags) ? tags : Array.Empty<BuildTag>();
        }
    }

    internal static class BuildContentExclusionExtensions
    {
        internal static string StableKey(this BuildContentExclusion exclusion) => exclusion.Candidate.StableKey;
    }
}
