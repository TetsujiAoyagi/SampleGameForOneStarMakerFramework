#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.Streaming;
using SampleGame.DependOnAll.Editor.Cells;
using UnityEngine;

namespace OneStarMaker.Tests.Editor
{
    [TestFixture]
    public sealed class CellPopulationPlanTests
    {
        private static IReadOnlyList<WorldCellGenerationTarget> Grid4x4()
            => Targets(new Vector2Int(4, 4));

        private static IReadOnlyList<WorldCellGenerationTarget> Grid3x3()
            => Targets(new Vector2Int(3, 3));

        private static IReadOnlyList<WorldCellGenerationTarget> Targets(Vector2Int size)
        {
            var result = new List<WorldCellGenerationTarget>();
            for (var y = 0; y < size.y; y++)
            {
                for (var x = 0; x < size.x; x++)
                {
                    result.Add(new WorldCellGenerationTarget($"Cell_{x}_{y}", new Vector2Int(x, y)));
                }
            }

            return result;
        }

        private static CellExistingState State(
            string identity,
            bool hasCellAuthoredRoot,
            bool hasEnvironmentScene = false,
            bool hasEnvironmentAuthoredRoot = false)
            => new(identity, hasCellAuthoredRoot, hasEnvironmentScene, hasEnvironmentAuthoredRoot);

        private static CellPopulationEntry Require(CellPopulationPlan plan, string identity)
        {
            var entry = plan.PopulationEntries.SingleOrDefault(e => e.Identity == identity);
            Assert.That(entry, Is.Not.Null, $"Populate 計画に {identity} が含まれること");
            return entry!;
        }

        [Test]
        public void Generated_PopulatesRegardlessOfAuthoredRoot()
        {
            Assert.That(CellAuthoringPolicy.Resolve("Cell_1_1"), Is.EqualTo(CellAuthoringPolicyKind.Generated));
            var noRoot = CellPopulationPlan.Compute(Grid4x4(), new[] { State("Cell_1_1", false) });
            var withRoot = CellPopulationPlan.Compute(Grid4x4(), new[] { State("Cell_1_1", true) });
            Assert.That(Require(noRoot, "Cell_1_1").CellAction, Is.EqualTo(CellPopulationAction.Populate));
            Assert.That(Require(withRoot, "Cell_1_1").CellAction, Is.EqualTo(CellPopulationAction.Populate));
        }

        [Test]
        public void HandAuthored_CellAndEnvironmentAreIndependent()
        {
            var plan = CellPopulationPlan.Compute(
                Grid4x4(), new[] { State("Cell_0_0", false, true, true) });
            Assert.That(CellAuthoringPolicy.Resolve("Cell_0_0"), Is.EqualTo(CellAuthoringPolicyKind.HandAuthored));
            Assert.That(Require(plan, "Cell_0_0").CellAction, Is.EqualTo(CellPopulationAction.Populate));
            Assert.That(Require(plan, "Cell_0_0").EnvironmentAction, Is.EqualTo(CellPopulationAction.Skip));
        }

        [Test]
        public void HandAuthored_ExistingRootSkipsAndIncompleteEnvironmentPopulates()
        {
            var plan = CellPopulationPlan.Compute(
                Grid4x4(), new[] { State("Cell_0_0", true, true, false) });
            Assert.That(Require(plan, "Cell_0_0").CellAction, Is.EqualTo(CellPopulationAction.Skip));
            Assert.That(Require(plan, "Cell_0_0").EnvironmentAction, Is.EqualTo(CellPopulationAction.Populate));
        }

        [Test]
        public void SameCoordinateDifferentIdentitiesRemainIndependent()
        {
            var targets = new[]
            {
                new WorldCellGenerationTarget("Cell_0_0", new Vector2Int(0, 0)),
                new WorldCellGenerationTarget("Arbitrary_A", new Vector2Int(0, 0)),
            };
            var plan = CellPopulationPlan.Compute(
                targets,
                new[] { State("Cell_0_0", true), State("Arbitrary_A", false) });
            Assert.That(plan.PopulationEntries.Select(e => e.Identity), Is.EquivalentTo(new[] { "Cell_0_0", "Arbitrary_A" }));
            Assert.That(Require(plan, "Cell_0_0").CellAction, Is.EqualTo(CellPopulationAction.Skip));
            Assert.That(Require(plan, "Arbitrary_A").CellAction, Is.EqualTo(CellPopulationAction.Populate));
        }

        [Test]
        public void DuplicateTargetOrExistingIdentityThrows()
        {
            var target = new WorldCellGenerationTarget("Arbitrary", Vector2Int.zero);
            Assert.Throws<System.ArgumentException>(() => CellPopulationPlan.Compute(
                new[] { target, target }, new CellExistingState[0]));
            Assert.Throws<System.ArgumentException>(() => CellPopulationPlan.Compute(
                new[] { target }, new[] { State("Arbitrary", false), State("Arbitrary", true) }));
        }

        [Test]
        public void IdentityComparisonIsOrdinal()
        {
            var targets = new[]
            {
                new WorldCellGenerationTarget("Cell_0_0", Vector2Int.zero),
                new WorldCellGenerationTarget("cell_0_0", Vector2Int.one),
            };
            var plan = CellPopulationPlan.Compute(targets, new CellExistingState[0]);
            Assert.That(plan.PopulationEntries, Has.Count.EqualTo(2));
            Assert.That(CellAuthoringPolicy.Resolve("cell_0_0"), Is.EqualTo(CellAuthoringPolicyKind.Generated));
        }

        [Test]
        public void ExistingSameCoordinateDifferentIdentityIsNotExistingTarget()
        {
            var target = new WorldCellGenerationTarget("Arbitrary", new Vector2Int(3, 3));
            var plan = CellPopulationPlan.Compute(
                new[] { target }, new[] { State("Other", true) });
            Assert.That(Require(plan, "Arbitrary").CellAction, Is.EqualTo(CellPopulationAction.Populate));
            Assert.That(plan.DeletionEntries.Single().Identity, Is.EqualTo("Other"));
        }

        [Test]
        public void SameInputTwiceIsIdempotent()
        {
            var existing = new[]
            {
                State("Cell_0_0", true, true, true),
                State("Cell_1_1", false),
                State("Cell_3_3", true),
            };
            var first = CellPopulationPlan.Compute(Grid4x4(), existing);
            var second = CellPopulationPlan.Compute(Grid4x4(), existing);
            Assert.That(second.PopulationEntries.Count, Is.EqualTo(first.PopulationEntries.Count));
            Assert.That(second.DeletionEntries.Select(e => e.Identity),
                Is.EquivalentTo(first.DeletionEntries.Select(e => e.Identity)));
            foreach (var entry in first.PopulationEntries)
            {
                var other = Require(second, entry.Identity);
                Assert.That(other.Coordinate, Is.EqualTo(entry.Coordinate));
                Assert.That(other.CellAction, Is.EqualTo(entry.CellAction));
                Assert.That(other.EnvironmentAction, Is.EqualTo(entry.EnvironmentAction));
            }
        }

        [Test]
        public void OutOfTargetExistingIsAbsentFromPopulation()
        {
            var plan = CellPopulationPlan.Compute(
                Grid3x3(), new[] { State("Cell_3_0", true), State("Cell_1_1", false) });
            Assert.That(plan.PopulationEntries.Any(e => e.Identity == "Cell_3_0"), Is.False);
            Assert.That(plan.PopulationEntries.Any(e => e.Identity == "Cell_1_1"), Is.True);
        }

        [Test]
        public void OutOfTargetShouldPopulateEnvironmentIsFalse()
        {
            var plan = CellPopulationPlan.Compute(
                Grid3x3(), new[] { State("Cell_3_0", true, true, true) });
            Assert.That(plan.ShouldPopulateEnvironment("Cell_3_0"), Is.False);
        }

        [Test]
        public void SameCoordinateGapUsesIdentitySetNotGeometry()
        {
            var targets = new[]
            {
                new WorldCellGenerationTarget("Cell_0_0", Vector2Int.zero),
                new WorldCellGenerationTarget("Cell_4_0", new Vector2Int(4, 0)),
            };
            var plan = CellPopulationPlan.Compute(
                targets,
                new[] { State("Cell_3_0", true), State("Cell_3_1", false) });
            Assert.That(plan.PopulationEntries.Select(e => e.Identity),
                Is.EquivalentTo(new[] { "Cell_0_0", "Cell_4_0" }));
            Assert.That(plan.IsDeletable("Cell_3_0"), Is.False);
            Assert.That(plan.IsDeletable("Cell_3_1"), Is.True);
        }

        [Test]
        public void DeletionAndEnvironmentQueriesAreIdentityBased()
        {
            var plan = CellPopulationPlan.Compute(
                Grid3x3(), new[] { State("Cell_3_1", false), State("Arbitrary_Old", false) });
            Assert.That(plan.IsDeletable("Cell_3_1"), Is.True);
            Assert.That(plan.IsDeletable("Arbitrary_Old"), Is.True);
            Assert.That(plan.IsDeletable("Cell_1_1"), Is.False);
            Assert.That(plan.ShouldPopulateEnvironment("Cell_1_1"), Is.True);
            Assert.That(plan.ShouldPopulateEnvironment("Missing"), Is.False);
        }

        [Test]
        public void OutOfTargetHandAuthoredIdentityIsRetained()
        {
            var plan = CellPopulationPlan.Compute(
                Grid3x3(), new[] { State("Cell_3_0", true), State("Cell_3_1", false) });
            Assert.That(plan.IsDeletable("Cell_3_0"), Is.False);
            Assert.That(plan.IsDeletable("Cell_3_1"), Is.True);
        }
    }
}
