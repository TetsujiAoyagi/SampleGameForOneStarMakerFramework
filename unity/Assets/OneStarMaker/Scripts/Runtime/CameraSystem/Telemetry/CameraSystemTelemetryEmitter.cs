#nullable enable

using System;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Telemetry
{
    /// <summary>
    /// CameraSystem のスナップショットを 1 件のテレメトリレコードへ変換して送出する。
    /// 状態観測用の点イベントなので開始/終了タイムスタンプは同一、elapsed は 0 とする。
    /// </summary>
    public static class CameraSystemTelemetryEmitter
    {
        /// <summary>
        /// スナップショットを収集し、View 数や最大スタック深度を metadata に詰めて Verbose レコードとして書き出す。
        /// テレメトリ無効時は何もしない。
        /// </summary>
        public static void EmitSnapshot(OneStarMaker.Runtime.CameraSystem.Core.CameraSystem system)
        {
            if (!AppTelemetry.IsEnabled)
            {
                return;
            }

            var collector = new CameraSystemTelemetryCollector();
            var snapshot = collector.Capture(system);
            var maxStackDepth = 0;
            for (var i = 0; i < snapshot.ViewSummaries.Length; i++)
            {
                maxStackDepth = Math.Max(maxStackDepth, snapshot.ViewSummaries[i].StackDepthTotal);
            }

            var metadata = new Metadata(
                cameraTotalViewCount: snapshot.TotalViewCount,
                cameraAdditionalViewCount: snapshot.AdditionalViewCount,
                cameraBlendingViewCount: snapshot.BlendingViewCount,
                cameraMaxStackDepthTotal: maxStackDepth);

            var record = new TelemetryRecord(
                traceId: AppTelemetry.GenerateId(),
                spanId: AppTelemetry.GenerateId(),
                parentSpanId: -1,
                name: TelemetryStartType.CameraSystemSnapshot,
                startTimestampUtcTicks: DateTime.UtcNow.Ticks,
                endTimestampUtcTicks: DateTime.UtcNow.Ticks,
                elapsedMs: 0.0,
                isSuccess: true,
                tags: null,
                level: TelemetryLevel.Verbose,
                metadata: metadata);

            AppTelemetry.WriteRecord(record);
        }
    }
}
