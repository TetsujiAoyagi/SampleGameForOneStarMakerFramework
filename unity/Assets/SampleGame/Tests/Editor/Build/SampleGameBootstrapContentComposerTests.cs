#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class SampleGameBootstrapContentComposerTests
    {
        [Test]
        public void Compose_AddsUiCommonAndMapAsExactlyOneFull()
        {
            var source = Candidate("scene/full", "Title", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { source }, Array.Empty<BuildContentExclusion>());
            var tags = new Dictionary<string, IReadOnlyList<BuildTag>> { [source.StableKey] = new[] { new BuildTag("Representation", "Full") } };
            var snapshot = new BuildMaterializationSnapshot(new[] { source }, tags,
                new[] { new BuildContentRequirement("Title", BuildContentCardinality.ExactlyOne) },
                new[] { Dependency(source.PhysicalKey, "Assets/Title.unity") });

            var result = SampleGameBootstrapContentComposer.Compose(
                plan, snapshot,
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                Dependency("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", SampleGameBootstrapContentComposer.UiCommonPath),
                "cccccccccccccccccccccccccccccccc",
                Dependency("cccccccccccccccccccccccccccccccc", SampleGameBootstrapContentComposer.SceneResourceMapPath));

            Assert.That(result.Plan.SelectedContent.Select(x => x.LogicalKey), Is.EquivalentTo(new[]
            {
                "Title",
                SampleGameBootstrapContentComposer.UiCommonLogicalKey,
                SampleGameBootstrapContentComposer.SceneResourceMapLogicalKey,
            }));
            Assert.That(result.Snapshot.Requirements.Single(x => x.LogicalKey == SampleGameBootstrapContentComposer.UiCommonLogicalKey).Cardinality,
                Is.EqualTo(BuildContentCardinality.ExactlyOne));
            Assert.That(result.Snapshot.Requirements.Single(x => x.LogicalKey == SampleGameBootstrapContentComposer.SceneResourceMapLogicalKey).Cardinality,
                Is.EqualTo(BuildContentCardinality.ExactlyOne));
            Assert.That(result.Snapshot.FindDependency("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")!.RootPath,
                Is.EqualTo(SampleGameBootstrapContentComposer.UiCommonPath));
        }

        [Test]
        public void Compose_KeepsTheAbsenceRecords()
        {
            var present = Candidate("scene/full", "Title", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            var absent = Candidate("other/full", "Other", "dddddddddddddddddddddddddddddddd");
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { present, absent }, Array.Empty<BuildContentExclusion>());
            var absence = new BuildMaterializationAbsence(absent.StableKey, absent.LogicalKey, absent.PhysicalKey, "Assets/Other.unity",
                BuildMaterializationAbsenceReason.RootFileMissing, new[] { "Assets/Other.unity" });
            var snapshot = new BuildMaterializationSnapshot(new[] { present, absent }, new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(), new[] { Dependency(present.PhysicalKey, "Assets/Title.unity") },
                Array.Empty<BuildGraphNode>(), new[] { absence });

            var result = SampleGameBootstrapContentComposer.Compose(
                plan, snapshot,
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                Dependency("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", SampleGameBootstrapContentComposer.UiCommonPath),
                "cccccccccccccccccccccccccccccccc",
                Dependency("cccccccccccccccccccccccccccccccc", SampleGameBootstrapContentComposer.SceneResourceMapPath));

            Assert.That(result.Snapshot.Absences.Select(x => x.StableKey + "|" + x.Reason + "|" + string.Join(",", x.MissingPaths)),
                Is.EqualTo(new[] { "other/full|RootFileMissing|Assets/Other.unity" }));
        }

        [Test]
        public void Compose_WithoutAbsences_HasNoAbsenceRecords()
        {
            var source = Candidate("scene/full", "Title", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { source }, Array.Empty<BuildContentExclusion>());
            var snapshot = new BuildMaterializationSnapshot(new[] { source }, new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(), new[] { Dependency(source.PhysicalKey, "Assets/Title.unity") });
            var result = SampleGameBootstrapContentComposer.Compose(
                plan, snapshot,
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                Dependency("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", SampleGameBootstrapContentComposer.UiCommonPath),
                "cccccccccccccccccccccccccccccccc",
                Dependency("cccccccccccccccccccccccccccccccc", SampleGameBootstrapContentComposer.SceneResourceMapPath));
            Assert.That(result.Snapshot.Absences, Is.Empty);
        }

        [Test]
        public void Compose_PhysicalCollision_IsRejected()
        {
            var source = Candidate("scene/full", "Title", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { source }, Array.Empty<BuildContentExclusion>());
            var snapshot = new BuildMaterializationSnapshot(new[] { source }, new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(), new[] { Dependency(source.PhysicalKey, "Assets/Title.unity") });
            Assert.Throws<InvalidOperationException>(() => SampleGameBootstrapContentComposer.Compose(
                plan, snapshot,
                source.PhysicalKey,
                Dependency(source.PhysicalKey, SampleGameBootstrapContentComposer.UiCommonPath),
                "cccccccccccccccccccccccccccccccc",
                Dependency("cccccccccccccccccccccccccccccccc", SampleGameBootstrapContentComposer.SceneResourceMapPath)));
        }

        [Test]
        public void BootstrapAssets_AreRealProjectAssets()
        {
            Assert.That(AssetDatabase.AssetPathToGUID(SampleGameBootstrapContentComposer.UiCommonPath), Is.Not.Empty);
            Assert.That(AssetDatabase.AssetPathToGUID(SampleGameBootstrapContentComposer.SceneResourceMapPath), Is.Not.Empty);
        }

        private static BuildContentCandidate Candidate(string stable, string logical, string physical) =>
            new(stable, logical, physical, new BuildProvenance("test", logical));
        private static BuildDependencySnapshot Dependency(string guid, string path) =>
            new(guid, path, new[] { new BuildDependencyEntry(guid, path) });
    }
}
