#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SampleGame.DependOnAll.Editor.Streaming.Cells.Generation;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.DependOnAll.Editor.Streaming.Cells.Planning;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor
{
    /// <summary>任意 identity を使う WorldCellGenerator の受入テスト。</summary>
    [TestFixture]
    public sealed class WorldCellGenerationIdentityTests
    {
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            // 永続化したテスト asset は先に削除し、残った一時 SO だけを破棄する。
            if (AssetDatabase.IsValidFolder("Assets/Test")) AssetDatabase.DeleteAsset("Assets/Test");
            foreach (var obj in _createdObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void ComputePlan_UsesArbitraryIdentityForPathsAndSkip()
        {
            var definition = CreateGridDefinition(1, 1);
            var targets = new[] { new WorldCellGenerationTarget("Arbitrary_Cell", Vector2Int.zero) };
            var first = WorldCellGenerator.ComputePlan(definition, targets, WorldCellExistingState.Empty);
            var entry = first.Entries.Single();
            Assert.That(entry.Identity, Is.EqualTo("Arbitrary_Cell"));
            Assert.That(entry.SceneAssetPath, Is.EqualTo("Assets/Test/World/Cells/Arbitrary_Cell/Arbitrary_Cell.unity"));
            Assert.That(entry.SceneResourceAssetPath, Is.EqualTo("Assets/Test/SceneMap/Cells/Arbitrary_Cell/Arbitrary_Cell.asset"));
            var second = WorldCellGenerator.ComputePlan(
                definition, targets, new WorldCellExistingState(new[] { "Arbitrary_Cell" }, null));
            Assert.That(second.SkipCount, Is.EqualTo(1));
        }

        [Test]
        public void ComputePlan_DuplicateTargetFailsBeforePlanning()
        {
            var definition = CreateGridDefinition(1, 1);
            var target = new WorldCellGenerationTarget("Arbitrary_Cell", Vector2Int.zero);
            Assert.Throws<System.ArgumentException>(() => WorldCellGenerator.ComputePlan(
                definition, new[] { target, target }, WorldCellExistingState.Empty));
        }

        [Test]
        public void Generate_DuplicateTargetFailsBeforeSideEffects()
        {
            var definition = CreateGridDefinition(1, 1);
            var map = CreateEmptyMap();
            var parent = CreateSceneResource("World");
            var target = new WorldCellGenerationTarget("Arbitrary_Cell", Vector2Int.zero);

            Assert.Throws<System.ArgumentException>(() => WorldCellGenerator.Generate(
                definition, new[] { target, target }, map, parent));
            Assert.That(map.SceneResources, Is.Empty);
            Assert.That(parent.Children, Is.Empty);
            Assert.That(AssetDatabase.IsValidFolder("Assets/Test"), Is.False);

            Assert.Throws<System.ArgumentException>(() => WorldCellGenerator.Generate(
                definition, new[] { default(WorldCellGenerationTarget) }, map, parent));
            Assert.That(map.SceneResources, Is.Empty);
            Assert.That(parent.Children, Is.Empty);
            Assert.That(AssetDatabase.IsValidFolder("Assets/Test"), Is.False);
        }

        [Test]
        public void FromMap_UsesTargetMembershipForArbitraryIdentity()
        {
            var map = CreateEmptyMap();
            var targetResource = CreateSceneResource("Arbitrary_Cell");
            var parserNamedResource = CreateSceneResource("Cell_8_8");
            AddToMap(map, targetResource);
            AddToMap(map, parserNamedResource);
            var targets = new[] { new WorldCellGenerationTarget("Arbitrary_Cell", Vector2Int.zero) };
            var state = WorldCellExistingState.FromMap(map, targets);
            Assert.That(state.ExistingCellIdentities, Is.EquivalentTo(new[] { "Arbitrary_Cell" }));
        }

        [Test]
        public void FromMap_DefaultTargetFailsBeforeReadingMap()
        {
            var map = CreateEmptyMap();
            Assert.Throws<System.ArgumentException>(() => WorldCellExistingState.FromMap(
                map, new[] { default(WorldCellGenerationTarget) }));
        }

        [Test]
        public void CellPopulationPlan_DefaultTargetFailsBeforePlanning()
        {
            Assert.Throws<System.ArgumentException>(() => CellPopulationPlan.Compute(
                new[] { default(WorldCellGenerationTarget) },
                System.Array.Empty<CellExistingState>()));
        }

        [Test]
        public void Generate_AdoptsArbitraryIdentityAsset_AndThenSkipsIt()
        {
            var definition = CreateGridDefinition(1, 1);
            var map = CreateEmptyMap();
            var parent = CreateSceneResource("World");
            var targets = new[] { new WorldCellGenerationTarget("Arbitrary_Cell", Vector2Int.zero) };
            var parentPath = "Assets/Test/SceneMap/World.asset";
            var resourcePath = "Assets/Test/SceneMap/Cells/Arbitrary_Cell/Arbitrary_Cell.asset";
            EnsureAssetFolder("Assets/Test/SceneMap/Cells/Arbitrary_Cell");
            AssetDatabase.CreateAsset(parent, parentPath);
            _createdObjects.Remove(parent);
            var existing = CreateSceneResource("Arbitrary_Cell");
            AssetDatabase.CreateAsset(existing, resourcePath);
            // Persisted Unity assets must be removed through AssetDatabase, not DestroyImmediate.
            _createdObjects.Remove(existing);
            AssetDatabase.SaveAssets();
            try
            {
                Assert.That(WorldCellGenerator.Generate(definition, targets, map, parent), Is.True);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                var state = WorldCellExistingState.FromMap(map, targets);
                var plan = WorldCellGenerator.ComputePlan(definition, targets, state);
                Assert.That(plan.CreateCount, Is.EqualTo(0));
                Assert.That(plan.SkipCount, Is.EqualTo(1));

                var adopted = AssetDatabase.LoadAssetAtPath<SceneResource>(resourcePath);
                Assert.That(adopted, Is.Not.Null);
                Assert.That(adopted!.Identity, Is.EqualTo("Arbitrary_Cell"));
                var mapResource = map.GetSceneResource("Arbitrary_Cell");
                Assert.That(mapResource, Is.Not.Null);
                Assert.That(mapResource!.Identity, Is.EqualTo("Arbitrary_Cell"));
                Assert.That(adopted.Parent, Is.Not.Null);
                Assert.That(adopted.Parent!.Identity, Is.EqualTo("World"));
                var persistedParent = AssetDatabase.LoadAssetAtPath<SceneResource>(parentPath);
                Assert.That(persistedParent, Is.Not.Null);
                Assert.That(persistedParent!.Children.Any(child =>
                    child != null && child.Identity == "Arbitrary_Cell"), Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset("Assets/Test");
            }
        }

        [Test]
        public void FromMap_DuplicateTargetEntryThrows()
        {
            var map = CreateEmptyMap();
            AddToMap(map, CreateSceneResource("Arbitrary_Cell"));
            AddToMap(map, CreateSceneResource("Arbitrary_Cell"));
            var targets = new[] { new WorldCellGenerationTarget("Arbitrary_Cell", Vector2Int.zero) };
            Assert.Throws<System.InvalidOperationException>(() => WorldCellExistingState.FromMap(map, targets));
        }

        [Test]
        public void GenerationTarget_RejectsInvalidIdentityPathCharacters()
        {
            Assert.Throws<System.ArgumentException>(() => new WorldCellGenerationTarget("", Vector2Int.zero));
            Assert.Throws<System.ArgumentException>(() => new WorldCellGenerationTarget("   ", Vector2Int.zero));
            Assert.Throws<System.ArgumentException>(() => new WorldCellGenerationTarget("A/B", Vector2Int.zero));
            Assert.Throws<System.ArgumentException>(() => new WorldCellGenerationTarget("A\\B", Vector2Int.zero));
        }

        private WorldGridDefinition CreateGridDefinition(int width, int height)
        {
            var definition = ScriptableObject.CreateInstance<WorldGridDefinition>();
            _createdObjects.Add(definition);
            var so = new SerializedObject(definition);
            so.FindProperty("_origin").vector3Value = Vector3.zero;
            so.FindProperty("_cellSize").floatValue = 100f;
            var rects = so.FindProperty("_rectangles");
            rects.ClearArray();
            rects.InsertArrayElementAtIndex(0);
            var elem = rects.GetArrayElementAtIndex(0);
            elem.FindPropertyRelative("origin").vector2IntValue = Vector2Int.zero;
            elem.FindPropertyRelative("size").vector2IntValue = new Vector2Int(width, height);
            so.FindProperty("_parentSceneIdentity").stringValue = "World";
            so.FindProperty("_sceneOutputFolder").stringValue = "Assets/Test/World/Cells";
            so.FindProperty("_sceneResourceOutputFolder").stringValue = "Assets/Test/SceneMap/Cells";
            so.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private SceneResourceMap CreateEmptyMap()
        {
            var map = ScriptableObject.CreateInstance<SceneResourceMap>();
            _createdObjects.Add(map);
            return map;
        }

        private SceneResource CreateSceneResource(string identity)
        {
            var resource = ScriptableObject.CreateInstance<SceneResource>();
            resource.Identity = identity;
            _createdObjects.Add(resource);
            return resource;
        }

        private static void AddToMap(SceneResourceMap map, SceneResource resource)
        {
            var so = new SerializedObject(map);
            var list = so.FindProperty("_sceneResources");
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = resource;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureAssetFolder(string path)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized)) return;
            var parts = normalized.Split('/');
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
