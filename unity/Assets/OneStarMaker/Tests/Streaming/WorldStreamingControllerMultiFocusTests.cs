#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
        private static (StreamingCandidateSet Candidates, StreamingPolicySettings Settings) CreateConfig(
            int gridWidth = 5,
            int gridHeight = 5,
            float cellSize = 100f,
            float loadRadius = 150f,
            float unloadRadius = 250f,
            int maxInFlight = 8)
            => (StreamingCandidateFixtures.DenseGrid(gridWidth, gridHeight, cellSize),
                StreamingCandidateFixtures.Settings(loadRadius, unloadRadius, maxInFlight));

        private static Vector3 CellCenter(int x, int y) => StreamingCandidateFixtures.CellCenter(x, y);

        [Test]
        public void Tick_MultiFocus_DesiredSetIsUnion()
        {
            var (candidates, settings) = CreateConfig(loadRadius: 120f, unloadRadius: 220f);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(candidates, settings, backend);

            var focusA = CellCenter(0, 0);
            var focusB = CellCenter(4, 4);
            var focuses = new List<Vector3> { focusA, focusB };

            controller.Tick(focuses);

            var requested = backend.AddCalls.Select(c => c.CellId).ToHashSet(StringComparer.Ordinal);
            var expected = StreamingCandidateFixtures.UnionWithinRadius(focuses, candidates, settings.LoadRadius);

            CollectionAssert.AreEquivalent(expected, requested);
        }

        [Test]
        public void Tick_MultiFocus_PriorityUsesNearestFocusDistance()
        {
            var (candidates, settings) = CreateConfig(loadRadius: 300f, unloadRadius: 400f);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(candidates, settings, backend);

            var focusA = CellCenter(0, 0);
            var focusB = CellCenter(4, 4);
            var focuses = new List<Vector3> { focusA, focusB };

            controller.Tick(focuses);

            var adds = backend.AddCalls.ToList();
            Assert.Greater(adds.Count, 1, "複数セルがロード対象になる設定であること");

            var nearestDistances = adds
                .Select(c => StreamingCandidateFixtures.NearestFocusDistance(
                    focuses, StreamingCandidateFixtures.CenterOf(candidates, c.CellId)))
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
            var (candidates, settings) = CreateConfig(loadRadius: 150f, unloadRadius: 250f);
            var focus = CellCenter(2, 2);

            // Tick(Vector3) は単一 focus 専用バッファ経由でも、複数 focus overload 1 件入力と同じ差分を出す。
            var legacyBackend = new FakeStreamingBackend();
            var legacyController = new WorldStreamingController(candidates, settings, legacyBackend);
            legacyController.Tick(focus);
            var legacyAdds = legacyBackend.AddCalls
                .Select(c => (c.CellId, c.Priority))
                .ToList();
            var legacyRemoves = legacyBackend.RemoveCalls.Select(c => c.CellId).ToList();

            var overloadBackend = new FakeStreamingBackend();
            var overloadController = new WorldStreamingController(candidates, settings, overloadBackend);
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
