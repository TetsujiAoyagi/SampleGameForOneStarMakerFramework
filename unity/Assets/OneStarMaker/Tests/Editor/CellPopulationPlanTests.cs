#nullable enable

using System.Linq;
using NUnit.Framework;
using SampleGame.DependOnAll.Editor.Cells;
using UnityEngine;

namespace OneStarMaker.Tests.Editor
{
    /// <summary>
    /// CellPopulationPlan の純関数テスト（AssetDatabase 非依存）。
    /// </summary>
    [TestFixture]
    public sealed class CellPopulationPlanTests
    {
        private static CellGridSpec Grid4x4()
            => new(gridWidth: 4, gridHeight: 4, origin: Vector3.zero, cellSize: 250f);

        private static CellGridSpec Grid3x3()
            => new(gridWidth: 3, gridHeight: 3, origin: Vector3.zero, cellSize: 250f);

        private static CellExistingState State(
            int x,
            int y,
            bool hasCellAuthoredRoot,
            bool hasEnvironmentScene = false,
            bool hasEnvironmentAuthoredRoot = false)
        {
            var coordinate = new Vector2Int(x, y);
            return new CellExistingState(
                identity: $"Cell_{x}_{y}",
                coordinate: coordinate,
                hasCellAuthoredRoot: hasCellAuthoredRoot,
                hasEnvironmentScene: hasEnvironmentScene,
                hasEnvironmentAuthoredRoot: hasEnvironmentAuthoredRoot);
        }

        private static CellPopulationEntry RequirePopulationEntry(
            CellPopulationPlan plan,
            int x,
            int y)
        {
            var identity = $"Cell_{x}_{y}";
            var entry = plan.PopulationEntries.SingleOrDefault(e => e.Identity == identity);
            Assert.That(entry, Is.Not.Null, $"Populate 計画に {identity} が含まれること");
            return entry!;
        }

        [Test]
        public void T1_Generated_Populates_RegardlessOfAuthoredRoot()
        {
            // Generated 座標 (1,1) — 南辺 HandAuthored 以外
            var withoutRoot = State(1, 1, hasCellAuthoredRoot: false);
            var withRoot = State(1, 1, hasCellAuthoredRoot: true);
            var grid = Grid4x4();

            Assert.That(CellAuthoringPolicy.Resolve(1, 1), Is.EqualTo(CellAuthoringPolicyKind.Generated));

            var planWithout = CellPopulationPlan.Compute(grid, new[] { withoutRoot });
            var planWith = CellPopulationPlan.Compute(grid, new[] { withRoot });

            Assert.That(RequirePopulationEntry(planWithout, 1, 1).CellAction,
                Is.EqualTo(CellPopulationAction.Populate),
                "AuthoredRoot 無しでも Generated は Populate");
            Assert.That(RequirePopulationEntry(planWith, 1, 1).CellAction,
                Is.EqualTo(CellPopulationAction.Populate),
                "AuthoredRoot 有りでも Generated は Populate");
        }

        [Test]
        public void T2_HandAuthored_WithCellAuthoredRoot_SkipsCell()
        {
            var existing = State(0, 0, hasCellAuthoredRoot: true);
            Assert.That(CellAuthoringPolicy.Resolve(0, 0), Is.EqualTo(CellAuthoringPolicyKind.HandAuthored));

            var plan = CellPopulationPlan.Compute(Grid4x4(), new[] { existing });
            var entry = RequirePopulationEntry(plan, 0, 0);

            Assert.That(entry.CellAction, Is.EqualTo(CellPopulationAction.Skip),
                "HandAuthored かつ Cell AuthoredRoot ありは Skip（本スライスの核心）");
        }

        [Test]
        public void T2b_HandAuthored_WithExistingEnvironmentScene_SkipsEnvironment()
        {
            var existing = State(
                0, 0,
                hasCellAuthoredRoot: true,
                hasEnvironmentScene: true,
                hasEnvironmentAuthoredRoot: true);

            var plan = CellPopulationPlan.Compute(Grid4x4(), new[] { existing });
            var entry = RequirePopulationEntry(plan, 0, 0);

            Assert.That(entry.EnvironmentAction, Is.EqualTo(CellPopulationAction.Skip),
                "HandAuthored かつ Environment .unity 既存は Environment も Skip");
        }

        [Test]
        public void T2c_HandAuthored_WithEnvironmentSceneButNoAuthoredRoot_PopulatesEnvironment()
        {
            // .unity だけあって AuthoredRoot が無い半端状態 → Environment は Populate（自己回復）
            var existing = State(
                0, 0,
                hasCellAuthoredRoot: true,
                hasEnvironmentScene: true,
                hasEnvironmentAuthoredRoot: false);
            Assert.That(CellAuthoringPolicy.Resolve(0, 0), Is.EqualTo(CellAuthoringPolicyKind.HandAuthored));

            var plan = CellPopulationPlan.Compute(Grid4x4(), new[] { existing });
            var entry = RequirePopulationEntry(plan, 0, 0);

            Assert.That(entry.EnvironmentAction, Is.EqualTo(CellPopulationAction.Populate),
                "HandAuthored でも Environment AuthoredRoot 無しなら Environment は Populate");
        }

        [Test]
        public void T3_HandAuthored_WithoutAuthoredRoot_Populates()
        {
            var existing = State(2, 0, hasCellAuthoredRoot: false, hasEnvironmentScene: false);
            Assert.That(CellAuthoringPolicy.Resolve(2, 0), Is.EqualTo(CellAuthoringPolicyKind.HandAuthored));

            var plan = CellPopulationPlan.Compute(Grid4x4(), new[] { existing });
            var entry = RequirePopulationEntry(plan, 2, 0);

            Assert.That(entry.CellAction, Is.EqualTo(CellPopulationAction.Populate),
                "HandAuthored かつ AuthoredRoot なしは初回スキャフォールドとして Populate");
            Assert.That(entry.EnvironmentAction, Is.EqualTo(CellPopulationAction.Populate),
                "hasEnvironmentScene: false なら Environment も Populate（初回スキャフォールド）");
        }

        [Test]
        public void T4_SameInput_Twice_IsIdempotent()
        {
            var existing = new[]
            {
                State(0, 0, hasCellAuthoredRoot: true, hasEnvironmentScene: true, hasEnvironmentAuthoredRoot: true),
                State(1, 1, hasCellAuthoredRoot: false),
                State(3, 3, hasCellAuthoredRoot: true),
            };
            var grid = Grid4x4();

            var first = CellPopulationPlan.Compute(grid, existing);
            var second = CellPopulationPlan.Compute(grid, existing);

            Assert.That(second.PopulationEntries.Count, Is.EqualTo(first.PopulationEntries.Count));
            Assert.That(second.DeletionEntries.Count, Is.EqualTo(first.DeletionEntries.Count));

            foreach (var a in first.PopulationEntries)
            {
                var b = second.PopulationEntries.Single(e => e.Identity == a.Identity);
                Assert.That(b.Coordinate, Is.EqualTo(a.Coordinate));
                Assert.That(b.CellAction, Is.EqualTo(a.CellAction), $"{a.Identity} CellAction が同一");
                Assert.That(b.EnvironmentAction, Is.EqualTo(a.EnvironmentAction),
                    $"{a.Identity} EnvironmentAction が同一");
            }

            foreach (var a in first.DeletionEntries)
            {
                Assert.That(
                    second.DeletionEntries.Any(e => e.Identity == a.Identity && e.Coordinate == a.Coordinate),
                    Is.True,
                    $"削除計画の {a.Identity} が 2 回目にも含まれること");
            }
        }

        [Test]
        public void T5_UnspecifiedPolicy_DefaultsToGenerated()
        {
            // 南辺以外は policy 未指定 → Generated
            Assert.That(CellAuthoringPolicy.Resolve(0, 1), Is.EqualTo(CellAuthoringPolicyKind.Generated));
            Assert.That(CellAuthoringPolicy.Resolve(2, 2), Is.EqualTo(CellAuthoringPolicyKind.Generated));

            var existing = State(2, 2, hasCellAuthoredRoot: true);
            var plan = CellPopulationPlan.Compute(Grid4x4(), new[] { existing });
            var entry = RequirePopulationEntry(plan, 2, 2);

            Assert.That(entry.CellAction, Is.EqualTo(CellPopulationAction.Populate),
                "未指定 policy は Generated 相当で AuthoredRoot 有りでも Populate");
        }

        [Test]
        public void T6_OutOfGridExistingCell_DoesNotAppearInPopulationPlan()
        {
            // 3×3 では (3,0) は範囲外。既存として渡しても Populate 計画には出ない。
            var existing = new[]
            {
                State(1, 1, hasCellAuthoredRoot: false),
                State(3, 0, hasCellAuthoredRoot: true, hasEnvironmentScene: true),
            };

            var plan = CellPopulationPlan.Compute(Grid3x3(), existing);

            Assert.That(plan.PopulationEntries.Any(e => e.Identity == "Cell_3_0"), Is.False,
                "グリッド範囲外の既存 Cell は Populate 計画に現れない");
            Assert.That(plan.PopulationEntries.Any(e => e.Identity == "Cell_1_1"), Is.True,
                "範囲内の既存 Cell は Populate 計画に現れる");
        }

        [Test]
        public void T7_OutOfGrid_HandAuthored_NotDeleted_Generated_IsDeleted()
        {
            // 3×3 縮小: Cell_3_0 は HandAuthored（南辺）、Cell_3_1 は Generated
            var existing = new[]
            {
                State(3, 0, hasCellAuthoredRoot: true, hasEnvironmentScene: true),
                State(3, 1, hasCellAuthoredRoot: false),
            };

            Assert.That(CellAuthoringPolicy.Resolve(3, 0), Is.EqualTo(CellAuthoringPolicyKind.HandAuthored));
            Assert.That(CellAuthoringPolicy.Resolve(3, 1), Is.EqualTo(CellAuthoringPolicyKind.Generated));

            var plan = CellPopulationPlan.Compute(Grid3x3(), existing);

            Assert.That(plan.DeletionEntries.Any(e => e.Identity == "Cell_3_0"), Is.False,
                "範囲外かつ HandAuthored は削除計画に現れない");
            Assert.That(plan.DeletionEntries.Any(e => e.Identity == "Cell_3_1"), Is.True,
                "範囲外かつ Generated は削除計画に現れる");
        }
    }
}
