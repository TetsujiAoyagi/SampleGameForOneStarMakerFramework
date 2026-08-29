#nullable enable

using System;

namespace OneStarMaker.Runtime.Streaming
{
    /// <summary>
    /// 距離政策のチューニング値（21-scene-streaming.md §8 / 34-ondemand-spatial-policy.md §4）。
    ///
    /// <para>
    /// <b>寿命が長いほう。</b> 候補集合（<see cref="StreamingCandidateSet"/>）が差し替わっても
    /// これは変わらない。だから別の型にしてある。
    /// </para>
    /// </summary>
    public sealed class StreamingPolicySettings
    {
        /// <param name="loadRadius">注視点からの XZ 平面距離がこの値以下の候補を desired set に含める。</param>
        /// <param name="unloadRadius">注視点からの XZ 平面距離がこの値以下の候補を retain set に含める（ヒステリシス）。</param>
        /// <param name="maxInFlight">未完了 RequestAdd の同時上限。</param>
        public StreamingPolicySettings(float loadRadius, float unloadRadius, int maxInFlight)
        {
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

            LoadRadius = loadRadius;
            UnloadRadius = unloadRadius;
            MaxInFlight = maxInFlight;
        }

        /// <summary>ロード半径（XZ 平面距離）。</summary>
        public float LoadRadius { get; }

        /// <summary>アンロード半径（XZ 平面距離）。ヒステリシス幅 = UnloadRadius - LoadRadius。</summary>
        public float UnloadRadius { get; }

        /// <summary>未完了 RequestAdd の同時上限。</summary>
        public int MaxInFlight { get; }
    }
}
