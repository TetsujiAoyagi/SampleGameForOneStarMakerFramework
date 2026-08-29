#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime.CameraSystem;
using OneStarMaker.Runtime.Streaming;
using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// CAM-09: CameraStreamingFocusAdapter の EditMode テスト。
    /// </summary>
    [TestFixture]
    public class CameraStreamingFocusAdapterTests
    {
        private sealed class FakeCameraView : ICameraView
        {
            public CameraViewSnapshot Snapshot { get; set; }
            public CameraViewSnapshot? IncomingSnapshot { get; set; }

            public CameraStackHandle Push(LogicalCamera camera, CameraLayer layer, in CameraBlendSpec blend) =>
                throw new NotSupportedException();

            public CameraModifierHandle AddModifier(ICameraPoseModifier modifier) =>
                throw new NotSupportedException();
        }

        private static (StreamingCandidateSet Candidates, StreamingPolicySettings Settings) CreateConfig(
            float loadRadius = 150f)
            => (StreamingCandidateFixtures.DenseGrid(5, 5),
                StreamingCandidateFixtures.Settings(loadRadius, unloadRadius: 250f, maxInFlight: 8));

        private static CameraViewSnapshot SnapshotAt(Vector3 position) =>
            CameraViewSnapshot.CreateInitial(new CameraPose
            {
                Position = position,
                Rotation = Quaternion.identity,
                FieldOfViewDegrees = 60f,
                NearClip = 0.3f,
                FarClip = 1000f,
                Aspect = 16f / 9f,
            });

        private static CameraFocusSource Source(ICameraView view, bool includeInStreaming = true) =>
            new() { View = view, IncludeInStreaming = includeInStreaming };

        [Test]
        public void CameraStreamingFocusAdapter_Tick_ForwardsCurrentAndIncomingFocuses()
        {
            var current = new Vector3(0f, 0f, 0f);
            var incoming = new Vector3(400f, 0f, 400f);
            var view = new FakeCameraView
            {
                Snapshot = SnapshotAt(current),
                IncomingSnapshot = SnapshotAt(incoming),
            };

            var (candidates, settings) = CreateConfig(loadRadius: 120f);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(candidates, settings, backend);
            var adapter = new CameraStreamingFocusAdapter(controller, new[] { Source(view) });

            adapter.Tick();

            Assert.That(adapter.LastForwardedFocusCount, Is.EqualTo(2));

            var expected = StreamingCandidateFixtures.UnionWithinRadius(
                new List<Vector3> { current, incoming }, candidates, settings.LoadRadius);
            var requested = backend.AddCalls.Select(c => c.CellId).ToHashSet(StringComparer.Ordinal);

            CollectionAssert.AreEquivalent(expected, requested);
        }

        [Test]
        public void CameraStreamingFocusAdapter_Tick_ExcludesRtViewSource()
        {
            var mainView = new FakeCameraView { Snapshot = SnapshotAt(new Vector3(0f, 0f, 0f)) };
            var rtView = new FakeCameraView { Snapshot = SnapshotAt(new Vector3(400f, 0f, 400f)) };

            var (candidates, settings) = CreateConfig(loadRadius: 120f);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(candidates, settings, backend);
            var adapter = new CameraStreamingFocusAdapter(controller, new[]
            {
                Source(mainView, includeInStreaming: true),
                Source(rtView, includeInStreaming: false),
            });

            adapter.Tick();

            Assert.That(adapter.LastForwardedFocusCount, Is.EqualTo(1));

            var expected = StreamingCandidateFixtures.UnionWithinRadius(
                new List<Vector3> { Vector3.zero }, candidates, settings.LoadRadius);
            var requested = backend.AddCalls.Select(c => c.CellId).ToHashSet(StringComparer.Ordinal);
            CollectionAssert.AreEquivalent(expected, requested);
        }

        [Test]
        public void CameraStreamingFocusAdapter_Tick_WithEmptyIncludedSources_DoesNotCallControllerOrThrowsClearly()
        {
            var rtOnlyView = new FakeCameraView { Snapshot = SnapshotAt(new Vector3(99f, 0f, 99f)) };

            var backend = new FakeStreamingBackend();
            var (candidates, settings) = CreateConfig();
            var controller = new WorldStreamingController(candidates, settings, backend);
            var adapter = new CameraStreamingFocusAdapter(controller, new[]
            {
                Source(rtOnlyView, includeInStreaming: false),
            });

            Assert.DoesNotThrow(() => adapter.Tick());
            Assert.That(adapter.LastForwardedFocusCount, Is.EqualTo(0));
            Assert.That(backend.CallHistory, Is.Empty);
        }

    }
}
