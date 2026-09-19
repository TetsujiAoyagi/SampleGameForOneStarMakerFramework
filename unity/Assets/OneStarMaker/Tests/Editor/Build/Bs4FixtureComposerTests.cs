#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;
using SampleGame.DependOnAll.Editor.Build;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class Bs4FixtureComposerTests
    {
        [Test]
        public void Compose_PreservesProductionAndAddsSelectedFixture()
        {
            var source = Candidate("scene/full", "Title", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { source }, Array.Empty<BuildContentExclusion>());
            var tags = new Dictionary<string, IReadOnlyList<BuildTag>> { [source.StableKey] = new[] { new BuildTag("Representation", "Full") } };
            var snapshot = new BuildMaterializationSnapshot(new[] { source }, tags,
                new[] { new BuildContentRequirement("Title", BuildContentCardinality.ExactlyOne) },
                new[] { Dependency(source.PhysicalKey, "Assets/Title.unity") });

            var result = Bs4FixtureComposer.Compose(plan, snapshot, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "Assets/Probe.prefab", "Full", "token");

            Assert.That(result.Plan.SelectedContent.Select(x => x.LogicalKey), Is.EquivalentTo(new[] { "Title", Bs4FixtureComposer.LogicalKey }));
            Assert.That(result.Snapshot.Candidates.Count, Is.EqualTo(2));
            Assert.That(result.Snapshot.Requirements.Single(x => x.LogicalKey == Bs4FixtureComposer.LogicalKey).Cardinality, Is.EqualTo(BuildContentCardinality.ExactlyOne));
            Assert.That(result.Snapshot.FindDependency("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"), Is.Not.Null);
        }

        [Test]
        public void Compose_PhysicalCollision_IsRejected()
        {
            var source = Candidate("scene/full", "Title", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { source }, Array.Empty<BuildContentExclusion>());
            var snapshot = new BuildMaterializationSnapshot(new[] { source }, new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(), new[] { Dependency(source.PhysicalKey, "Assets/Title.unity") });
            Assert.Throws<InvalidOperationException>(() => Bs4FixtureComposer.Compose(plan, snapshot, source.PhysicalKey, "Assets/Probe.prefab", "Full", "token"));
        }

        private static BuildContentCandidate Candidate(string stable, string logical, string physical) =>
            new(stable, logical, physical, new BuildProvenance("test", logical));
        private static BuildDependencySnapshot Dependency(string guid, string path) =>
            new(guid, path, new[] { new BuildDependencyEntry(guid, path) });
    }
}
