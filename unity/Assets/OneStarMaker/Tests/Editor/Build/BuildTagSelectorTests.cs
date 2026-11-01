#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class BuildTagSelectorTests
    {
        private static readonly BuildTagSchema Schema = new BuildTagSchema(new[]
        {
            Dimension("Representation", "Full", "Whitebox"),
            Dimension("Season", "Spring", "Summer")
        });

        [TestCase(null, null, true)]
        [TestCase("Season", "Spring", true)]
        [TestCase("Representation", "Whitebox", true)]
        [TestCase("Both", "Selected", true)]
        [TestCase("Season", "Summer", false)]
        [TestCase("Representation", "Full", false)]
        [TestCase("Both", "WrongSeason", false)]
        [TestCase("Both", "WrongRepresentation", false)]
        public void TruthTable_SelectsNeutralAndRequiresAllTaggedDimensions(
            string? candidateShape,
            string? candidateValues,
            bool expectedSelected)
        {
            var tags = TagsFor(candidateShape, candidateValues);
            var result = Select(
                new[] { Candidate("candidate", "group", "physical") },
                new[] { new FakeProvider("provider", _ => tags) },
                Request(Tag("Season", "Spring"), Tag("Representation", "Whitebox")));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Plan!.SelectedContent.Select(item => item.StableKey),
                expectedSelected ? Is.EquivalentTo(new[] { "candidate" }) : Is.Empty);
        }

        [Test]
        public void RequestMultipleValues_AreOrWithinDimension()
        {
            var result = Select(
                new[]
                {
                    Candidate("spring", "world", "p1"),
                    Candidate("summer", "world", "p2")
                },
                new[] { new FakeProvider("provider", candidate => new[] { Tag("Season", candidate.StableKey == "spring" ? "Spring" : "Summer") }) },
                Request(Tag("Season", "Spring"), Tag("Season", "Summer")));

            Assert.That(result.Plan!.SelectedContent.Select(item => item.StableKey),
                Is.EqualTo(new[] { "spring", "summer" }));
        }

        [Test]
        public void SchemaValidButUnrequestedDimension_IsStructuredExclusion()
        {
            var result = Select(
                new[] { Candidate("candidate", "world", "physical") },
                new[] { new FakeProvider("provider", _ => new[] { Tag("Season", "Spring") }) },
                Request(Tag("Representation", "Full")));

            var exclusion = result.Plan!.ExcludedContent.Single();
            Assert.That(exclusion.ReasonCode, Is.EqualTo(BuildExclusionReasonCode.UnrequestedDimension));
            Assert.That(exclusion.Dimension, Is.EqualTo("Season"));
            Assert.That(exclusion.Value, Is.EqualTo("Spring"));
        }

        [Test]
        public void RequestedDimensionWithDifferentValue_IsStructuredExclusion()
        {
            var result = Select(
                new[] { Candidate("candidate", "world", "physical") },
                new[] { new FakeProvider("provider", _ => new[] { Tag("Season", "Summer") }) },
                Request(Tag("Season", "Spring")));

            Assert.That(result.Plan!.ExcludedContent.Single().ReasonCode,
                Is.EqualTo(BuildExclusionReasonCode.ValueNotSelected));
        }

        [TestCase(null, "Spring")]
        [TestCase("", "Spring")]
        [TestCase(" Season", "Spring")]
        [TestCase("Season", null)]
        [TestCase("Season", "")]
        [TestCase("Season", "Spring ")]
        public void InvalidRequestSelection_IsErrorAndHidesPlan(string? dimension, string? value)
        {
            var result = Select(Array.Empty<BuildContentCandidate>(), Array.Empty<IBuildTagProvider>(),
                Request(Tag(dimension, value)));

            AssertError(result, BuildValidationCode.InvalidRequestSelection);
        }

        [Test]
        public void EmptyRequestSelection_IsErrorAndHidesPlan()
        {
            var result = Select(
                new[] { Candidate("neutral", "world", "physical") },
                Array.Empty<IBuildTagProvider>(),
                Request());

            AssertError(result, BuildValidationCode.InvalidRequestSelection);
        }

        [Test]
        public void UnknownRequestDimensionAndValue_AreErrors()
        {
            var result = Select(Array.Empty<BuildContentCandidate>(), Array.Empty<IBuildTagProvider>(),
                Request(Tag("Unknown", "Spring"), Tag("Season", "Autumn")));

            Assert.That(result.Plan, Is.Null);
            Assert.That(result.Issues.Select(issue => issue.Code),
                Is.EquivalentTo(new[] { BuildValidationCode.UnknownDimension, BuildValidationCode.UnknownValue }));
        }

        [Test]
        public void UnknownCandidateDimensionAndValue_AreErrorsAttributedToProvider()
        {
            var result = Select(
                new[] { Candidate("candidate", "world", "physical") },
                new[] { new FakeProvider("provider-a", _ => new[] { Tag("Unknown", "Spring"), Tag("Season", "Autumn") }) },
                Request(Tag("Season", "Spring")));

            Assert.That(result.Plan, Is.Null);
            Assert.That(result.Issues.Any(issue => issue.Code == BuildValidationCode.UnknownDimension), Is.True);
            Assert.That(result.Issues.Any(issue => issue.Code == BuildValidationCode.UnknownValue), Is.True);
            Assert.That(result.Issues.All(issue => issue.ProviderKey == "provider-a"), Is.True);
        }

        [TestCase("season", "Spring", BuildValidationCode.UnknownDimension)]
        [TestCase("Season", "spring", BuildValidationCode.UnknownValue)]
        public void RequestSchemaComparison_IsOrdinalCaseSensitive(
            string dimension,
            string value,
            BuildValidationCode expectedCode)
        {
            var result = Select(
                new[] { Candidate("candidate", "world", "physical") },
                Array.Empty<IBuildTagProvider>(),
                Request(Tag(dimension, value)));

            AssertError(result, expectedCode);
        }

        [TestCase(null, "Spring", BuildValidationCode.UnknownDimension)]
        [TestCase("", "Spring", BuildValidationCode.UnknownDimension)]
        [TestCase(" Season", "Spring", BuildValidationCode.UnknownDimension)]
        [TestCase("Season ", "Spring", BuildValidationCode.UnknownDimension)]
        [TestCase("Season", null, BuildValidationCode.UnknownValue)]
        [TestCase("Season", "", BuildValidationCode.UnknownValue)]
        [TestCase("Season", " Spring", BuildValidationCode.UnknownValue)]
        [TestCase("Season", "Spring ", BuildValidationCode.UnknownValue)]
        public void MalformedCandidateTag_IsErrorAndHidesPlan(
            string? dimension,
            string? value,
            BuildValidationCode expectedCode)
        {
            var result = Select(
                new[] { Candidate("candidate", "world", "physical") },
                new[] { new FakeProvider("provider", _ => new[] { Tag(dimension, value) }) },
                Request(Tag("Season", "Spring")));

            AssertError(result, expectedCode);
        }

        [Test]
        public void NullCandidateTag_IsStructuredErrorAndHidesPlan()
        {
            var result = Select(
                new[] { Candidate("candidate", "world", "physical") },
                new[] { new FakeProvider("provider", _ => new BuildTag[] { null! }) },
                Request(Tag("Season", "Spring")));

            AssertError(result, BuildValidationCode.UnknownDimension);
            Assert.That(result.Issues.Single().Subject, Is.EqualTo(BuildValidationSubject.Candidate));
            Assert.That(result.Issues.Single().ProviderKey, Is.EqualTo("provider"));
        }

        [Test]
        public void ProvenanceSceneRole_IsNotInferredAsBuildTag()
        {
            var candidate = new BuildContentCandidate("candidate", "world", "physical",
                new BuildProvenance("scene", "source", new[]
                {
                    new KeyValuePair<string, string>("SceneRole", "Lighting")
                }));

            var result = Select(
                new[] { candidate },
                Array.Empty<IBuildTagProvider>(),
                Request(Tag("Season", "Spring")));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Plan!.SelectedContent.Select(item => item.StableKey),
                Is.EqualTo(new[] { "candidate" }));
        }

        [Test]
        public void DuplicateTag_IsWarningAndPlanRemainsAvailable()
        {
            var result = Select(
                new[] { Candidate("candidate", "world", "physical") },
                new[]
                {
                    new FakeProvider("a", _ => new[] { Tag("Season", "Spring") }),
                    new FakeProvider("b", _ => new[] { Tag("Season", "Spring") })
                },
                Request(Tag("Season", "Spring")));

            Assert.That(result.Plan, Is.Not.Null);
            Assert.That(result.Issues.Single().Code, Is.EqualTo(BuildValidationCode.DuplicateTag));
            Assert.That(result.Issues.Single().Severity, Is.EqualTo(BuildValidationSeverity.Warning));
        }

        [Test]
        public void ConflictingValuesForCandidateDimension_AreError()
        {
            var result = Select(
                new[] { Candidate("candidate", "world", "physical") },
                new[] { new FakeProvider("provider", _ => new[] { Tag("Season", "Spring"), Tag("Season", "Summer") }) },
                Request(Tag("Season", "Spring")));

            AssertError(result, BuildValidationCode.ConflictingCandidateValues);
        }

        [Test]
        public void DuplicateCandidateKey_IsError()
        {
            var result = Select(
                new[] { Candidate("same", "a", "p1"), Candidate("same", "b", "p2") },
                Array.Empty<IBuildTagProvider>(),
                Request(Tag("Representation", "Full")));

            AssertError(result, BuildValidationCode.DuplicateCandidateKey);
        }

        [Test]
        public void SharedPhysicalKey_IsError()
        {
            var result = Select(
                new[] { Candidate("a", "group", "same"), Candidate("b", "group", "same") },
                Array.Empty<IBuildTagProvider>(),
                Request(Tag("Representation", "Full")));

            AssertError(result, BuildValidationCode.PhysicalKeyCollision);
        }

        [TestCase(BuildContentCardinality.OneOrMore, 0, false)]
        [TestCase(BuildContentCardinality.OneOrMore, 1, true)]
        [TestCase(BuildContentCardinality.OneOrMore, 2, true)]
        [TestCase(BuildContentCardinality.ExactlyOne, 0, false)]
        [TestCase(BuildContentCardinality.ExactlyOne, 1, true)]
        [TestCase(BuildContentCardinality.ExactlyOne, 2, false)]
        public void RequirementCardinality_CountsSelectedDistinctCandidates(
            BuildContentCardinality cardinality,
            int selectedCount,
            bool expectedSuccess)
        {
            var candidates = new[]
            {
                Candidate("a", "required", "p1"),
                Candidate("b", "required", "p2")
            };
            var result = Select(
                candidates,
                new[] { new FakeProvider("provider", candidate =>
                    new[] { Tag("Season", selectedCount == 0 ? "Summer" : candidate.StableKey == "a" || selectedCount == 2 ? "Spring" : "Summer") }) },
                Request(Tag("Season", "Spring")),
                new BuildContentRequirement("required", cardinality));

            Assert.That(result.IsSuccess, Is.EqualTo(expectedSuccess));
            if (!expectedSuccess) AssertError(result, BuildValidationCode.CardinalityViolation);
        }

        [Test]
        public void RequirementWithNoCandidates_UsesRequiredGroupMissing()
        {
            var result = Select(Array.Empty<BuildContentCandidate>(), Array.Empty<IBuildTagProvider>(),
                Request(Tag("Representation", "Full")),
                new BuildContentRequirement("missing", BuildContentCardinality.OneOrMore));

            AssertError(result, BuildValidationCode.RequiredGroupMissing);
        }

        [Test]
        public void DuplicateAndConflictingRequirements_AreDistinctErrors()
        {
            var candidate = Candidate("candidate", "group", "physical");
            var duplicate = Select(new[] { candidate }, Array.Empty<IBuildTagProvider>(),
                Request(Tag("Representation", "Full")),
                new BuildContentRequirement("group", BuildContentCardinality.OneOrMore),
                new BuildContentRequirement("group", BuildContentCardinality.OneOrMore));
            var conflict = Select(new[] { candidate }, Array.Empty<IBuildTagProvider>(),
                Request(Tag("Representation", "Full")),
                new BuildContentRequirement("group", BuildContentCardinality.OneOrMore),
                new BuildContentRequirement("group", BuildContentCardinality.ExactlyOne));

            AssertError(duplicate, BuildValidationCode.DuplicateRequirement);
            AssertError(conflict, BuildValidationCode.ConflictingRequirement);
        }

        [Test]
        public void AllSuccessInputOrders_DoNotChangeFullSnapshot()
        {
            var candidates = new[]
            {
                CandidateWithProperties("b", "world", "physical-b", false),
                CandidateWithProperties("a", "world", "physical-a", false)
            };
            var providers = new IBuildTagProvider[]
            {
                new FakeProvider("z", candidate => new[] { Tag("Season", candidate.StableKey == "a" ? "Spring" : "Summer") }),
                new FakeProvider("a", candidate => new[]
                {
                    Tag("Representation", "Whitebox"),
                    Tag("Season", candidate.StableKey == "a" ? "Spring" : "Summer")
                })
            };
            var forward = Select(candidates, providers,
                Request(Tag("Season", "Spring"), Tag("Representation", "Whitebox")));
            var reversedCandidates = new[]
            {
                CandidateWithProperties("a", "world", "physical-a", true),
                CandidateWithProperties("b", "world", "physical-b", true)
            };
            var reversedProviders = new IBuildTagProvider[]
            {
                new FakeProvider("a", candidate => new[]
                {
                    Tag("Season", candidate.StableKey == "a" ? "Spring" : "Summer"),
                    Tag("Representation", "Whitebox")
                }),
                new FakeProvider("z", candidate => new[] { Tag("Season", candidate.StableKey == "a" ? "Spring" : "Summer") })
            };
            var reverse = Select(reversedCandidates, reversedProviders,
                Request(Tag("Representation", "Whitebox"), Tag("Season", "Spring")));

            Assert.That(Snapshot(reverse), Is.EqualTo(Snapshot(forward)));
            Assert.That(forward.Issues.Any(issue => issue.Severity == BuildValidationSeverity.Warning), Is.True);
            Assert.That(forward.Plan!.ExcludedContent, Has.Count.EqualTo(1));
        }

        [Test]
        public void AllErrorInputOrders_DoNotChangeFullSnapshot()
        {
            var candidates = new[]
            {
                Candidate("duplicate", "first", "physical-a"),
                Candidate("duplicate", "second", "physical-b")
            };
            var forward = Select(
                candidates,
                new[] { new FakeProvider("provider", _ => new[] { Tag("Unknown", "value"), Tag("Season", "Autumn") }) },
                Request(Tag("Unknown", "value"), Tag("Season", "Autumn")));
            var reverse = Select(
                candidates.Reverse(),
                new[] { new FakeProvider("provider", _ => new[] { Tag("Season", "Autumn"), Tag("Unknown", "value") }) },
                Request(Tag("Season", "Autumn"), Tag("Unknown", "value")));

            Assert.That(forward.Plan, Is.Null);
            Assert.That(forward.Issues.Count, Is.GreaterThan(1));
            Assert.That(Snapshot(reverse), Is.EqualTo(Snapshot(forward)));
        }

        [Test]
        public void ProviderBufferAndInputCollections_AreDefensivelyCopied()
        {
            var mutableTags = new List<BuildTag> { Tag("Season", "Spring") };
            var mutableProperties = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("revision", "1")
            };
            var candidate = new BuildContentCandidate("candidate", "world", "physical",
                new BuildProvenance("source", "id", mutableProperties));
            var result = Select(new[] { candidate },
                new[] { new FakeProvider("provider", _ => mutableTags) },
                Request(Tag("Season", "Spring")));

            mutableTags[0] = Tag("Season", "Summer");
            mutableProperties[0] = new KeyValuePair<string, string>("revision", "2");

            Assert.That(result.Plan!.SelectedContent, Has.Count.EqualTo(1));
            Assert.That(result.Plan.SelectedContent[0].Provenance.Properties[0].Value, Is.EqualTo("1"));
        }

        // ═══ 宣言からの到達（BS5a）で足した policy: requirement の無い候補と、選ばれなかった表現の除外 ═══

        [Test]
        public void ExcludeUnrequired_CandidateWithoutRequirement_IsUnreachableExclusion()
        {
            var result = SelectExcludingUnrequired(
                new[] { Candidate("kept", "required", "p1"), Candidate("dropped", "elsewhere", "p2") },
                new[] { new FakeProvider("provider", _ => new[] { Tag("Representation", "Full") }) },
                Request(Tag("Representation", "Full")),
                new BuildContentRequirement("required", BuildContentCardinality.ExactlyOne, "Full"));

            Assert.That(result.IsSuccess, Is.True, Snapshot(result));
            Assert.That(result.Plan!.SelectedContent.Select(item => item.StableKey), Is.EqualTo(new[] { "kept" }));
            var exclusion = result.Plan.ExcludedContent.Single();
            Assert.That(exclusion.Candidate.StableKey, Is.EqualTo("dropped"));
            Assert.That(exclusion.ReasonCode, Is.EqualTo(BuildExclusionReasonCode.Unreachable));
            Assert.That(exclusion.Dimension, Is.EqualTo("LogicalKey"));
            Assert.That(exclusion.Value, Is.EqualTo("elsewhere"));
        }

        [Test]
        public void ExcludeUnrequired_RepresentationNotInRequirement_IsRepresentationNotPreferred()
        {
            // request は Full と Whitebox の両方を許すが、requirement は Whitebox だけ。Full は tag では落ちず requirement で落ちる。
            var result = SelectExcludingUnrequired(
                new[] { Candidate("cell/full", "cell", "p1"), Candidate("cell/whitebox", "cell", "p2") },
                new[] { new FakeProvider("provider", candidate => new[] { Tag("Representation", candidate.StableKey.EndsWith("/full", StringComparison.Ordinal) ? "Full" : "Whitebox") }) },
                Request(Tag("Representation", "Full"), Tag("Representation", "Whitebox")),
                new BuildContentRequirement("cell", BuildContentCardinality.ExactlyOne, "Whitebox"));

            Assert.That(result.IsSuccess, Is.True, Snapshot(result));
            Assert.That(result.Plan!.SelectedContent.Select(item => item.StableKey), Is.EqualTo(new[] { "cell/whitebox" }));
            var exclusion = result.Plan.ExcludedContent.Single();
            Assert.That(exclusion.ReasonCode, Is.EqualTo(BuildExclusionReasonCode.RepresentationNotPreferred));
            Assert.That(exclusion.Dimension, Is.EqualTo("Representation"));
            Assert.That(exclusion.Value, Is.EqualTo("Full"));
        }

        [Test]
        public void RepresentationRequirement_CountsPerRepresentation()
        {
            var provider = new FakeProvider("provider", candidate => new[] { Tag("Representation", candidate.StableKey.Split('/').Last()) });
            var request = Request(Tag("Representation", "Full"), Tag("Representation", "Whitebox"));

            // 宣言した表現の候補が無い: 別の表現があっても RequiredGroupMissing（CardinalityViolation ではない）。
            var missing = SelectExcludingUnrequired(new[] { Candidate("cell/Full", "cell", "p1") }, new[] { provider }, request,
                new BuildContentRequirement("cell", BuildContentCardinality.ExactlyOne, "Whitebox"));
            AssertError(missing, BuildValidationCode.RequiredGroupMissing);
            Assert.That(missing.Issues.Single(issue => issue.Code == BuildValidationCode.RequiredGroupMissing).Value, Is.EqualTo("Whitebox"));
            Assert.That(missing.Issues.Any(issue => issue.Code == BuildValidationCode.CardinalityViolation), Is.False);

            // 同じ表現の候補が 2 つ: CardinalityViolation。
            var twice = SelectExcludingUnrequired(new[] { Candidate("cell/Whitebox", "cell", "p1"), Candidate("cell2/Whitebox", "cell", "p2") },
                new[] { provider }, request, new BuildContentRequirement("cell", BuildContentCardinality.ExactlyOne, "Whitebox"));
            AssertError(twice, BuildValidationCode.CardinalityViolation);

            // 両表現を 1 つずつ要求（All）: 両方選ばれ、issue は無い。
            var both = SelectExcludingUnrequired(new[] { Candidate("cell/Full", "cell", "p1"), Candidate("cell/Whitebox", "cell", "p2") },
                new[] { provider }, request,
                new BuildContentRequirement("cell", BuildContentCardinality.ExactlyOne, "Full"),
                new BuildContentRequirement("cell", BuildContentCardinality.ExactlyOne, "Whitebox"));
            Assert.That(both.IsSuccess, Is.True, Snapshot(both));
            Assert.That(both.Plan!.SelectedContent.Count, Is.EqualTo(2));
        }

        [Test]
        public void RepresentationRequirements_SameKeyDifferentRepresentation_AreNotDuplicates()
        {
            var candidate = Candidate("group/Full", "group", "physical");
            var provider = new FakeProvider("provider", _ => new[] { Tag("Representation", "Full") });
            var distinct = SelectExcludingUnrequired(new[] { candidate }, new[] { provider }, Request(Tag("Representation", "Full")),
                new BuildContentRequirement("group", BuildContentCardinality.ExactlyOne, "Full"),
                new BuildContentRequirement("group", BuildContentCardinality.ExactlyOne, "Whitebox"));
            Assert.That(distinct.Issues.Any(issue => issue.Code == BuildValidationCode.DuplicateRequirement), Is.False);

            var duplicate = SelectExcludingUnrequired(new[] { candidate }, new[] { provider }, Request(Tag("Representation", "Full")),
                new BuildContentRequirement("group", BuildContentCardinality.ExactlyOne, "Full"),
                new BuildContentRequirement("group", BuildContentCardinality.ExactlyOne, "Full"));
            AssertError(duplicate, BuildValidationCode.DuplicateRequirement);
        }

        [Test]
        public void Policy_RepresentationRequirementWithoutDimension_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new BuildSelectionPolicy(Schema,
                new[] { new BuildContentRequirement("group", BuildContentCardinality.ExactlyOne, "Full") }, false, null));
            Assert.Throws<ArgumentException>(() => new BuildSelectionPolicy(Schema, null, true, null));
        }

        private static BuildPlanResult SelectExcludingUnrequired(
            IEnumerable<BuildContentCandidate> candidates,
            IEnumerable<IBuildTagProvider> providers,
            BuildRequest request,
            params BuildContentRequirement[] requirements)
        {
            return new BuildTagSelector().Select(request, candidates, providers,
                new BuildSelectionPolicy(Schema, requirements, true, "Representation"));
        }

        private static BuildPlanResult Select(
            IEnumerable<BuildContentCandidate> candidates,
            IEnumerable<IBuildTagProvider> providers,
            BuildRequest request,
            params BuildContentRequirement[] requirements)
        {
            return new BuildTagSelector().Select(request, candidates, providers,
                new BuildSelectionPolicy(Schema, requirements));
        }

        private static void AssertError(BuildPlanResult result, BuildValidationCode code)
        {
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.Issues.Any(issue => issue.Code == code
                && issue.Severity == BuildValidationSeverity.Error), Is.True);
        }

        private static string Snapshot(BuildPlanResult result)
        {
            var planSnapshot = result.Plan == null
                ? "PLAN:null"
                : "PLAN:" + string.Join("|", result.Plan.Request.Selections.Select(tag =>
                        $"R:{tag.Dimension}:{tag.Value}"))
                    + "|" + string.Join("|", result.Plan.SelectedContent.Select(candidate =>
                        "S:" + CandidateSnapshot(candidate)))
                    + "|" + string.Join("|", result.Plan.ExcludedContent.Select(exclusion =>
                        $"X:{CandidateSnapshot(exclusion.Candidate)}:{exclusion.ReasonCode}:{exclusion.Dimension}:{exclusion.Value}"));
            return planSnapshot + "|" + string.Join("|", result.Issues.Select(issue =>
                    $"I:{issue.Severity}:{issue.Code}:{issue.Subject}:{issue.SubjectKey}:{issue.Dimension}:{issue.Value}:{issue.ProviderKey}"));
        }

        private static string CandidateSnapshot(BuildContentCandidate candidate) =>
            $"{candidate.StableKey}:{candidate.LogicalKey}:{candidate.PhysicalKey}:" +
            $"{candidate.Provenance.SourceKind}:{candidate.Provenance.SourceId}:" +
            string.Join(",", candidate.Provenance.Properties.Select(pair => $"{pair.Key}={pair.Value}"));

        private static BuildRequest Request(params BuildTag[] tags) => new BuildRequest(tags);

        private static BuildContentCandidate Candidate(string stable, string logical, string physical) =>
            new BuildContentCandidate(stable, logical, physical,
                new BuildProvenance("test", stable, new[] { new KeyValuePair<string, string>("key", stable) }));

        private static BuildContentCandidate CandidateWithProperties(
            string stable,
            string logical,
            string physical,
            bool reverse)
        {
            var properties = new[]
            {
                new KeyValuePair<string, string>("alpha", "1"),
                new KeyValuePair<string, string>("omega", "2")
            };
            return new BuildContentCandidate(stable, logical, physical,
                new BuildProvenance("test-kind", "source-" + stable,
                    reverse ? properties.Reverse() : properties));
        }

        private static BuildTag Tag(string? dimension, string? value) => new BuildTag(dimension, value);

        private static KeyValuePair<string, IEnumerable<string>> Dimension(string name, params string[] values) =>
            new KeyValuePair<string, IEnumerable<string>>(name, values);

        private static IReadOnlyList<BuildTag> TagsFor(string? shape, string? values)
        {
            if (shape == null) return Array.Empty<BuildTag>();
            if (shape == "Season") return new[] { Tag("Season", values!) };
            if (shape == "Representation") return new[] { Tag("Representation", values!) };
            return new[]
            {
                Tag("Season", values == "WrongSeason" ? "Summer" : "Spring"),
                Tag("Representation", values == "WrongRepresentation" ? "Full" : "Whitebox")
            };
        }

        private sealed class FakeProvider : IBuildTagProvider
        {
            private readonly Func<BuildContentCandidate, IReadOnlyList<BuildTag>> _provide;

            public FakeProvider(string stableProviderKey, Func<BuildContentCandidate, IReadOnlyList<BuildTag>> provide)
            {
                StableProviderKey = stableProviderKey;
                _provide = provide;
            }

            public string StableProviderKey { get; }

            public IReadOnlyList<BuildTag> GetTags(BuildContentCandidate candidate) => _provide(candidate);
        }
    }
}
