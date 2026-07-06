#nullable enable

using System;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace OneStarMaker.Runtime.Streaming
{
    /// <summary>
    /// WorldStreamingController のポリシーパラメータ（21-scene-streaming.md §8）。
    /// グリッド定義・ロード/アンロード半径・同時 in-flight 上限を保持する。
    /// </summary>
    public sealed class StreamingConfig
    {
        /// <param name="grid">セル原点・サイズ（CellGridConfig）。</param>
        /// <param name="gridWidth">グリッド幅（X 方向セル数）。</param>
        /// <param name="gridHeight">グリッド高さ（Z 方向セル数）。</param>
        /// <param name="loadRadius">注視点からの XZ 平面距離がこの値以下のセルを desired set に含める。</param>
        /// <param name="unloadRadius">注視点からの XZ 平面距離がこの値以下のセルを retain set に含める（ヒステリシス）。</param>
        /// <param name="maxInFlight">未完了 RequestAdd の同時上限。</param>
        public StreamingConfig(
            CellGridConfig grid,
            int gridWidth,
            int gridHeight,
            float loadRadius,
            float unloadRadius,
            int maxInFlight)
        {
            if (gridWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gridWidth), gridWidth, "グリッド幅は 1 以上である必要があります。");
            }

            if (gridHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gridHeight), gridHeight, "グリッド高さは 1 以上である必要があります。");
            }

            if (loadRadius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(loadRadius), loadRadius, "ロード半径は正の値である必要があります。");
            }

            if (unloadRadius <= loadRadius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unloadRadius),
                    unloadRadius,
                    "アンロード半径はロード半径より大きい必要があります（ヒステリシス）。");
            }

            if (maxInFlight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInFlight), maxInFlight, "maxInFlight は 1 以上である必要があります。");
            }

            Grid = grid;
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            LoadRadius = loadRadius;
            UnloadRadius = unloadRadius;
            MaxInFlight = maxInFlight;
        }

        /// <summary>セル原点・サイズ。</summary>
        public CellGridConfig Grid { get; }

        /// <summary>グリッド幅（X 方向セル数）。</summary>
        public int GridWidth { get; }

        /// <summary>グリッド高さ（Z 方向セル数）。</summary>
        public int GridHeight { get; }

        /// <summary>ロード半径（XZ 平面距離）。</summary>
        public float LoadRadius { get; }

        /// <summary>アンロード半径（XZ 平面距離）。ヒステリシス幅 = UnloadRadius - LoadRadius。</summary>
        public float UnloadRadius { get; }

        /// <summary>未完了 RequestAdd の同時上限。</summary>
        public int MaxInFlight { get; }
    }
}
