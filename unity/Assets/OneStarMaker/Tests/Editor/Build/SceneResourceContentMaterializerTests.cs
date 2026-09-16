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
            public FakeGateway(params string[] guidPathPairs)
            {
                for (var i = 0; i < guidPathPairs.Length; i += 2) _paths.Add(guidPathPairs[i], guidPathPairs[i + 1]);
            }
            public string GuidToPath(string guid) => _paths.TryGetValue(guid, out var path) ? path : string.Empty;
            public string PathToGuid(string path) => _paths.SingleOrDefault(x => x.Value == path).Key ?? string.Empty;
            public string[] GetDependencies(string path) => new[] { path };
            public bool FileExists(string path) => _paths.ContainsValue(path);
            public bool IsFolder(string path) => false;
        }
    }
}
