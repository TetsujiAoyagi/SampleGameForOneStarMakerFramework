#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime.CameraSystem;
using OneStarMaker.Runtime.SceneSystem;
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

        private static StreamingConfig CreateConfig(float loadRadius = 150f)
        {
            var grid = new CellGridConfig(Vector3.zero, cellSize: 100f, height: 10f);
            return new StreamingConfig(grid, DenseCells(5, 5), loadRadius, unloadRadius: 250f, maxInFlight: 8);
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

        private static Vector3 CellCenter(int x, int y, in CellGridConfig grid) =>
            grid.Origin + new Vector3((x + 0.5f) * grid.CellSize, 0f, (y + 0.5f) * grid.CellSize);

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

            var config = CreateConfig(loadRadius: 120f);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(config, backend);
            var adapter = new CameraStreamingFocusAdapter(controller, new[] { Source(view) });

            adapter.Tick();

            Assert.That(adapter.LastForwardedFocusCount, Is.EqualTo(2));

            var expected = ComputeUnionDesired(new List<Vector3> { current, incoming }, config);
            var requested = backend.AddCalls.Select(c => c.CellId).ToHashSet(StringComparer.Ordinal);

            CollectionAssert.AreEquivalent(expected, requested);
        }

        [Test]
        public void CameraStreamingFocusAdapter_Tick_ExcludesRtViewSource()
        {
            var mainView = new FakeCameraView { Snapshot = SnapshotAt(new Vector3(0f, 0f, 0f)) };
            var rtView = new FakeCameraView { Snapshot = SnapshotAt(new Vector3(400f, 0f, 400f)) };

            var config = CreateConfig(loadRadius: 120f);
            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(config, backend);
            var adapter = new CameraStreamingFocusAdapter(controller, new[]
            {
                Source(mainView, includeInStreaming: true),
                Source(rtView, includeInStreaming: false),
            });

            adapter.Tick();

            Assert.That(adapter.LastForwardedFocusCount, Is.EqualTo(1));

            var expected = ComputeUnionDesired(new List<Vector3> { Vector3.zero }, config);
            var requested = backend.AddCalls.Select(c => c.CellId).ToHashSet(StringComparer.Ordinal);
            CollectionAssert.AreEquivalent(expected, requested);
        }

        [Test]
        public void CameraStreamingFocusAdapter_Tick_WithEmptyIncludedSources_DoesNotCallControllerOrThrowsClearly()
        {
            var rtOnlyView = new FakeCameraView { Snapshot = SnapshotAt(new Vector3(99f, 0f, 99f)) };

            var backend = new FakeStreamingBackend();
            var controller = new WorldStreamingController(CreateConfig(), backend);
            var adapter = new CameraStreamingFocusAdapter(controller, new[]
            {
                Source(rtOnlyView, includeInStreaming: false),
            });

            Assert.DoesNotThrow(() => adapter.Tick());
            Assert.That(adapter.LastForwardedFocusCount, Is.EqualTo(0));
            Assert.That(backend.CallHistory, Is.Empty);
        }

        private static HashSet<string> ComputeUnionDesired(IReadOnlyList<Vector3> focuses, StreamingConfig config)
        {
            var union = new HashSet<string>(StringComparer.Ordinal);
            var grid = config.Grid;

            for (var i = 0; i < focuses.Count; i++)
            {
                for (var c = 0; c < config.Cells.Count; c++)
                {
                    var cell = config.Cells[c];
                    var center = CellCenter(cell.x, cell.y, grid);
                    var dx = focuses[i].x - center.x;
                    var dz = focuses[i].z - center.z;
                    var distance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance <= config.LoadRadius)
                    {
                        union.Add(CellIdentity.Format(cell.x, cell.y));
                    }
                }
            }

            return union;
        }

        private static IReadOnlyList<Vector2Int> DenseCells(int width, int height)
        {
            var cells = new Vector2Int[width * height];
            var i = 0;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    cells[i++] = new Vector2Int(x, y);
                }
            }

            return cells;
        }
    }
}
