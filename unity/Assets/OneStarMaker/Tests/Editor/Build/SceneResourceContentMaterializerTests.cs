#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class SceneResourceContentMaterializerTests
    {
        private const string GuidA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private readonly List<UnityEngine.Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in _objects) if (value != null) UnityEngine.Object.DestroyImmediate(value);
            _objects.Clear();
        }

        [Test]
        public void Materialize_EmptyVariant_ProducesFullCandidateAndClosure()
        {
            var map = CreateMap(CreateResource("Scene:A", new AssetPayload(string.Empty, new AssetReference(GuidA))));
            var result = new SceneResourceContentMaterializer(new FakeGateway(GuidA, "Assets/A.unity")).Materialize(map);

            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.Snapshot, Is.Not.Null);
            var snapshot = result.Snapshot!;
            Assert.That(snapshot.Candidates.Single().LogicalKey, Is.EqualTo("Scene:A"));
            Assert.That(snapshot.Candidates.Single().PhysicalKey, Is.EqualTo(GuidA));
            Assert.That(snapshot.Candidates.Single().StableKey, Does.StartWith("osm-build-candidate-v1|"));
            Assert.That(snapshot.Dependencies.Single().Entries.Select(x => x.Path), Is.EqualTo(new[] { "Assets/A.unity" }));
            Assert.That(snapshot.Requirements.Single().Cardinality, Is.EqualTo(BuildContentCardinality.ExactlyOne));

            var policy = new BuildSelectionPolicy(new BuildTagSchema(new[]
            {
                new KeyValuePair<string, IEnumerable<string>>("Representation", new[] { "Full", "Whitebox" })
            }), snapshot.Requirements);
            var selection = new BuildTagSelector().Select(new BuildRequest(new[] { new BuildTag("Representation", "Full") }),
                snapshot.Candidates, snapshot.TagProviders, policy);
            Assert.That(selection.IsSuccess, Is.True);
            Assert.That(selection.Plan!.SelectedContent.Count, Is.EqualTo(1));
        }

        [Test]
        public void Materialize_InvalidIdentity_ReturnsStructuredIssueWithoutSnapshot()
        {
            var map = CreateMap(CreateResource(" bad ", new AssetPayload(string.Empty, new AssetReference(GuidA))));
            var result = new SceneResourceContentMaterializer(new FakeGateway(GuidA, "Assets/A.unity")).Materialize(map);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Issues.Select(x => x.Code), Does.Contain(BuildMaterializationIssueCode.InvalidResourceIdentity));
        }

        [Test]
        public void Materialize_ReversedResources_ProducesSameCanonicalCandidates()
        {
            const string guidB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var first = CreateResource("A", new AssetPayload("Whitebox", new AssetReference(GuidA)));
            var second = CreateResource("B", new AssetPayload(string.Empty, new AssetReference(guidB)));
            var gateway = new FakeGateway(GuidA, "Assets/A.unity", guidB, "Assets/B.unity");
            var left = new SceneResourceContentMaterializer(gateway).Materialize(CreateMap(first, second)).Snapshot!;
            var right = new SceneResourceContentMaterializer(gateway).Materialize(CreateMap(second, first)).Snapshot!;
            Assert.That(right.Candidates.Select(x => x.StableKey), Is.EqualTo(left.Candidates.Select(x => x.StableKey)));
            Assert.That(right.Dependencies.Select(x => x.RootGuid), Is.EqualTo(left.Dependencies.Select(x => x.RootGuid)));
        }

        [Test]
        public void Materialize_SharedPhysicalGuid_ReturnsCollision()
        {
            var map = CreateMap(
                CreateResource("A", new AssetPayload(string.Empty, new AssetReference(GuidA))),
                CreateResource("B", new AssetPayload(string.Empty, new AssetReference(GuidA))));
            var result = new SceneResourceContentMaterializer(new FakeGateway(GuidA, "Assets/A.unity")).Materialize(map);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Issues.Select(x => x.Code), Does.Contain(BuildMaterializationIssueCode.PhysicalKeyCollision));
        }

        [Test]
        public void StableKey_LengthPrefix_DoesNotCollideForDelimiterValues()
        {
            var left = ScenePayloadMappingPolicy.StableKey("a:1", "b", GuidA);
            var right = ScenePayloadMappingPolicy.StableKey("a", "1:b", GuidA);
            Assert.That(left, Is.Not.EqualTo(right));
            Assert.That(ScenePayloadMappingPolicy.StableKey("雪", "Full", GuidA), Does.Contain("1:雪"));
        }

        [TestCase("Assets/Foo.prefab", true)]
        [TestCase("Assets/Foo.cs", false)]
        [TestCase("Assets/Foo.asmref", false)]
        [TestCase("Assets/Foo.meta", false)]
        [TestCase("Assets/Resources/unity_builtin_extra.prefab", true)]
        [TestCase("Assets/Library/unity default resources.prefab", true)]
        [TestCase("Packages/Foo.prefab", false)]
        [TestCase("Other/Foo.prefab", false)]
        public void DependencyFilter_UsesFrozenRules(string path, bool expected) =>
            Assert.That(AssetDependencySnapshotBuilder.IsContent(path), Is.EqualTo(expected));

        [Test]
        public void Materialize_UppercaseGuid_CanonicalizesPhysicalKey()
        {
            var upper = GuidA.ToUpperInvariant();
            var map = CreateMap(CreateResource("A", new AssetPayload(string.Empty, new AssetReference(upper))));
            var result = new SceneResourceContentMaterializer(new FakeGateway(GuidA, "Assets/A.unity")).Materialize(map);
            Assert.That(result.Snapshot!.Candidates.Single().PhysicalKey, Is.EqualTo(GuidA));
        }

        [Test]
        public void Materialize_NullVariant_ReturnsStructuredIssue()
        {
            var payload = new AssetPayload(null!, new AssetReference(GuidA));
            var result = new SceneResourceContentMaterializer(new FakeGateway(GuidA, "Assets/A.unity"))
                .Materialize(CreateMap(CreateResource("A", payload)));
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Issues.Select(x => x.Code), Does.Contain(BuildMaterializationIssueCode.InvalidVariant));
        }

        [Test]
        public void Materialize_DuplicateLogicalIdentity_ReturnsStructuredIssue()
        {
            var result = new SceneResourceContentMaterializer(new FakeGateway(GuidA, "Assets/A.unity"))
                .Materialize(CreateMap(
                    CreateResource("A", new AssetPayload(string.Empty, new AssetReference(GuidA))),
                    CreateResource("A", new AssetPayload("Whitebox", new AssetReference("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")))));
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(result.Issues.Select(x => x.Code), Does.Contain(BuildMaterializationIssueCode.DuplicateLogicalKey));
        }

        [Test]
        public void Materialize_DuplicateIdentityErrorIssues_AreResourceOrderIndependent()
        {
            var valid = CreateResource("A", new AssetPayload(string.Empty, new AssetReference(GuidA)));
            var missing = CreateResource("A", new AssetPayload(string.Empty, null!));
            var gateway = new FakeGateway(GuidA, "Assets/A.unity");
            var first = new SceneResourceContentMaterializer(gateway).Materialize(CreateMap(valid, missing));
            var second = new SceneResourceContentMaterializer(gateway).Materialize(CreateMap(missing, valid));
            Assert.That(first.Snapshot, Is.Null);
            Assert.That(second.Issues.Select(ProjectIssue), Is.EqualTo(first.Issues.Select(ProjectIssue)));
            Assert.That(first.Issues.Select(x => x.Code), Does.Contain(BuildMaterializationIssueCode.MissingReference));
        }

        [Test]
        public void Materialize_SharedGuidErrorIssues_ArePayloadOrderIndependent()
        {
            var full = new AssetPayload(string.Empty, new AssetReference(GuidA));
            var whitebox = new AssetPayload("Whitebox", new AssetReference(GuidA));
            var gateway = new FakeGateway(GuidA, "Assets/A.unity");
            var first = new SceneResourceContentMaterializer(gateway).Materialize(CreateMap(CreateResource("A", full, whitebox)));
            var second = new SceneResourceContentMaterializer(gateway).Materialize(CreateMap(CreateResource("A", whitebox, full)));
            Assert.That(first.Snapshot, Is.Null);
            Assert.That(second.Issues.Select(ProjectIssue), Is.EqualTo(first.Issues.Select(ProjectIssue)));
            Assert.That(first.Issues.Select(x => x.Code), Does.Contain(BuildMaterializationIssueCode.PhysicalKeyCollision));
        }

        [Test]
        public void MaterializationResult_SortsIssueNamesOrdinalAndDeduplicates()
        {
            var issues = new[]
            {
                new BuildMaterializationIssue(BuildMaterializationIssueCode.NullResource, BuildMaterializationSubject.Resource, "x"),
                new BuildMaterializationIssue(BuildMaterializationIssueCode.InvalidResourceIdentity, BuildMaterializationSubject.Resource, "x"),
                new BuildMaterializationIssue(BuildMaterializationIssueCode.NullResource, BuildMaterializationSubject.Resource, "x")
            };
            var result = new BuildMaterializationResult(null, issues);
            Assert.That(result.Issues.Select(x => x.Code), Is.EqualTo(new[]
            {
                BuildMaterializationIssueCode.InvalidResourceIdentity,
                BuildMaterializationIssueCode.NullResource
            }));
        }

        [Test]
        public void SnapshotTagProvider_DefensivelyCopiesTagBuffers()
        {
            var candidate = ScenePayloadMappingPolicy.Candidate("A", "Full", GuidA, "Assets/A.unity");
            var tagBuffer = new List<BuildTag> { new BuildTag("Representation", "Full") };
            var tags = new Dictionary<string, IReadOnlyList<BuildTag>> { [candidate.StableKey] = tagBuffer };
            var snapshot = new BuildMaterializationSnapshot(new[] { candidate }, tags,
                new[] { new BuildContentRequirement("A", BuildContentCardinality.ExactlyOne) },
                new[] { new BuildDependencySnapshot(GuidA, "Assets/A.unity", new[] { new BuildDependencyEntry(GuidA, "Assets/A.unity") }) });
            tagBuffer[0] = new BuildTag("Representation", "Whitebox");
            Assert.That(snapshot.TagProviders.Single().GetTags(candidate).Single().Value, Is.EqualTo("Full"));
        }

        [Test]
        public void Selector_WhiteboxAndUnknownValue_UseExistingBs1Contract()
        {
            var guidB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var gateway = new FakeGateway(GuidA, "Assets/A.unity", guidB, "Assets/B.unity");
            var snapshot = new SceneResourceContentMaterializer(gateway).Materialize(CreateMap(
                CreateResource("A", new AssetPayload("Whitebox", new AssetReference(GuidA))),
                CreateResource("B", new AssetPayload("Future", new AssetReference(guidB))))).Snapshot!;
            var policy = new BuildSelectionPolicy(new BuildTagSchema(new[]
            {
                new KeyValuePair<string, IEnumerable<string>>("Representation", new[] { "Full", "Whitebox" })
            }), snapshot.Requirements);
            var result = new BuildTagSelector().Select(new BuildRequest(new[] { new BuildTag("Representation", "Whitebox") }),
                snapshot.Candidates, snapshot.TagProviders, policy);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Issues.Select(x => x.Code), Does.Contain(BuildValidationCode.UnknownValue));
        }

        [Test]
        public void Materialize_PayloadAndDependencyOrder_ProducesSameFullProjection()
        {
            const string guidB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var forwardMap = CreateMap(CreateResource("A",
                new AssetPayload(string.Empty, new AssetReference(GuidA)),
                new AssetPayload("Whitebox", new AssetReference(guidB))));
            var reverseMap = CreateMap(CreateResource("A",
                new AssetPayload("Whitebox", new AssetReference(guidB)),
                new AssetPayload(string.Empty, new AssetReference(GuidA))));
            var forward = new SceneResourceContentMaterializer(
                new FakeGateway(GuidA, "Assets/A.unity", guidB, "Assets/B.unity")).Materialize(forwardMap).Snapshot!;
            var reverse = new SceneResourceContentMaterializer(
                new FakeGateway(true, GuidA, "Assets/A.unity", guidB, "Assets/B.unity")).Materialize(reverseMap).Snapshot!;
            Assert.That(Project(reverse), Is.EqualTo(Project(forward)));
        }

        [Test]
        public void Materialize_FixedSeedPermutation_PreservesFullProjectionAndIssues()
        {
            const string guidB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            const string guidC = "cccccccccccccccccccccccccccccccc";
            var pairs = new[] { GuidA, "Assets/A.unity", guidB, "Assets/B.unity", guidC, "Assets/C.unity" };
            var resources = new[]
            {
                CreateResource("A", new AssetPayload("Whitebox", new AssetReference(GuidA)),
                    new AssetPayload(string.Empty, new AssetReference(guidB))),
                CreateResource("B", new AssetPayload(string.Empty, new AssetReference(guidC)))
            };
            var baseline = new SceneResourceContentMaterializer(new FakeGateway(pairs)).Materialize(CreateMap(resources));
            var random = new System.Random(12345);
            var seededOrder = resources.OrderBy(_ => random.Next()).ToArray();
            var permuted = new SceneResourceContentMaterializer(new FakeGateway(true, pairs)).Materialize(CreateMap(seededOrder));
            Assert.That(Project(permuted.Snapshot!), Is.EqualTo(Project(baseline.Snapshot!)));
            Assert.That(permuted.Issues.Select(ProjectIssue), Is.EqualTo(baseline.Issues.Select(ProjectIssue)));
        }

        [Test]
        public void Materialize_GatewayFailureAndUnresolvedDependency_AreStructured()
        {
            var map = CreateMap(CreateResource("A", new AssetPayload(string.Empty, new AssetReference(GuidA))));
            var broken = new FakeGateway(GuidA, "Assets/A.unity") { ThrowOnDependencies = true };
            var failure = new SceneResourceContentMaterializer(broken).Materialize(map);
            Assert.That(failure.Snapshot, Is.Null);
            Assert.That(failure.Issues.Select(x => x.Code), Does.Contain(BuildMaterializationIssueCode.GatewayFailure));

            var missing = new FakeGateway(GuidA, "Assets/A.unity") { ExtraDependency = "Assets/Missing.prefab" };
            var unresolved = new SceneResourceContentMaterializer(missing).Materialize(map);
            Assert.That(unresolved.Snapshot, Is.Null);
            Assert.That(unresolved.Issues.Select(x => x.Code), Does.Contain(BuildMaterializationIssueCode.MissingDependencyAsset));
        }

        [Test]
        public void Materialize_RoundTripMismatchAndMissingRoot_AreStructured()
        {
            var map = CreateMap(CreateResource("A", new AssetPayload(string.Empty, new AssetReference(GuidA))));
            var mismatch = new FakeGateway(GuidA, "Assets/A.unity") { ReturnWrongGuid = true };
            Assert.That(new SceneResourceContentMaterializer(mismatch).Materialize(map).Issues.Select(x => x.Code),
                Does.Contain(BuildMaterializationIssueCode.RootGuidRoundTripMismatch));
            var missing = new FakeGateway(GuidA, "Assets/A.unity") { RootMissing = true };
            Assert.That(new SceneResourceContentMaterializer(missing).Materialize(map).Issues.Select(x => x.Code),
                Does.Contain(BuildMaterializationIssueCode.MissingRootAsset));
        }

        [Test]
        public void Materialize_CrossRootDependencyGuidCollision_FailsDeterministically()
        {
            const string guidB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            const string sharedGuid = "cccccccccccccccccccccccccccccccc";
            var rootA = CreateResource("A", new AssetPayload(string.Empty, new AssetReference(GuidA)));
            var rootB = CreateResource("B", new AssetPayload(string.Empty, new AssetReference(guidB)));
            var gateway = new CrossRootCollisionGateway(GuidA, guidB, sharedGuid);
            var first = new SceneResourceContentMaterializer(gateway).Materialize(CreateMap(rootA, rootB));
            var second = new SceneResourceContentMaterializer(gateway).Materialize(CreateMap(rootB, rootA));
            Assert.That(first.Snapshot, Is.Null);
            Assert.That(second.Snapshot, Is.Null);
            Assert.That(first.Issues.Select(ProjectIssue), Is.EqualTo(second.Issues.Select(ProjectIssue)));
            Assert.That(first.Issues.Select(x => x.Code), Does.Contain(BuildMaterializationIssueCode.DependencyIdentityCollision));
        }

        [Test]
        public void Selector_WhiteboxRequest_SelectsWhiteboxCandidate()
        {
            var snapshot = new SceneResourceContentMaterializer(new FakeGateway(GuidA, "Assets/A.unity"))
                .Materialize(CreateMap(CreateResource("A", new AssetPayload("Whitebox", new AssetReference(GuidA))))).Snapshot!;
            var result = Select(snapshot, "Whitebox", "Full", "Whitebox");
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Plan!.SelectedContent.Single().PhysicalKey, Is.EqualTo(GuidA));
        }

        [Test]
        public void Selector_NeutralSupplement_RemainsSelected()
        {
            var snapshot = new SceneResourceContentMaterializer(new FakeGateway(GuidA, "Assets/A.unity"))
                .Materialize(CreateMap(CreateResource("A", new AssetPayload(string.Empty, new AssetReference(GuidA))))).Snapshot!;
            var neutral = new BuildContentCandidate("neutral", "optional", "neutral-physical",
                new BuildProvenance("test", "neutral"));
            var policy = new BuildSelectionPolicy(new BuildTagSchema(new[]
            {
                new KeyValuePair<string, IEnumerable<string>>("Representation", new[] { "Full" })
            }), snapshot.Requirements);
            var result = new BuildTagSelector().Select(new BuildRequest(new[] { new BuildTag("Representation", "Full") }),
                snapshot.Candidates.Concat(new[] { neutral }), snapshot.TagProviders, policy);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Plan!.SelectedContent.Select(x => x.StableKey), Does.Contain("neutral"));
        }

        [Test]
        public void Selector_ExactlyOne_RejectsZeroAndMultipleSelections()
        {
            const string guidB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            var zero = new SceneResourceContentMaterializer(new FakeGateway(GuidA, "Assets/A.unity"))
                .Materialize(CreateMap(CreateResource("A", new AssetPayload(string.Empty, new AssetReference(GuidA))))).Snapshot!;
            Assert.That(Select(zero, "Whitebox", "Full", "Whitebox").Issues.Select(x => x.Code),
                Does.Contain(BuildValidationCode.CardinalityViolation));

            var multiple = new SceneResourceContentMaterializer(new FakeGateway(GuidA, "Assets/A.unity", guidB, "Assets/B.unity"))
                .Materialize(CreateMap(CreateResource("A",
                    new AssetPayload(string.Empty, new AssetReference(GuidA)),
                    new AssetPayload(string.Empty, new AssetReference(guidB))))).Snapshot!;
            Assert.That(Select(multiple, "Full", "Full", "Whitebox").Issues.Select(x => x.Code),
                Does.Contain(BuildValidationCode.CardinalityViolation));
        }

        private static BuildPlanResult Select(BuildMaterializationSnapshot snapshot, string selected, params string[] allowed)
        {
            var policy = new BuildSelectionPolicy(new BuildTagSchema(new[]
            {
                new KeyValuePair<string, IEnumerable<string>>("Representation", allowed)
            }), snapshot.Requirements);
            return new BuildTagSelector().Select(new BuildRequest(new[] { new BuildTag("Representation", selected) }),
                snapshot.Candidates, snapshot.TagProviders, policy);
        }

        private static string[] Project(BuildMaterializationSnapshot snapshot)
        {
            var lines = new List<string>();
            lines.AddRange(snapshot.Candidates.Select(x => $"C|{x.StableKey}|{x.LogicalKey}|{x.PhysicalKey}|{x.Provenance.SourceKind}|{x.Provenance.SourceId}|{string.Join(",", x.Provenance.Properties.Select(p => p.Key + "=" + p.Value))}"));
            lines.AddRange(snapshot.Candidates.SelectMany(candidate => snapshot.TagProviders.SelectMany(provider =>
                provider.GetTags(candidate).Select(tag => $"T|{provider.StableProviderKey}|{candidate.StableKey}|{tag.Dimension}|{tag.Value}"))));
            lines.AddRange(snapshot.Requirements.Select(x => $"R|{x.LogicalKey}|{x.Cardinality}"));
            lines.AddRange(snapshot.Dependencies.SelectMany(x => x.Entries.Select(e => $"D|{x.RootGuid}|{x.RootPath}|{e.Guid}|{e.Path}")));
            return lines.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        private static string ProjectIssue(BuildMaterializationIssue x) =>
            $"{x.Code}|{x.Subject}|{x.SubjectKey}|{x.RootGuid}|{x.Path}|{x.DetailKey}";

        private SceneResourceMap CreateMap(params SceneResource[] resources)
        {
            var map = ScriptableObject.CreateInstance<SceneResourceMap>();
            _objects.Add(map);
            Set(map, "_sceneResources", new List<SceneResource>(resources));
            return map;
        }

        private SceneResource CreateResource(string identity, params AssetPayload[] payloads)
        {
            var resource = ScriptableObject.CreateInstance<SceneResource>();
            _objects.Add(resource);
            resource.Identity = identity;
            Set(resource, "_sceneAssetDescription", new SceneAssetDescription(identity, LoadType.OnDemand, payloads.ToList()));
            return resource;
        }

        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

        private sealed class FakeGateway : IAssetDatabaseGateway
        {
            private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
            private readonly bool _reverseDependencies;
            public bool ThrowOnDependencies { get; set; }
            public bool ReturnWrongGuid { get; set; }
            public bool RootMissing { get; set; }
            public string? ExtraDependency { get; set; }
            public FakeGateway(params string[] guidPathPairs) : this(false, guidPathPairs) { }
            public FakeGateway(bool reverseDependencies, params string[] guidPathPairs)
            {
                _reverseDependencies = reverseDependencies;
                for (var i = 0; i < guidPathPairs.Length; i += 2) _paths.Add(guidPathPairs[i], guidPathPairs[i + 1]);
            }
            public string GuidToPath(string guid) => _paths.TryGetValue(guid, out var path) ? path : string.Empty;
            public string PathToGuid(string path) => ReturnWrongGuid ? "dddddddddddddddddddddddddddddddd"
                : _paths.SingleOrDefault(x => x.Value == path).Key ?? string.Empty;
            public string[] GetDependencies(string path)
            {
                if (ThrowOnDependencies) throw new InvalidOperationException("gateway fixture");
                var values = _paths.Values.ToArray();
                if (_reverseDependencies) Array.Reverse(values);
                return ExtraDependency == null ? values : values.Concat(new[] { ExtraDependency }).ToArray();
            }
            public bool FileExists(string path) => !RootMissing && _paths.ContainsValue(path);
            public bool IsFolder(string path) => false;
        }

        private sealed class CrossRootCollisionGateway : IAssetDatabaseGateway
        {
            private readonly string _a;
            private readonly string _b;
            private readonly string _shared;
            public CrossRootCollisionGateway(string a, string b, string shared)
            {
                _a = a;
                _b = b;
                _shared = shared;
            }
            public string GuidToPath(string guid) => guid == _a ? "Assets/A.unity"
                : guid == _b ? "Assets/B.unity" : string.Empty;
            public string PathToGuid(string path) => path == "Assets/A.unity" ? _a
                : path == "Assets/B.unity" ? _b
                : path == "Assets/X.prefab" || path == "Assets/Y.prefab" ? _shared : string.Empty;
            public string[] GetDependencies(string path) => path == "Assets/A.unity"
                ? new[] { "Assets/A.unity", "Assets/X.prefab" }
                : new[] { "Assets/B.unity", "Assets/Y.prefab" };
            public bool FileExists(string path) => path == "Assets/A.unity" || path == "Assets/B.unity"
                || path == "Assets/X.prefab" || path == "Assets/Y.prefab";
            public bool IsFolder(string path) => false;
        }
    }
}
