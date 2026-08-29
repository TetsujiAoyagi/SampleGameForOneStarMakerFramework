#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.Streaming;
using UnityEngine;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// 距離政策テストの共有フィクスチャ。
    ///
    /// <para>
    /// 本番と同じく「均一格子から体積を焼く」形にしてある。セルの体積は中心がセル中心の箱なので、
    /// 期待値の数値は座標列だった頃と 1 つも変わらない（移行 M-1 の受入 2 / 3）。
    /// 格子を知っているのはこのフィクスチャだけであり、政策層は identity と体積しか読まない。
    /// </para>
    /// </summary>
    internal static class StreamingCandidateFixtures
    {
        public const float DefaultCellSize = 100f;
        public const float DefaultCellHeight = 10f;

        public static readonly Vector3 Origin = Vector3.zero;

        public static Vector3 CellCenter(int x, int y, float cellSize = DefaultCellSize)
            => Origin + new Vector3((x + 0.5f) * cellSize, 0f, (y + 0.5f) * cellSize);

        public static Bounds CellVolume(int x, int y, float cellSize = DefaultCellSize)
            => new(CellCenter(x, y, cellSize), new Vector3(cellSize, DefaultCellHeight, cellSize));

        public static StreamingCandidateSet DenseGrid(int width, int height, float cellSize = DefaultCellSize)
        {
            var cells = new List<Vector2Int>(width * height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }

            return FromCoordinates(cells, cellSize);
        }

        public static StreamingCandidateSet FromCoordinates(
            IReadOnlyList<Vector2Int> cells,
            float cellSize = DefaultCellSize)
        {
            var candidates = new List<StreamingCandidate>(cells.Count);
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                candidates.Add(new StreamingCandidate(
                    CellIdentity.Format(cell.x, cell.y),
                    CellVolume(cell.x, cell.y, cellSize)));
            }

            return new StreamingCandidateSet(candidates);
        }

        public static StreamingPolicySettings Settings(
            float loadRadius = 150f,
            float unloadRadius = 250f,
            int maxInFlight = 4)
            => new(loadRadius, unloadRadius, maxInFlight);

        public static float XzDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static float NearestFocusDistance(IReadOnlyList<Vector3> focuses, Vector3 point)
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

        /// <summary>候補の体積中心を identity で引く（priority 検証用）。</summary>
        public static Vector3 CenterOf(StreamingCandidateSet candidates, string identity)
        {
            var list = candidates.Candidates;
            for (var i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Identity, identity, StringComparison.Ordinal))
                {
                    return list[i].Volume.center;
                }
            }

            throw new KeyNotFoundException($"候補に '{identity}' がありません。");
        }

        /// <summary>注視点から半径内にある候補 identity（体積中心の XZ 距離）。</summary>
        public static HashSet<string> WithinRadius(
            Vector3 focus,
            StreamingCandidateSet candidates,
            float radius)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var list = candidates.Candidates;

            for (var i = 0; i < list.Count; i++)
            {
                if (XzDistance(focus, list[i].Volume.center) <= radius)
                {
                    result.Add(list[i].Identity);
                }
            }

            return result;
        }

        /// <summary>複数注視点の和集合（CAM-08）。</summary>
        public static HashSet<string> UnionWithinRadius(
            IReadOnlyList<Vector3> focuses,
            StreamingCandidateSet candidates,
            float radius)
        {
            var union = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < focuses.Count; i++)
            {
                union.UnionWith(WithinRadius(focuses[i], candidates, radius));
            }

            return union;
        }
    }
}
