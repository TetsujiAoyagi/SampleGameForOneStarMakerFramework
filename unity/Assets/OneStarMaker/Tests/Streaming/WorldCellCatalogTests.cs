#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using SampleGame.InGame.Streaming;
using UnityEngine;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// S-3: WorldCellCatalog の矩形集合 membership（T-A / T-D）。
    /// </summary>
    [TestFixture]
    public sealed class WorldCellCatalogTests
    {
        [Test]
        public void TA_SeparatedRectangles_NearestCrossRectCenterExceedsUnloadRadius()
        {
            // 空隙 2 セル: (0,0) 2×2 と (4,0) 2×2。最近中心間は 750m > UnloadRadius 550。
            var rectangles = new[]
            {
                new CellRect(Vector2Int.zero, new Vector2Int(2, 2)),
                new CellRect(new Vector2Int(4, 0), new Vector2Int(2, 2)),
            };

            Assert.That(
                MinCrossRectCenterDistance(rectangles, WorldCellCatalog.Origin, WorldCellCatalog.CellSize),
                Is.GreaterThan(WorldCellCatalog.UnloadRadius));
        }

        [Test]
        public void TA_AdjacentRectangles_AreDetectedAsGapViolation()
        {
            // 隣接: 中心間 250m <= UnloadRadius 550。違反をテスト側で検出できること。
            var rectangles = new[]
            {
                new CellRect(Vector2Int.zero, new Vector2Int(2, 2)),
                new CellRect(new Vector2Int(2, 0), new Vector2Int(2, 2)),
            };

            Assert.That(
                MinCrossRectCenterDistance(rectangles, WorldCellCatalog.Origin, WorldCellCatalog.CellSize),
                Is.LessThanOrEqualTo(WorldCellCatalog.UnloadRadius));
        }

        [Test]
        public void TD_TryGetCoordinate_FourCornersTrue_OutsideFalse()
        {
            Assert.That(WorldCellCatalog.Rectangles.Length, Is.EqualTo(1), "本番矩形数は 1");
            Assert.That(WorldCellCatalog.Rectangles[0].Origin, Is.EqualTo(Vector2Int.zero));
            Assert.That(WorldCellCatalog.Rectangles[0].Size, Is.EqualTo(new Vector2Int(4, 4)));

            Assert.That(WorldCellCatalog.TryGetCoordinate(WorldCellCatalog.GetCellCenter(0, 0), out var sw), Is.True);
            Assert.That(sw, Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(WorldCellCatalog.TryGetCoordinate(WorldCellCatalog.GetCellCenter(3, 0), out var se), Is.True);
            Assert.That(se, Is.EqualTo(new Vector2Int(3, 0)));
            Assert.That(WorldCellCatalog.TryGetCoordinate(WorldCellCatalog.GetCellCenter(0, 3), out var nw), Is.True);
            Assert.That(nw, Is.EqualTo(new Vector2Int(0, 3)));
            Assert.That(WorldCellCatalog.TryGetCoordinate(WorldCellCatalog.GetCellCenter(3, 3), out var ne), Is.True);
            Assert.That(ne, Is.EqualTo(new Vector2Int(3, 3)));

            Assert.That(
                WorldCellCatalog.TryGetCoordinate(new Vector3(-1f, 0f, 0f), out _),
                Is.False,
                "グリッド左外は false");
            Assert.That(
                WorldCellCatalog.TryGetCoordinate(new Vector3(4f * WorldCellCatalog.CellSize + 1f, 0f, 0f), out _),
                Is.False,
                "グリッド右外は false");
        }

        [Test]
        public void TD_FixtureGapCoordinate_IsOutsideMembership()
        {
            var rectangles = new[]
            {
                new CellRect(Vector2Int.zero, new Vector2Int(2, 2)),
                new CellRect(new Vector2Int(4, 0), new Vector2Int(2, 2)),
            };

            var gapWorld = WorldCellCatalog.GetCellCenter(2, 0);
            Assert.That(
                TryGetCoordinateOnFixture(gapWorld, rectangles),
                Is.False,
                "AABB 内でも空隙なら false");

            var inRect = WorldCellCatalog.GetCellCenter(1, 1);
            Assert.That(TryGetCoordinateOnFixture(inRect, rectangles, out var coordinate), Is.True);
            Assert.That(coordinate, Is.EqualTo(new Vector2Int(1, 1)));
        }

        private static bool TryGetCoordinateOnFixture(
            Vector3 worldPosition,
            IReadOnlyList<CellRect> rectangles)
            => TryGetCoordinateOnFixture(worldPosition, rectangles, out _);

        private static bool TryGetCoordinateOnFixture(
            Vector3 worldPosition,
            IReadOnlyList<CellRect> rectangles,
            out Vector2Int coordinate)
        {
            var local = worldPosition - WorldCellCatalog.Origin;
            var x = Mathf.FloorToInt(local.x / WorldCellCatalog.CellSize);
            var y = Mathf.FloorToInt(local.z / WorldCellCatalog.CellSize);
            coordinate = new Vector2Int(x, y);
            for (var i = 0; i < rectangles.Count; i++)
            {
                if (rectangles[i].Contains(coordinate))
                {
                    return true;
                }
            }

            coordinate = default;
            return false;
        }

        private static float MinCrossRectCenterDistance(
            IReadOnlyList<CellRect> rectangles,
            Vector3 origin,
            float cellSize)
        {
            var min = float.MaxValue;
            for (var a = 0; a < rectangles.Count; a++)
            {
                for (var b = a + 1; b < rectangles.Count; b++)
                {
                    foreach (var cellA in Enumerate(rectangles[a]))
                    {
                        foreach (var cellB in Enumerate(rectangles[b]))
                        {
                            var ca = origin + new Vector3(
                                (cellA.x + 0.5f) * cellSize,
                                0f,
                                (cellA.y + 0.5f) * cellSize);
                            var cb = origin + new Vector3(
                                (cellB.x + 0.5f) * cellSize,
                                0f,
                                (cellB.y + 0.5f) * cellSize);
                            var dx = ca.x - cb.x;
                            var dz = ca.z - cb.z;
                            var distance = Mathf.Sqrt((dx * dx) + (dz * dz));
                            if (distance < min)
                            {
                                min = distance;
                            }
                        }
                    }
                }
            }

            return min;
        }

        private static IEnumerable<Vector2Int> Enumerate(CellRect rect)
        {
            for (var y = 0; y < rect.Size.y; y++)
            {
                for (var x = 0; x < rect.Size.x; x++)
                {
                    yield return new Vector2Int(rect.Origin.x + x, rect.Origin.y + y);
                }
            }
        }
    }
}
