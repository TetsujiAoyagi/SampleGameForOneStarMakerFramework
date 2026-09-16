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
            Assert.That(selection.HasErrors, Is.False);
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
