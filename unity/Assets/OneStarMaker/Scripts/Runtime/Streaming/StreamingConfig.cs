#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace OneStarMaker.Runtime.Streaming
{
    /// <summary>
    /// WorldStreamingController のポリシーパラメータ（21-scene-streaming.md §8）。
    /// セル座標集合・ロード/アンロード半径・同時 in-flight 上限を保持する。
    /// 矩形レイアウトは知らない。列挙は呼び出し側の責務。
    /// </summary>
    public sealed class StreamingConfig
    {
        /// <param name="grid">セル原点・サイズ（CellGridConfig）。</param>
        /// <param name="cells">走査対象のセル座標（1 件以上）。</param>
        /// <param name="loadRadius">注視点からの XZ 平面距離がこの値以下のセルを desired set に含める。</param>
        /// <param name="unloadRadius">注視点からの XZ 平面距離がこの値以下のセルを retain set に含める（ヒステリシス）。</param>
        /// <param name="maxInFlight">未完了 RequestAdd の同時上限。</param>
        public StreamingConfig(
            CellGridConfig grid,
            IReadOnlyList<Vector2Int> cells,
            float loadRadius,
            float unloadRadius,
            int maxInFlight)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (cells.Count == 0)
            {
                throw new ArgumentException("セル座標集合は 1 件以上である必要があります。", nameof(cells));
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

            var copy = new Vector2Int[cells.Count];
            for (var i = 0; i < cells.Count; i++)
            {
                copy[i] = cells[i];
            }

            Grid = grid;
            Cells = copy;
            LoadRadius = loadRadius;
            UnloadRadius = unloadRadius;
            MaxInFlight = maxInFlight;
        }

        /// <summary>セル原点・サイズ。</summary>
        public CellGridConfig Grid { get; }

        /// <summary>走査対象のセル座標。</summary>
        public IReadOnlyList<Vector2Int> Cells { get; }

        /// <summary>ロード半径（XZ 平面距離）。</summary>
        public float LoadRadius { get; }

        /// <summary>アンロード半径（XZ 平面距離）。ヒステリシス幅 = UnloadRadius - LoadRadius。</summary>
        public float UnloadRadius { get; }

        /// <summary>未完了 RequestAdd の同時上限。</summary>
        public int MaxInFlight { get; }
    }
}
