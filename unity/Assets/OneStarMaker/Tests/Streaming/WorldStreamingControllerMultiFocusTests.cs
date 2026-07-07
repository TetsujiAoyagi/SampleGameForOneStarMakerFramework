#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.Streaming;
using UnityEngine;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// CAM-08: WorldStreamingController の複数注視点対応レッドテスト。
    /// 既存単一 focus API の挙動不変（G-4）も検証する。
    /// </summary>
    [TestFixture]
    public class WorldStreamingControllerMultiFocusTests
    {
        private static StreamingConfig CreateConfig(
            int gridWidth = 5,
            int gridHeight = 5,
            float cellSize = 100f,
            float loadRadius = 150f,
            float unloadRadius = 250f,
            int maxInFlight = 8)
        {
            var grid = new CellGridConfig(Vector3.zero, cellSize, height: 10f);
            return new StreamingConfig(grid, gridWidth, gridHeight, loadRadius, unloadRadius, maxInFlight);
        }

        private static Vector3 CellCenter(int x, int y, in CellGridConfig grid)
        {
            return grid.Origin + new Vector3(
                (x + 0.5f) * grid.CellSize,
                0f,
                (y + 0.5f) * grid.CellSize);
        }

        private static float XzDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float NearestFocusDistance(IReadOnlyList<Vector3> focuses, Vector3 point)
        {
            var nearest = float.MaxValue;
            for (var i = 0; i < focuses.Count; i++)
            {
                var distance = XzDistance(focuses[i], point);
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            return nearest;
        }

        private static HashSet<string> ComputeCellsWithinRadius(
            Vector3 focus,
            StreamingConfig config,
            float radius)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var grid = config.Grid;

            for (var x = 0; x < config.GridWidth; x++)
            {
                for (var y = 0; y < config.GridHeight; y++)
                {
                    var center = CellCenter(x, y, grid);
                    if (XzDistance(focus, center) <= radius)
                    {
                        result.Add(CellIdentity.Format(x, y));
                    }
                }
            }

            return result;
        }

        private static HashSet<string> ComputeUnionDesired(
            IReadOnlyList<Vector3> focuses,
            StreamingConfig config)
        {
            var union = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < focuses.Count; i++)
            {
                union.UnionWith(ComputeCellsWithinRadius(focuses[i], config, config.LoadRadius));
            }

            return union;
        }

        [Test]
        public void Tick_MultiFocus_DesiredSetIsUnion()
        {
            var config = CreateConfig(loadRadius: 120f, unloadRadius: 220f);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(config, backend);

            var focusA = CellCenter(0, 0, config.Grid);
            var focusB = CellCenter(4, 4, config.Grid);
            var focuses = new List<Vector3> { focusA, focusB };

            controller.Tick(focuses);

            var requested = backend.AddCalls.Select(c => c.CellId).ToHashSet(StringComparer.Ordinal);
            var expected = ComputeUnionDesired(focuses, config);

            CollectionAssert.AreEquivalent(expected, requested);
        }

        [Test]
        public void Tick_MultiFocus_PriorityUsesNearestFocusDistance()
        {
            var config = CreateConfig(loadRadius: 300f, unloadRadius: 400f);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(config, backend);

            var focusA = CellCenter(0, 0, config.Grid);
            var focusB = CellCenter(4, 4, config.Grid);
            var focuses = new List<Vector3> { focusA, focusB };

            controller.Tick(focuses);

            var adds = backend.AddCalls.ToList();
            Assert.Greater(adds.Count, 1, "複数セルがロード対象になる設定であること");

            var grid = config.Grid;
            var nearestDistances = adds
                .Select(c =>
                {
                    Assert.IsTrue(CellIdentity.TryParse(c.CellId, out var coord));
                    var center = CellCenter(coord.x, coord.y, grid);
                    return NearestFocusDistance(focuses, center);
                })
                .ToList();

            for (var i = 1; i < nearestDistances.Count; i++)
            {
                Assert.LessOrEqual(nearestDistances[i - 1], nearestDistances[i],
                    "RequestAdd の発行順は最寄り focus への距離昇順である");
            }

            CollectionAssert.AreEqual(
                Enumerable.Range(0, adds.Count).ToList(),
                adds.Select(c => c.Priority).ToList(),
                "priority は Tick 内の発行順序数（0 始まりの最寄り距離順ランク）");
        }

        [Test]
        public void Tick_SingleFocusOverload_BehaviorUnchanged()
        {
            var config = CreateConfig(loadRadius: 150f, unloadRadius: 250f);
            var focus = CellCenter(2, 2, config.Grid);

            // Tick(Vector3) は単一 focus 専用バッファ経由でも、複数 focus overload 1 件入力と同じ差分を出す。
            var legacyBackend = new FakeStreamingBackend();
            var legacyController = new WorldStreamingController(config, legacyBackend);
            legacyController.Tick(focus);
            var legacyAdds = legacyBackend.AddCalls
                .Select(c => (c.CellId, c.Priority))
                .ToList();
            var legacyRemoves = legacyBackend.RemoveCalls.Select(c => c.CellId).ToList();

            var overloadBackend = new FakeStreamingBackend();
            var overloadController = new WorldStreamingController(config, overloadBackend);
            overloadController.Tick(new List<Vector3> { focus });
            var overloadAdds = overloadBackend.AddCalls
                .Select(c => (c.CellId, c.Priority))
                .ToList();
            var overloadRemoves = overloadBackend.RemoveCalls.Select(c => c.CellId).ToList();

            CollectionAssert.AreEqual(legacyAdds, overloadAdds,
                "単一 focus の overload は既存 Tick(Vector3) と同一の RequestAdd を発行する");
            CollectionAssert.AreEqual(legacyRemoves, overloadRemoves,
                "単一 focus の overload は既存 Tick(Vector3) と同一の RequestRemove を発行する");
        }
    }
}
