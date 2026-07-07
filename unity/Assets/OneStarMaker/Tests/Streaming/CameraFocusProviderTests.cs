#nullable enable

using System.Collections.Generic;
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
    /// CAM-08: CameraFocusProvider のレッドテスト。
    /// ICameraView 群から注視点集合を構成し、ブレンド先読みと RT 除外を検証する。
    /// </summary>
    [TestFixture]
    public class CameraFocusProviderTests
    {
        private sealed class FakeCameraView : ICameraView
        {
            public CameraViewSnapshot Snapshot { get; set; }
            public CameraViewSnapshot? IncomingSnapshot { get; set; }

            public CameraStackHandle Push(LogicalCamera camera, CameraLayer layer, in CameraBlendSpec blend) =>
                throw new System.NotSupportedException();

            public CameraModifierHandle AddModifier(ICameraPoseModifier modifier) =>
                throw new System.NotSupportedException();
        }

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
        public void Provider_SingleView_YieldsOnePosition()
        {
            var view = new FakeCameraView
            {
                Snapshot = SnapshotAt(new Vector3(10f, 0f, 20f)),
            };
            var provider = new CameraFocusProvider();

            var positions = provider.CollectFocusPositions(new[] { Source(view) });

            Assert.That(positions, Has.Count.EqualTo(1));
            Assert.That(positions[0], Is.EqualTo(new Vector3(10f, 0f, 20f)));
        }

        [Test]
        public void Provider_TwoViews_YieldsBothPositions()
        {
            var viewA = new FakeCameraView { Snapshot = SnapshotAt(new Vector3(0f, 0f, 0f)) };
            var viewB = new FakeCameraView { Snapshot = SnapshotAt(new Vector3(100f, 0f, 200f)) };
            var provider = new CameraFocusProvider();

            var positions = provider.CollectFocusPositions(new[]
            {
                Source(viewA),
                Source(viewB),
            });

            Assert.That(positions, Has.Count.EqualTo(2));
            Assert.That(positions[0], Is.EqualTo(new Vector3(0f, 0f, 0f)));
            Assert.That(positions[1], Is.EqualTo(new Vector3(100f, 0f, 200f)));
        }

        [Test]
        public void Provider_Blending_IncludesIncomingPosition()
        {
            var current = new Vector3(0f, 0f, 0f);
            var incoming = new Vector3(500f, 0f, 500f);
            var view = new FakeCameraView
            {
                Snapshot = SnapshotAt(current),
                IncomingSnapshot = SnapshotAt(incoming),
            };
            var provider = new CameraFocusProvider();

            var positions = provider.CollectFocusPositions(new[] { Source(view) });

            Assert.That(positions, Has.Count.EqualTo(2));
            Assert.That(positions[0], Is.EqualTo(current));
            Assert.That(positions[1], Is.EqualTo(incoming));
        }

        [Test]
        public void Provider_RtView_ExcludedByConfig()
        {
            var mainView = new FakeCameraView { Snapshot = SnapshotAt(new Vector3(1f, 0f, 1f)) };
            var rtView = new FakeCameraView { Snapshot = SnapshotAt(new Vector3(99f, 0f, 99f)) };
            var provider = new CameraFocusProvider();

            var positions = provider.CollectFocusPositions(new[]
            {
                Source(mainView, includeInStreaming: true),
                Source(rtView, includeInStreaming: false),
            });

            Assert.That(positions, Has.Count.EqualTo(1));
            Assert.That(positions[0], Is.EqualTo(new Vector3(1f, 0f, 1f)));
        }
    }
}
