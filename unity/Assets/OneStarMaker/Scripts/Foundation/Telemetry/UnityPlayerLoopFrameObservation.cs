#nullable enable

using System;

namespace OneStarMaker.Foundation.Telemetry
{
    /// <summary>
    /// Unity player loop 上の <c>Time.frameCount</c> を Foundation 層から観測する indirection。
    ///
    /// <para>
    /// worker thread / job callback では frame を <c>null</c> にする。
    /// main thread 以外で <c>Time.frameCount</c> を読むと「その thread が main frame 上で実行された」
    /// という誤解を招くため、Runtime bootstrap が main thread 判定付き delegate を register する。
    /// </para>
    ///
    /// <para>
    /// Log の <c>unityFrameAtEmit</c> は formatter が envelope を組み立てた時点の frame である。
    /// ユーザーコード内の事象そのものの発生 frame と必ずしも一致しない（queue 遅延等）点に注意。
    /// </para>
    /// </summary>
    public static class UnityPlayerLoopFrameObservation
    {
        private static Func<int?>? s_tryGetCurrentFrame;

        /// <summary>
        /// Runtime bootstrap が UnityEngine 実装を bind する。未 register 時は常に null（未観測）。
        /// </summary>
        public static void Register(Func<int?> tryGetCurrentFrame)
        {
            s_tryGetCurrentFrame = tryGetCurrentFrame ?? throw new ArgumentNullException(nameof(tryGetCurrentFrame));
        }

        /// <summary>
        /// 現在の player-loop frame。main thread 以外、または未 register 時は null。
        /// Elastic range query との整合のため sentinel 数値は使わない。
        /// </summary>
        public static int? TryGetCurrentFrame()
            => s_tryGetCurrentFrame?.Invoke();

        /// <summary>テスト用 reset。</summary>
        internal static void ResetForTests()
        {
            s_tryGetCurrentFrame = null;
        }
    }
}
