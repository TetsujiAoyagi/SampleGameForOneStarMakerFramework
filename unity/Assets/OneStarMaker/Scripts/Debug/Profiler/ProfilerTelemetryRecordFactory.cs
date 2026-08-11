#nullable enable

using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime;

namespace OneStarMaker.Debug
{
    /// <summary>
    /// profiler テレメトリの <see cref="TelemetryRecord"/> を組み立てるだけの純粋型。
    ///
    /// <para>
    /// Contract v3: ProfilerSummary は kind=sample（elapsedMs は意味を持たない）、
    /// GcSpike / UiCost は kind=event。export 側は sample の elapsedMs キーを省略する。
    /// </para>
    /// <para>
    /// 送出（<c>AppTelemetry.WriteRecord</c>）はここでは行わない。呼ぶのは
    /// <see cref="ProfilerTelemetryEmitter"/> 側。
    /// 時刻と Unity frame を引数で受けるのは、テストで固定値に固定するため。
    /// </para>
    /// </summary>
    public static class ProfilerTelemetryRecordFactory
    {
        /// <summary>
        /// 1 秒サマリ（kind=sample）。
        /// tags は fps から <c>ClassifyFrameRate</c> で決まる（30fps 未満で FrameRateDrop）。
        /// </summary>
        public static TelemetryRecord CreateSummary(
            float fps,
            float cpuAvgMs,
            float gpuAvgMs,
            bool gpuAvailable,
            long utcTicks)
        {
            var (metadata, payload) = RuntimeTelemetryMetadataFactory.CreateFrameSampleTelemetry(
                fps: fps,
                cpuTime: cpuAvgMs,
                gpuTime: gpuAvgMs,
                gpuAvailable: gpuAvailable);

            return Create(
                startType: TelemetryStartType.ProfilerSummary,
                tags: RuntimeTelemetryMetadataFactory.ClassifyFrameRate(fps),
                level: TelemetryLevel.Verbose,
                metadata: metadata,
                payload: payload,
                utcTicks: utcTicks);
        }

        /// <summary>
        /// GC スパイク（kind=event）。根拠値だけを EventDetail に載せる（cpu/elapsed 欄を持たせない）。
        /// </summary>
        public static TelemetryRecord CreateGcSpike(int gcGen0Delta, int unityFrame, long utcTicks)
        {
            return Create(
                startType: TelemetryStartType.GcSpike,
                tags: TelemetryTagType.AllocSpike | TelemetryTagType.Bottleneck,
                level: TelemetryLevel.Summary,
                metadata: default,
                payload: TelemetryPayload.ForEventDetail(
                    gcGen0Delta: gcGen0Delta,
                    unityFrame: unityFrame),
                utcTicks: utcTicks);
        }

        /// <summary>
        /// UI コスト超過（kind=event）。瞬間 event なので根拠値は frameCount のみ。
        /// cpu/gpu を flat に載せると「区間計測」と誤読されるため載せない。
        /// </summary>
        public static TelemetryRecord CreateUiCost(int unityFrame, long utcTicks)
        {
            return Create(
                startType: TelemetryStartType.UiCost,
                tags: TelemetryTagType.Bottleneck,
                level: TelemetryLevel.Summary,
                metadata: default,
                payload: TelemetryPayload.ForEventDetail(
                    gcGen0Delta: 0,
                    unityFrame: unityFrame),
                utcTicks: utcTicks);
        }

        /// <summary>
        /// 3 種共通のエンベロープ。start と end を同じ ticks に揃え、elapsedMs は 0 で固定する。
        /// 親なしのセンチネルは -1 に統一する（0 と混在させない）。
        /// </summary>
        private static TelemetryRecord Create(
            TelemetryStartType startType,
            TelemetryTagType? tags,
            TelemetryLevel level,
            in Metadata metadata,
            in TelemetryPayload payload,
            long utcTicks)
        {
            return new TelemetryRecord(
                traceId: AppTelemetry.GenerateId(),
                spanId: AppTelemetry.GenerateId(),
                parentSpanId: -1,
                name: startType,
                startTimestampUtcTicks: utcTicks,
                endTimestampUtcTicks: utcTicks,
                elapsedMs: 0,
                isSuccess: true,
                tags: tags,
                level: level,
                metadata: metadata,
                kind: TelemetryKindRules.InferKind(startType),
                payload: payload);
        }
    }
}
