#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.DependOnAll.Editor.WorldAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.WorldAuthoring
{
    [TestFixture]
    public sealed class WorldWorkspaceSelectionTests
    {
        private readonly List<ScriptableObject> _created = new();
        private SceneSetup[] _originalSetup = Array.Empty<SceneSetup>();
        private string _tempRoot = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _originalSetup = EditorSceneManager.GetSceneManagerSetup();
            _tempRoot = string.Empty;
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_tempRoot))
            {
                RestoreOriginalSetup();
                if (AssetDatabase.IsValidFolder(_tempRoot)) AssetDatabase.DeleteAsset(_tempRoot);
            }
            foreach (var item in _created) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            _created.Clear();
            AssetDatabase.Refresh();
        }

        [TestCase("Level", "Full", "Spring_Cell_0_0:")]
        [TestCase("Level", "Whitebox", "Spring_Cell_0_0:Whitebox")]
        [TestCase("Environment", "Full", "Spring_Cell_0_0:|Spring_Environment_0_0:")]
        [TestCase("Lighting", "Full", "Spring_Cell_0_0:|Spring_Environment_0_0:|Spring_Lighting:|Spring_Lighting_0_0:")]
        [TestCase("Vfx", "Full", "Spring_Cell_0_0:|Spring_Environment_0_0:|Spring_Lighting:|Spring_VFX_0_0:")]
        [TestCase("Planner", "Full", "Spring_Cell_0_0:Whitebox|Spring_Events_0_0:")]
        public void BuildOpenPlan_UsesFrozenOrderAndExactVariant(string roleName, string payloadName, string expected)
        {
            var role = (WorldWorkspaceRole)Enum.Parse(typeof(WorldWorkspaceRole), roleName);
            var payload = (WorldLevelPayload)Enum.Parse(typeof(WorldLevelPayload), payloadName);
            var selection = new WorldWorkspaceSelection(WorldSeason.Spring, 0, 0, role, payload);
            var actual = string.Join("|", Convert(selection.BuildOpenPlan()));
            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase("Spring", 0, 0)]
        [TestCase("Winter", 8, 5)]
        public void Constructor_AcceptsAllCoordinateBoundaries(string seasonName, int x, int y)
        {
            var season = (WorldSeason)Enum.Parse(typeof(WorldSeason), seasonName);
            Assert.DoesNotThrow(() => new WorldWorkspaceSelection(season, x, y, WorldWorkspaceRole.Level, WorldLevelPayload.Full));
        }

        [Test]
        public void Selection_AllSeasonsBoundariesRolesAndPayloads_ProducePlans()
        {
            var coordinates = new[] { (X: 0, Y: 0), (X: 8, Y: 5) };
            foreach (WorldSeason season in Enum.GetValues(typeof(WorldSeason)))
            foreach (var coordinate in coordinates)
            foreach (WorldWorkspaceRole role in Enum.GetValues(typeof(WorldWorkspaceRole)))
            foreach (WorldLevelPayload payload in Enum.GetValues(typeof(WorldLevelPayload)))
            {
                var selection = new WorldWorkspaceSelection(
                    season, coordinate.X, coordinate.Y, role, payload);
                var plan = selection.BuildOpenPlan();
                Assert.That(plan, Is.Not.Empty, $"{season}/{coordinate}/{role}/{payload}");
                Assert.That(plan[0].Identity,
                    Is.EqualTo($"{season}_Cell_{coordinate.X}_{coordinate.Y}"));
            }
        }

        [TestCase(-1, 0)]
        [TestCase(9, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 6)]
        public void Constructor_RejectsOutOfRangeCoordinates(int x, int y)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldWorkspaceSelection(WorldSeason.Spring, x, y, WorldWorkspaceRole.Level, WorldLevelPayload.Full));
        }

        [TestCase("Lighting", "Spring_Lighting_8_5")]
        [TestCase("Vfx", "Spring_VFX_8_5")]
        [TestCase("Planner", "Spring_Events_8_5")]
        public void CreatePlan_CalculatesQualifiedPaths(string roleName, string identity)
        {
            var role = (WorldWorkspaceRole)Enum.Parse(typeof(WorldWorkspaceRole), roleName);
            var selection = new WorldWorkspaceSelection(WorldSeason.Spring, 8, 5, role, WorldLevelPayload.Full);
            var plan = WorldCompanionCreationPlan.Create(selection);
            Assert.That(plan.Identity, Is.EqualTo(identity));
            Assert.That(plan.ParentIdentity, Is.EqualTo("Spring_Cell_8_5"));
            Assert.That(plan.ScenePath, Is.EqualTo($"Assets/SampleGame/InGame/InGameSession/World/Seasons/Spring/Cells/Spring_Cell_8_5/{identity}/{identity}.unity"));
            Assert.That(plan.ResourcePath, Is.EqualTo($"Assets/SampleGame/InGame/InGameSession/World/Seasons/Spring/Cells/Spring_Cell_8_5/{identity}/{identity}.asset"));
            Assert.That(plan.NodePath, Is.EqualTo($"Assets/SceneGraphData/Nodes/Cells/{identity}.asset"));
        }

        [TestCase((int)WorldWorkspaceRole.Level)]
        [TestCase((int)WorldWorkspaceRole.Environment)]
        public void CreatePlan_RejectsRequiredNonCompanionRoles(int roleValue)
        {
            var selection = new WorldWorkspaceSelection(
                WorldSeason.Spring, 0, 0, (WorldWorkspaceRole)roleValue, WorldLevelPayload.Full);
            Assert.Throws<InvalidOperationException>(() => WorldCompanionCreationPlan.Create(selection));
        }

        [Test]
        public void Opener_WhiteboxMissing_DoesNotFallbackToDefaultPayload()
        {
            var sampleSceneGuid = AssetDatabase.AssetPathToGUID("Assets/Scenes/SampleScene.unity");
            var resource = CreateResource("Spring_Cell_0_0", new AssetPayload(string.Empty, new AssetReference(sampleSceneGuid)));
            var map = CreateMap(resource);
            var plan = new WorldWorkspaceSelection(WorldSeason.Spring, 0, 0, WorldWorkspaceRole.Level, WorldLevelPayload.Whitebox).BuildOpenPlan();

            var result = WorldWorkspaceSceneOpener.Resolve(map, plan);

            Assert.That(result.CanOpen, Is.False);
            Assert.That(result.ScenePaths, Is.Empty);
            Assert.That(result.MissingRequired, Has.Count.EqualTo(1));
        }

        [Test]
        public void Opener_ResolvedLightingPlan_OpensSingleThenAdditiveInFrozenOrder()
        {
            Assert.That(WorldCompanionRecoveryJournal.Exists, Is.False);
            _tempRoot = $"Assets/__WorldWorkspaceOpenTests_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _tempRoot.Substring("Assets/".Length));
            var identities = new[]
            {
                "Spring_Cell_0_0",
                "Spring_Environment_0_0",
                "Spring_Lighting",
                "Spring_Lighting_0_0",
            };
            var paths = new string[identities.Length];
            var resources = new SceneResource[identities.Length];
            for (var i = 0; i < identities.Length; i++)
            {
                paths[i] = $"{_tempRoot}/{identities[i]}.unity";
                var mode = i == 0 ? NewSceneMode.Single : NewSceneMode.Additive;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
                Assert.That(EditorSceneManager.SaveScene(scene, paths[i]), Is.True);
                resources[i] = CreateResource(
                    identities[i],
                    new AssetPayload(string.Empty, new AssetReference(AssetDatabase.AssetPathToGUID(paths[i]))));
            }
            var map = CreateMap(resources);
            var selection = new WorldWorkspaceSelection(
                WorldSeason.Spring, 0, 0, WorldWorkspaceRole.Lighting, WorldLevelPayload.Full);

            Assert.That(WorldWorkspaceSceneOpener.Open(map, selection, out var message), Is.True, message);

            Assert.That(SceneManager.sceneCount, Is.EqualTo(paths.Length));
            for (var i = 0; i < paths.Length; i++)
                Assert.That(SceneManager.GetSceneAt(i).path, Is.EqualTo(paths[i]));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(paths[0]));
        }

        private SceneResource CreateResource(string identity, params AssetPayload[] payloads)
        {
            var resource = ScriptableObject.CreateInstance<SceneResource>();
            Set(resource, "_identity", identity);
            Set(resource, "_sceneAssetDescription", new SceneAssetDescription(identity, LoadType.OnDemand, new List<AssetPayload>(payloads)));
            _created.Add(resource);
            return resource;
        }

        private SceneResourceMap CreateMap(params SceneResource[] resources)
        {
            var map = ScriptableObject.CreateInstance<SceneResourceMap>();
            Set(map, "_sceneResources", new List<SceneResource>(resources));
            SceneResourceGeneratorBridge.Rebuild(map);
            _created.Add(map);
            return map;
        }

        private static IEnumerable<string> Convert(IReadOnlyList<WorldWorkspaceOpenItem> plan)
        {
            for (var i = 0; i < plan.Count; i++) yield return plan[i].Identity + ":" + plan[i].Variant;
        }

        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

        private void RestoreOriginalSetup()
        {
            foreach (var item in _originalSetup)
            {
                if (!item.isLoaded || string.IsNullOrEmpty(item.path)) continue;
                EditorSceneManager.RestoreSceneManagerSetup(_originalSetup);
                return;
            }

            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        private static class SceneResourceGeneratorBridge
        {
            internal static void Rebuild(SceneResourceMap map)
                => OneStarMaker.Editor.SceneGraph.SceneResourceGenerator.RebuildMapLookup(map);
        }
    }
}
