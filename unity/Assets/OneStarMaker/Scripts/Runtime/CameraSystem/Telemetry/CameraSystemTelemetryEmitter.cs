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
    /// Contract v3 では kind=sample（状態ゲージ）。elapsed は意味を持たない。
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

            // Contract v3: カウンタの正本は CameraCounters payload。flat metadata は段階移行の併記。
            var metadata = new Metadata(
                cameraTotalViewCount: snapshot.TotalViewCount,
                cameraAdditionalViewCount: snapshot.AdditionalViewCount,
                cameraBlendingViewCount: snapshot.BlendingViewCount,
                cameraMaxStackDepthTotal: maxStackDepth);
            var payload = TelemetryPayload.ForCameraCounters(
                totalViewCount: snapshot.TotalViewCount,
                additionalViewCount: snapshot.AdditionalViewCount,
                blendingViewCount: snapshot.BlendingViewCount,
                maxStackDepthTotal: maxStackDepth);

            var now = DateTime.UtcNow.Ticks;
            var record = new TelemetryRecord(
                traceId: AppTelemetry.GenerateId(),
                spanId: AppTelemetry.GenerateId(),
                parentSpanId: -1,
                name: TelemetryStartType.CameraSystemSnapshot,
                startTimestampUtcTicks: now,
                endTimestampUtcTicks: now,
                elapsedMs: 0.0,
                isSuccess: true,
                tags: null,
                level: TelemetryLevel.Verbose,
                metadata: metadata,
                kind: TelemetryKind.Sample,
                payload: payload);

            AppTelemetry.WriteRecord(record);
        }
    }
}
