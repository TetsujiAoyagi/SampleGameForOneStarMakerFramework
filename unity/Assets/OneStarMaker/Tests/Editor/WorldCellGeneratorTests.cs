#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.Streaming;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor
{
    /// <summary>
    /// T-05 World Cell Generator の受入テスト。
    /// .unity ファイル I/O は対象外。ComputePlan / ApplyPlan の純粋な計画・Map 登録を検証する。
    /// </summary>
    [TestFixture]
    public sealed class WorldCellGeneratorTests
    {
        private const int GridSize = 3;
        private const string ParentIdentity = "World";

        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Generate_GridDefinition_ProducesNxNResources()
        {
            var definition = CreateGridDefinition(GridSize, GridSize);
            var map = CreateEmptyMap();
            var parent = CreateSceneResource(ParentIdentity);

            var plan = WorldCellGenerator.ComputePlan(definition, WorldCellExistingState.Empty);
            var result = WorldCellGenerator.ApplyPlan(definition, plan, map, parent);
            RegisterCreatedResources(result);

            Assert.That(plan.Entries, Has.Count.EqualTo(GridSize * GridSize));
            Assert.That(plan.CreateCount, Is.EqualTo(GridSize * GridSize));
            Assert.That(result.AllCellResources, Has.Count.EqualTo(GridSize * GridSize));
        }

        [Test]
        public void Generate_AllCells_AreOnDemandChildrenOfWorld()
        {
            var definition = CreateGridDefinition(GridSize, GridSize);
            var map = CreateEmptyMap();
            var parent = CreateSceneResource(ParentIdentity);

            var plan = WorldCellGenerator.ComputePlan(definition, WorldCellExistingState.Empty);
            var result = WorldCellGenerator.ApplyPlan(definition, plan, map, parent);
            RegisterCreatedResources(result);

            Assert.That(parent.Children, Has.Count.EqualTo(GridSize * GridSize));

            foreach (var entry in plan.Entries)
            {
                var resource = map.GetSceneResource(entry.Identity);
                Assert.That(resource, Is.Not.Null, $"Map から {entry.Identity} が取得できること");
                Assert.That(resource!.Parent, Is.SameAs(parent));
                Assert.That(resource.Parent!.Identity, Is.EqualTo(ParentIdentity));
                Assert.That(resource.LoadType, Is.EqualTo(LoadType.OnDemand));
                Assert.That(resource.SceneAssetDescription, Is.Not.Null,
                    $"{entry.Identity} の SceneAssetDescription が生成されていること");
                Assert.That(resource.SceneAssetDescription!.SceneIdentity, Is.EqualTo(entry.Identity),
                    "SceneAssetDescription.SceneIdentity がセル identity と一致すること");
                Assert.That(parent.Children, Contains.Item(resource),
                    $"parent.Children に {entry.Identity} が同一参照で含まれること");
            }
        }

        [Test]
        public void Generate_Naming_FollowsCellXYConvention()
        {
            var definition = CreateGridDefinition(GridSize, GridSize);

            var plan = WorldCellGenerator.ComputePlan(definition, WorldCellExistingState.Empty);

            Assert.That(plan.Entries, Has.Count.EqualTo(GridSize * GridSize));

            var expectedCoordinates = new HashSet<Vector2Int>();
            for (var y = 0; y < GridSize; y++)
            {
                for (var x = 0; x < GridSize; x++)
                {
                    expectedCoordinates.Add(new Vector2Int(x, y));
                }
            }

            var actualCoordinates = new HashSet<Vector2Int>();

            foreach (var entry in plan.Entries)
            {
                Assert.That(CellIdentity.IsCellId(entry.Identity), Is.True,
                    $"'{entry.Identity}' は Cell_{{x}}_{{y}} 形式であること");
                Assert.That(CellIdentity.TryParse(entry.Identity, out var parsed), Is.True);
                Assert.That(parsed, Is.EqualTo(entry.Coordinate));
                Assert.That(entry.Identity, Is.EqualTo(CellIdentity.Format(entry.Coordinate.x, entry.Coordinate.y)));
                actualCoordinates.Add(entry.Coordinate);
            }

            Assert.That(actualCoordinates, Is.EquivalentTo(expectedCoordinates));
        }

        [Test]
        public void ComputePlan_OutputPaths_UsePerCellSubfolders()
        {
            // CCS-00: フォルダ = 実行環境境界。Cell の .unity / .asset は同名サブフォルダに同居する。
            var definition = CreateGridDefinition(GridSize, GridSize);
            var plan = WorldCellGenerator.ComputePlan(definition, WorldCellExistingState.Empty);

            foreach (var entry in plan.Entries)
            {
                var identity = entry.Identity;
                Assert.That(
                    entry.SceneAssetPath,
                    Is.EqualTo($"Assets/Test/World/Cells/{identity}/{identity}.unity"));
                Assert.That(
                    entry.SceneResourceAssetPath,
                    Is.EqualTo($"Assets/Test/SceneMap/Cells/{identity}/{identity}.asset"));
            }
        }

        [Test]
        public void Generate_RunTwice_IsIdempotent()
        {
            var definition = CreateGridDefinition(GridSize, GridSize);
            var map = CreateEmptyMap();
            var parent = CreateSceneResource(ParentIdentity);

            var planFirst = WorldCellGenerator.ComputePlan(definition, WorldCellExistingState.Empty);
            var resultFirst = WorldCellGenerator.ApplyPlan(definition, planFirst, map, parent);
            RegisterCreatedResources(resultFirst);

            Assert.That(planFirst.CreateCount, Is.EqualTo(GridSize * GridSize));
            Assert.That(planFirst.SkipCount, Is.EqualTo(0));

            var existingState = WorldCellExistingState.FromMap(map);
            var planSecond = WorldCellGenerator.ComputePlan(definition, existingState);
            var resultSecond = WorldCellGenerator.ApplyPlan(definition, planSecond, map, parent);
            RegisterCreatedResources(resultSecond);

            Assert.That(planSecond.CreateCount, Is.EqualTo(0),
                "2 回目の計画に新規 Create が含まれないこと");
            Assert.That(planSecond.SkipCount, Is.EqualTo(GridSize * GridSize),
                "2 回目の計画は全件 Skip であること");
            Assert.That(resultSecond.CreatedOrUpdatedResources, Is.Empty,
                "2 回目の Apply で新規リソースが作られないこと");

            var registeredCellIds = CollectCellIdentitiesFromMap(map);
            Assert.That(registeredCellIds, Has.Count.EqualTo(GridSize * GridSize));
            Assert.That(registeredCellIds, Is.EquivalentTo(
                planFirst.Entries.Select(e => e.Identity).ToArray()),
                "2 回実行後も Map 上のセル集合に差分がないこと");

            Assert.That(resultFirst.AllCellResources.Select(r => r.Identity),
                Is.EquivalentTo(resultSecond.AllCellResources.Select(r => r.Identity)),
                "AllCellResources の identity 集合が 1 回目と 2 回目で一致すること");

            var cellResourcesInRawList = map.SceneResources
                .Where(r => r != null && CellIdentity.IsCellId(r.Identity))
                .ToList();
            Assert.That(cellResourcesInRawList, Has.Count.EqualTo(GridSize * GridSize),
                "SceneResources 生リスト上のセル件数が重複なく GridSize^2 であること");
            Assert.That(
                cellResourcesInRawList.GroupBy(r => r!.Identity).Count(g => g.Count() > 1),
                Is.EqualTo(0),
                "SceneResources 生リスト上に identity 重複がないこと");

            Assert.That(parent.Children, Has.Count.EqualTo(GridSize * GridSize),
                "2 回実行後も parent.Children が増えていないこと");

            foreach (var first in resultFirst.AllCellResources)
            {
                var second = resultSecond.AllCellResources.Single(r => r.Identity == first.Identity);
                Assert.That(second, Is.SameAs(first),
                    $"2 回目の {first.Identity} は 1 回目と同一インスタンスであること（新規作成されない）");
            }
        }

        [Test]
        public void ComputePlan_InvalidDefinition_Throws()
        {
            var zeroGrid = CreateGridDefinition(0, GridSize);
            Assert.Throws<System.ArgumentException>(
                () => WorldCellGenerator.ComputePlan(zeroGrid, WorldCellExistingState.Empty),
                "ゼロ以下のグリッドサイズは明示的な例外になること");

            var negativeGrid = CreateGridDefinition(GridSize, -1);
            Assert.Throws<System.ArgumentException>(
                () => WorldCellGenerator.ComputePlan(negativeGrid, WorldCellExistingState.Empty),
                "負のグリッドサイズは明示的な例外になること");
        }

        [Test]
        public void Generate_RegistersAllCells_ToSceneResourceMap()
        {
            var definition = CreateGridDefinition(GridSize, GridSize);
            var map = CreateEmptyMap();
            var parent = CreateSceneResource(ParentIdentity);

            var plan = WorldCellGenerator.ComputePlan(definition, WorldCellExistingState.Empty);
            var result = WorldCellGenerator.ApplyPlan(definition, plan, map, parent);
            RegisterCreatedResources(result);

            foreach (var entry in plan.Entries)
            {
                var resource = map.GetSceneResource(entry.Identity);
                Assert.That(resource, Is.Not.Null,
                    $"SceneResourceMap から {entry.Identity} が引けること");
                Assert.That(resource!.Identity, Is.EqualTo(entry.Identity));
                Assert.That(resource.SceneAssetDescription, Is.Not.Null,
                    $"{entry.Identity} の SceneAssetDescription が生成されていること");
            }

            var registeredCellIds = CollectCellIdentitiesFromMap(map);
            Assert.That(registeredCellIds, Has.Count.EqualTo(GridSize * GridSize));
        }

        private void RegisterCreatedResources(WorldCellGenerationResult result)
        {
            foreach (var resource in result.CreatedOrUpdatedResources)
            {
                if (!_createdObjects.Contains(resource))
                {
                    _createdObjects.Add(resource);
                }
            }
        }

        private WorldGridDefinition CreateGridDefinition(int width, int height)
        {
            var definition = ScriptableObject.CreateInstance<WorldGridDefinition>();
            _createdObjects.Add(definition);

            var so = new SerializedObject(definition);
            so.FindProperty("_origin").vector3Value = Vector3.zero;
            so.FindProperty("_cellSize").floatValue = 100f;
            so.FindProperty("_gridWidth").intValue = width;
            so.FindProperty("_gridHeight").intValue = height;
            so.FindProperty("_parentSceneIdentity").stringValue = ParentIdentity;
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

        private static HashSet<string> CollectCellIdentitiesFromMap(SceneResourceMap map)
        {
            var identities = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var resource in map.SceneResources)
            {
                if (resource != null && CellIdentity.IsCellId(resource.Identity))
                {
                    identities.Add(resource.Identity);
                }
            }

            return identities;
        }
    }
}
