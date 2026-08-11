#nullable enable

using System;
using OneStarMaker.Foundation.Telemetry;

namespace OneStarMaker.Debug
{
    /// <summary>
    /// 1 フレームで送出すべき profiler テレメトリの種別。
    /// 同一フレームで複数立ち得るため <see cref="FlagsAttribute"/> を付ける。
    /// </summary>
    [Flags]
    public enum ProfilerTelemetryEmission
    {
        /// <summary>送出なし。</summary>
        None = 0,

        /// <summary>1 秒サマリ（kind=sample）。</summary>
        Summary = 1,

        /// <summary>GC スパイク（kind=event）。</summary>
        GcSpike = 2,

        /// <summary>UI 描画コスト超過（kind=event）。</summary>
        UiCost = 4,
    }

    /// <summary>
    /// 判定に必要な 1 フレーム分の観測値。
    ///
    /// <para>
    /// UnityEngine の型を持たないのは意図的。
    /// Time.frameCount / GC.CollectionCount / ProfilerRecorder は Emitter 側で読み、
    /// ここへは「値」として渡す。そうしないと閾値判定に単体テストが書けない。
    /// </para>
    /// </summary>
    public readonly struct ProfilerFrameInput
    {
        /// <summary>サンプラが 1 秒サマリを更新したか。</summary>
        public readonly bool SummaryUpdated;

        /// <summary>直前フレームからの Gen0 GC 回数の差分。</summary>
        public readonly int GcGen0Delta;

        /// <summary>UI コストが計測可能か（ProfilerRecorder が有効か）。</summary>
        public readonly bool UiCostAvailable;

        /// <summary>Canvas Rebuild 回数。</summary>
        public readonly long CanvasRebuildCount;

        /// <summary>描画バッチ数。</summary>
        public readonly long BatchCount;

        public ProfilerFrameInput(
            bool summaryUpdated,
            int gcGen0Delta,
            bool uiCostAvailable,
            long canvasRebuildCount,
            long batchCount)
        {
            SummaryUpdated = summaryUpdated;
            GcGen0Delta = gcGen0Delta;
            UiCostAvailable = uiCostAvailable;
            CanvasRebuildCount = canvasRebuildCount;
            BatchCount = batchCount;
        }
    }

    /// <summary>
    /// 閾値と 1 フレームの観測値から「どのレコードを出すか」だけを決める純粋型。
    /// レコードの組み立ては <see cref="ProfilerTelemetryRecordFactory"/>、
    /// 送出は <see cref="ProfilerTelemetryEmitter"/> の責務。
    /// </summary>
    public static class ProfilerTelemetryPolicy
    {
        /// <summary>
        /// 送出種別を決める。
        ///
        /// <para>
        /// 閾値比較はすべて厳密な <c>&gt;</c> であり <c>&gt;=</c> ではない。
        /// ここを <c>&gt;=</c> にすると gcPerFrame の既定値 1 で毎フレーム GcSpike が出る。
        /// この境界は <c>ProfilerTelemetryPolicyTests</c> の
        /// 「GC差分が閾値ちょうどならGcSpikeは立たない」で固定してあり、
        /// <c>&gt;=</c> に書き換えると当該テストが赤くなる。
        /// </para>
        /// </summary>
        /// <param name="input">1 フレーム分の観測値。</param>
        /// <param name="thresholds">閾値。null なら判定しない（= 何も出さない）。</param>
        /// <param name="telemetryEnabled">テレメトリ自体が有効か。</param>
        public static ProfilerTelemetryEmission Decide(
            in ProfilerFrameInput input,
            TelemetryThresholds? thresholds,
            bool telemetryEnabled)
        {
            if (!telemetryEnabled)
            {
                return ProfilerTelemetryEmission.None;
            }

            if (thresholds == null)
            {
                return ProfilerTelemetryEmission.None;
            }

            var emission = ProfilerTelemetryEmission.None;

            if (input.SummaryUpdated)
            {
                emission |= ProfilerTelemetryEmission.Summary;
            }

            if (input.GcGen0Delta > 0 && input.GcGen0Delta > thresholds.GcPerFrame)
            {
                emission |= ProfilerTelemetryEmission.GcSpike;
            }

            if (input.UiCostAvailable &&
                (input.CanvasRebuildCount > thresholds.CanvasRebuildPerFrame ||
                 input.BatchCount > thresholds.BatchCount))
            {
                emission |= ProfilerTelemetryEmission.UiCost;
            }

            return emission;
        }
    }
}
