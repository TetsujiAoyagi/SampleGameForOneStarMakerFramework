#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace SampleGame.InGame.Streaming
{
    /// <summary>
    /// セル格子上の軸揃え矩形。幅・高さは 1 以上。
    /// </summary>
    public readonly struct CellRect
    {
        public CellRect(Vector2Int origin, Vector2Int size)
        {
            if (size.x < 1 || size.y < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(size),
                    size,
                    "矩形サイズは幅・高さとも 1 以上である必要があります。");
            }

            Origin = origin;
            Size = size;
        }

        public Vector2Int Origin { get; }

        /// <summary>x = 幅, y = 高さ。どちらも 1 以上。</summary>
        public Vector2Int Size { get; }

        public bool Contains(Vector2Int coordinate)
        {
            return coordinate.x >= Origin.x
                && coordinate.y >= Origin.y
                && coordinate.x < Origin.x + Size.x
                && coordinate.y < Origin.y + Size.y;
        }
    }

    /// <summary>
    /// 実証スライス用のワールド格子定数。
    /// Player カプセル（高さ約 2.2m）を基準に、人間が編集する作業単位として
    /// <see cref="CellSize"/> = 250m / 4×4 を採用する。
    /// アセット側の <c>WorldGridDefinition</c> と数値を食い違わせないこと。
    /// </summary>
    public static class WorldCellCatalog
    {
        /// <summary>セル親コンテナの identity。</summary>
        public const string WorldIdentity = "World";

        /// <summary>グリッド原点（Cell_0_0 の最小コーナー）。</summary>
        public static readonly Vector3 Origin = Vector3.zero;

        /// <summary>1 セルの XZ 一辺（メートル）。Unity 1 単位 = 1m。</summary>
        public const float CellSize = 250f;

        /// <summary>バウンズ計算用のセル高さ（ロード判断には使わない）。</summary>
        public const float CellHeight = 96f;

        /// <summary>
        /// 本番レイアウト。現行 4×4 を矩形 1 個として残す。
        /// </summary>
        public static readonly CellRect[] Rectangles =
        {
            new(new Vector2Int(0, 0), new Vector2Int(4, 4)),
        };

        /// <summary>
        /// desired set に入れる距離。
        /// セル中心間 250m に対し、近傍 1 リング程度が載るよう約 1.5 セル分。
        /// </summary>
        public const float LoadRadius = 375f;

        /// <summary>retain に残す距離。LoadRadius より大きくして境界振動を防ぐ。</summary>
        public const float UnloadRadius = 550f;

        /// <summary>同時 in-flight Add の上限。</summary>
        public const int MaxInFlight = 2;

        /// <summary>Tick 間引き間隔（秒）。正典の「5Hz 相当」。</summary>
        public const float TickIntervalSeconds = 0.2f;

        /// <summary>スポーン高度（床の上）。</summary>
        public const float SpawnHeight = 28f;

        /// <summary>セルの地形モチーフ数（Editor 焼き込みとランタイム tint の契約）。</summary>
        public const int MotifCount = 4;

        private static readonly Vector2Int[] ExpandedCells = ExpandAndValidate(Rectangles);
        private static readonly HashSet<Vector2Int> CellMembership = new(ExpandedCells);

        /// <summary>ランタイム / Editor 双方で使う格子メタデータ。</summary>
        public static CellGridConfig CreateGridConfig()
            => new(Origin, CellSize, CellHeight);

        /// <summary>矩形集合を展開したセル座標。</summary>
        public static IReadOnlyList<Vector2Int> EnumerateCells() => ExpandedCells;

        /// <summary>指定セルのワールド中心（XZ）。Y は 0。</summary>
        public static Vector3 GetCellCenter(int x, int y)
        {
            return Origin + new Vector3(
                (x + 0.5f) * CellSize,
                0f,
                (y + 0.5f) * CellSize);
        }

        /// <summary>プレイヤー初期スポーン。Cell_0_0 中心上空。</summary>
        public static Vector3 SpawnPosition()
            => GetCellCenter(0, 0) + Vector3.up * SpawnHeight;

        /// <summary>
        /// ワールド座標からセル座標を求める。集合外（空隙含む）なら false。
        /// Origin / CellSize で floor したあと membership。AABB 内でも空隙なら false。
        /// </summary>
        public static bool TryGetCoordinate(Vector3 worldPosition, out Vector2Int coordinate)
        {
            var local = worldPosition - Origin;
            var x = Mathf.FloorToInt(local.x / CellSize);
            var y = Mathf.FloorToInt(local.z / CellSize);
            coordinate = new Vector2Int(x, y);
            if (!CellMembership.Contains(coordinate))
            {
                coordinate = default;
                return false;
            }

            return true;
        }

        /// <summary>ワールド座標が載っているセル identity。グリッド外は null。</summary>
        public static string? TryGetCellIdentity(Vector3 worldPosition)
        {
            if (!TryGetCoordinate(worldPosition, out var coordinate))
            {
                return null;
            }

            return CellIdentity.Format(coordinate.x, coordinate.y);
        }

        /// <summary>デバッグテレポート用。グリッド四隅の上空。</summary>
        public static Vector3 CornerSpawn(int cornerIndex)
        {
            // 0: 南西(0,0) / 1: 南東(W-1,0) / 2: 北西(0,H-1) / 3: 北東(W-1,H-1)
            var rect = Rectangles[0];
            var x = cornerIndex is 1 or 3 ? rect.Origin.x + rect.Size.x - 1 : rect.Origin.x;
            var y = cornerIndex is 2 or 3 ? rect.Origin.y + rect.Size.y - 1 : rect.Origin.y;
            return GetCellCenter(x, y) + Vector3.up * SpawnHeight;
        }

        /// <summary>
        /// セル識別用の座標色。Editor 焼き込みと見た目の契約を共有する。
        /// </summary>
        public static Color GetCellTint(int x, int y)
            => Color.HSVToRGB(((x * 17) + (y * 31)) % 100 / 100f, 0.45f, 0.85f);

        /// <summary>
        /// セル固有の地形モチーフ index（0 .. MotifCount-1）。
        /// Marker 形状・ローカル小物の配置パターンに使う。幾何統合ではなく「中身の差」。
        /// </summary>
        public static int GetMotifIndex(int x, int y)
            => ((x * 3) + (y * 5)) % MotifCount;

        private static Vector2Int[] ExpandAndValidate(IReadOnlyList<CellRect> rectangles)
        {
            if (rectangles == null || rectangles.Count == 0)
            {
                throw new InvalidOperationException("矩形集合は 1 件以上である必要があります。");
            }

            for (var i = 0; i < rectangles.Count; i++)
            {
                for (var j = i + 1; j < rectangles.Count; j++)
                {
                    if (Overlaps(rectangles[i], rectangles[j]))
                    {
                        throw new InvalidOperationException("矩形同士の重なりは禁止です。");
                    }
                }
            }

            var cells = new List<Vector2Int>();
            for (var r = 0; r < rectangles.Count; r++)
            {
                var rect = rectangles[r];
                for (var y = 0; y < rect.Size.y; y++)
                {
                    for (var x = 0; x < rect.Size.x; x++)
                    {
                        cells.Add(new Vector2Int(rect.Origin.x + x, rect.Origin.y + y));
                    }
                }
            }

            return cells.ToArray();
        }

        private static bool Overlaps(CellRect a, CellRect b)
        {
            return a.Origin.x < b.Origin.x + b.Size.x
                && b.Origin.x < a.Origin.x + a.Size.x
                && a.Origin.y < b.Origin.y + b.Size.y
                && b.Origin.y < a.Origin.y + a.Size.y;
        }
    }
}
