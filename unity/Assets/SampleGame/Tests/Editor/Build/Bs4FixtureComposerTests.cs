#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Foundation.Config;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;

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

        [Test]
        public void ParseSelectedPhysicalGuid_StableKeyContainsSeparators_ReturnsTrailingGuid()
        {
            const string guid = "f1e2d3c4b5a697887766554433221100";
            var selected = "osm-build-candidate-v1|11:PlayerScene4:Full32:" + guid + "|PlayerScene|" + guid;

            Assert.That(SampleGamePlayerBuildCoordinator.ParseSelectedPhysicalGuid(selected), Is.EqualTo(guid));
        }

        [TestCase("missing-separator")]
        [TestCase("stable|logical|not-a-guid")]
        public void ParseSelectedPhysicalGuid_NonCanonicalValue_IsRejected(string selected)
        {
            Assert.Throws<InvalidOperationException>(() => SampleGamePlayerBuildCoordinator.ParseSelectedPhysicalGuid(selected));
        }

        [Test]
        public void BootstrapScene_IsARealProjectAsset()
        {
            Assert.That(AssetDatabase.AssetPathToGUID(SampleGamePlayerBuildCoordinator.BootstrapScene), Is.Not.Empty);
        }

        [Test]
        public void RuntimeJson_CopiesContentSetRepresentationAndCompanionSetFromDeclaration()
        {
            // baked config は宣言から写す（AC14 の期待値 `cellCompanionSet` と `representation` の出どころ）。
            var json = SampleGamePlayerBuildCoordinator.RuntimeJson("identity", SampleGameBuildPurposes.Bs4SpringFull, "token", "smoke");
            Assert.That(json, Does.Contain("\"contentSet\": \"bs4-spring-full\""));
            Assert.That(json, Does.Contain("\"representation\": \"Full\""));
            Assert.That(json, Does.Contain("\"cellCompanionSet\": \"Full\""));
            Assert.That(json, Does.Contain("\"runtimeMode\": \"directory\""));

            var planner = SampleGamePlayerBuildCoordinator.RuntimeJson("identity", SampleGameBuildPurposes.SpringPlannerWhitebox, "token", "smoke");
            Assert.That(planner, Does.Contain("\"cellCompanionSet\": \"Planner\""));
            Assert.That(planner, Does.Contain("\"representation\": \"Whitebox\""));
        }

        // 動作確認の Player の baked config は、今の 12 の値に content:playerKind = smoke を足した 13 で、他は変えない。
        [Test]
        public void RuntimeJson_SmokeKind_IsTheExistingTwelveValuesPlusPlayerKind()
        {
            var settings = SampleGamePlayerBuildCoordinator.SettingsOf(PlayerBuildKind.Smoke);
            var values = Flatten(SampleGamePlayerBuildCoordinator.RuntimeJson("identity", settings.Declaration, "token", settings.PlayerKindValue));
            Assert.That(values, Is.EquivalentTo(new Dictionary<string, string>
            {
                ["content:schemaVersion"] = "1",
                ["content:runtimeMode"] = "directory",
                ["content:buildIdentity"] = "identity",
                ["content:contentSet"] = "bs4-spring-full",
                ["content:target"] = "StandaloneWindows64-Player",
                ["content:relativeDirectory"] = "content",
                ["content:firstScene"] = "Title",
                ["content:representation"] = "Full",
                ["content:probeToken"] = "token",
                ["content:playerKind"] = "smoke",
                ["debugSocket:enabled"] = "false",
                ["telemetry:profiler:enabled"] = "true",
                ["world:cellCompanionSet"] = SampleGameBuildPurposes.CellCompanionSetOf(SampleGameBuildPurposes.Bs4SpringFull),
            }));
        }

        // 遊べる Player は同じ 13 の key で、probe なしの spring-full・probeToken 空・playerKind = playable。
        [Test]
        public void RuntimeJson_PlayableKind_HasTheSameKeysWithPlayableValues()
        {
            var settings = SampleGamePlayerBuildCoordinator.SettingsOf(PlayerBuildKind.Playable);
            Assert.That(settings.UsesProbe, Is.False);
            var values = Flatten(SampleGamePlayerBuildCoordinator.RuntimeJson("identity", settings.Declaration, string.Empty, settings.PlayerKindValue));
            Assert.That(values, Is.EquivalentTo(new Dictionary<string, string>
            {
                ["content:schemaVersion"] = "1",
                ["content:runtimeMode"] = "directory",
                ["content:buildIdentity"] = "identity",
                ["content:contentSet"] = "spring-full",
                ["content:target"] = "StandaloneWindows64-Player",
                ["content:relativeDirectory"] = "content",
                ["content:firstScene"] = "Title",
                ["content:representation"] = SampleGameBuildPurposes.SpringFull.Representation.Primary,
                ["content:probeToken"] = "",
                ["content:playerKind"] = "playable",
                ["debugSocket:enabled"] = "false",
                ["telemetry:profiler:enabled"] = "true",
                ["world:cellCompanionSet"] = SampleGameBuildPurposes.CellCompanionSetOf(SampleGameBuildPurposes.SpringFull),
            }));
        }

        [Test]
        public void SettingsOf_SelectsDeclarationProbeAndArtifactsRootPerKind()
        {
            var smoke = SampleGamePlayerBuildCoordinator.SettingsOf(PlayerBuildKind.Smoke);
            Assert.That(smoke.Declaration.Name, Is.EqualTo("bs4-spring-full"));
            Assert.That(smoke.UsesProbe, Is.True);
            Assert.That(smoke.PlayerKindValue, Is.EqualTo("smoke"));

            var projectRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "osm-s41-root", "unity");
            var artifacts = System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, "..", "artifacts"));
            Assert.That(SampleGamePlayerBuildCoordinator.ArtifactsRootOf(projectRoot, PlayerBuildKind.Smoke),
                Is.EqualTo(System.IO.Path.Combine(artifacts, "bs4")));
            Assert.That(SampleGamePlayerBuildCoordinator.ArtifactsRootOf(projectRoot, PlayerBuildKind.Playable),
                Is.EqualTo(System.IO.Path.Combine(artifacts, "playable")));
        }

        [Test]
        public void SettingsOf_CopiesBootstrapOnlyForTheSmokePlayer()
        {
            // 動作確認の Player は UICommon と map を Player に写す今の build、遊べる Player は写さずに空の開始 Scene から始める。
            Assert.That(SampleGamePlayerBuildCoordinator.SettingsOf(PlayerBuildKind.Smoke).CopiesBootstrap, Is.True);
            var playable = SampleGamePlayerBuildCoordinator.SettingsOf(PlayerBuildKind.Playable);
            Assert.That(playable.CopiesBootstrap, Is.False);
            Assert.That(playable.Declaration.Name, Is.EqualTo("spring-full"));
            Assert.That(playable.UsesProbe, Is.False);
            Assert.That(playable.PlayerKindValue, Is.EqualTo("playable"));
            Assert.That(SampleGamePlayerBuildCoordinator.PlayableStartScene,
                Is.EqualTo("Assets/SampleGame/DependOnAll/PlayerStart/PlayablePlayerStart.unity"));
        }

        private static Dictionary<string, string> Flatten(string json)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            JsonConfigFlattener.Flatten(json, values);
            return values;
        }

        private static BuildContentCandidate Candidate(string stable, string logical, string physical) =>
            new(stable, logical, physical, new BuildProvenance("test", logical));
        private static BuildDependencySnapshot Dependency(string guid, string path) =>
            new(guid, path, new[] { new BuildDependencyEntry(guid, path) });
    }
}
