#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SampleGame.DependOnAll.Editor.Streaming.Cells.Generation;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.DependOnAll.Editor.Streaming.Cells.Planning;
using SampleGame.DependOnAll.Editor.Streaming.Cells.State;
using SampleGame.InGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor
{
    [TestFixture]
    public sealed class WorldCellExistingStateCollectorTests
    {
        private const string Root = "Assets/TestCollector";
        private const string WorldScenePath = "Assets/SampleGame/InGame/InGameSession/World/World.unity";
        private readonly List<Object> _transientObjects = new();

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
            foreach (var obj in _transientObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _transientObjects.Clear();
        }

        [Test]
        public void Collect_UsesChildrenAndSiblingResources_NotUnrelatedScenesOrCellResource()
        {
            var ownOnly = CreateCellFolder("Arbitrary_Cell");
            CreatePersistedResource(ownOnly, "Arbitrary_Cell");
            CreateScene(ownOnly, "Arbitrary_Cell", DemoCellScene.AuthoredRootName);
            var unrelatedFolder = CreateCellFolder("Cell_0_0");
            CreatePersistedResource(unrelatedFolder, "Cell_0_0");
            CreateScene(unrelatedFolder, "Unrelated_0_0", string.Empty);

            var childFolder = CreateCellFolder("Cell_1_0");
            var childEnvironment = CreatePersistedResource(
                childFolder, "Environment_1_0");
            var childCell = CreatePersistedResource(childFolder, "Cell_1_0");
            AddChild(childCell, childEnvironment);
            CreateScene(childFolder, "Environment_1_0", EnvironmentScene.AuthoredRootName);

            var siblingFolder = CreateCellFolder("Cell_2_0");
            CreatePersistedResource(siblingFolder, "Cell_2_0");
            var siblingEnvironment = CreatePersistedResource(siblingFolder, "Environment_2_0");
            SetDanglingScenePayload(siblingEnvironment);
            CreateScene(siblingFolder, "Environment_2_0", EnvironmentScene.AuthoredRootName);

            var targets = new[]
            {
                new WorldCellGenerationTarget("Cell_0_0", Vector2Int.zero),
                new WorldCellGenerationTarget("Arbitrary_Cell", Vector2Int.one),
            };
            var states = WorldCellExistingStateCollector.Collect(targets, Root, WorldScenePath);

            var own = states.Single(s => s.Identity == "Arbitrary_Cell");
            var unrelated = states.Single(s => s.Identity == "Cell_0_0");
            var child = states.Single(s => s.Identity == "Cell_1_0");
            var sibling = states.Single(s => s.Identity == "Cell_2_0");
            Assert.That(own.HasCellAuthoredRoot, Is.True, "任意 identity の Cell AuthoredRoot を収集する");
            Assert.That(own.HasEnvironmentScene, Is.False, "Cell 自身の SceneResource は Environment に数えない");
            Assert.That(unrelated.HasEnvironmentScene, Is.False, "無関係な .unity だけでは Environment 候補にならない");
            Assert.That(child.HasEnvironmentScene, Is.True, "Cell.Children の Environment resource を収集する");
            Assert.That(child.HasEnvironmentAuthoredRoot, Is.True, "空 payload の Children resource は identity path を検査する");
            Assert.That(sibling.HasEnvironmentScene, Is.True, "同一フォルダの非 Cell resource を収集する");
            Assert.That(sibling.HasEnvironmentAuthoredRoot, Is.True, "dangling payload は identity path にフォールバックする");

            var plan = CellPopulationPlan.Compute(
                new[] { new WorldCellGenerationTarget("Cell_0_0", Vector2Int.zero) }, states);
            Assert.That(plan.ShouldPopulateEnvironment("Cell_0_0"), Is.True,
                "無関係な .unity だけでは HandAuthored Environment を Skip しない");
        }

        [Test]
        public void Collect_RejectsDuplicateTargetIdentities()
        {
            var target = new WorldCellGenerationTarget("Arbitrary_Cell", Vector2Int.zero);
            Assert.Throws<System.ArgumentException>(() =>
                WorldCellExistingStateCollector.Collect(
                    new[] { target, target }, Root, WorldScenePath));
            Assert.Throws<System.ArgumentException>(() =>
                WorldCellExistingStateCollector.Collect(
                    new[] { default(WorldCellGenerationTarget) }, Root, WorldScenePath));
        }

        private string CreateCellFolder(string identity)
        {
            var folder = $"{Root}/{identity}";
            EnsureFolder(folder);
            return folder;
        }

        private SceneResource CreatePersistedResource(string folder, string identity)
        {
            var resource = ScriptableObject.CreateInstance<SceneResource>();
            resource.Identity = identity;
            var path = $"{folder}/{identity}.asset";
            AssetDatabase.CreateAsset(resource, path);
            AssetDatabase.SaveAssets();
            return resource;
        }

        private static void AddChild(SceneResource parent, SceneResource child)
        {
            var so = new SerializedObject(parent);
            var children = so.FindProperty("_children");
            children.InsertArrayElementAtIndex(children.arraySize);
            children.GetArrayElementAtIndex(children.arraySize - 1).objectReferenceValue = child;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void CreateScene(string folder, string identity, string authoredRootName)
        {
            var path = $"{folder}/{identity}.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(authoredRootName))
            {
                var root = new GameObject(authoredRootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            EditorSceneManager.SaveScene(scene, path);
        }

        private static void SetDanglingScenePayload(SceneResource resource)
        {
            var description = new SceneAssetDescription();
            description.AddPayload(
                string.Empty,
                new AssetReference("00000000000000000000000000000000"));
            var field = typeof(SceneResource).GetField(
                "_sceneAssetDescription", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field!.SetValue(resource, description);
            EditorUtility.SetDirty(resource);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
