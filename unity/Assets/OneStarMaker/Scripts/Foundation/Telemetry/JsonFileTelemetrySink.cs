#nullable enable

using System;
using Microsoft.Extensions.Logging;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Logging;
using ZLogger;

namespace OneStarMaker.Foundation.Telemetry
{
    /// <summary>
    /// telemetry record を ZLogger へ流す sink。
    ///
    /// <para>
    /// 名前は既存との互換のため残しているが、責務は「手書き JSONL 出力」ではなく
    /// 「telemetry を 1 種類の ZLogger entry として流すこと」になっている。
    /// </para>
    ///
    /// <para>
    /// これにより、
    /// - rolling file 側は人間向け summary / JSON 表示
    /// - realtime stream 側は MessagePack formatter
    /// を同じ entry から分岐できる。
    /// </para>
    ///
    /// <para>
    /// Contract v3: kind / schemaVersion / payload 主要フィールドも structured property として出す。
    /// Filebeat が Unity ログを直接読む経路でも v3 契約が消えないようにする。
    /// </para>
    /// </summary>
    public sealed class JsonFileTelemetrySink : ITelemetrySink
    {
        private readonly ILogger _logger;
        private bool _disposed;

        public JsonFileTelemetrySink(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger("Telemetry");
        }

        /// <inheritdoc />
        public void Write(in TelemetryRecord record)
        {
            if (_disposed) return;

            // L0 rolling JSON でも相関値を JSON property として検索できるよう、
            // 文字列連結ではなく MEL の message-template structured logging を使う。
            // AppLoggerFactory の JSON formatter は IncludeProperties.All のため、
            // 各 placeholder は本文だけでなく独立した JSON field として永続化される。
            // telemetry の realtime transport は専用 sink が担うので、この entry 自体は
            // MessagePack formatter 側で抑止し、二重送信しない。
            var name = record.Name.ToStartTypeString();
            var kind = record.Kind.ToWireString();
            var schemaVersion = 3;
            var payloadShape = (int)record.Payload.Shape;
            var payloadTargetIdentity = record.Payload.TargetIdentity ?? string.Empty;
            var payloadStage = record.Payload.Stage ?? string.Empty;
            var payloadManagedDeltaBytes = record.Payload.HasTimingMemory ? record.Payload.ManagedDeltaBytes : 0L;
            var payloadNativeDeltaBytes = record.Payload.HasTimingMemory ? record.Payload.NativeDeltaBytes : 0L;
            var payloadFps = record.Payload.HasFrame ? record.Payload.Fps : 0f;
            var payloadCpuMs = record.Payload.HasFrame ? record.Payload.CpuMs : 0f;
            var payloadGpuAvailable = record.Payload.HasFrame && record.Payload.GpuAvailable;
            var payloadGcGen0Delta = record.Payload.HasEventDetail ? record.Payload.GcGen0Delta : 0;
            var traceId = record.TraceId;
            var spanId = record.SpanId;
            var parentSpanId = record.ParentSpanId;
            var startTimestampUtcTicks = record.StartTimestampUtcTicks;
            var endTimestampUtcTicks = record.EndTimestampUtcTicks;
            // sample の elapsedMs=0 は意味を持たないが、ZLogger template は固定プレースホルダのため
            // 値としては残し、消費者は kind で解釈する（NDJSON export 側ではキー省略）。
            var elapsedMs = record.ElapsedMs;
            var isSuccess = record.IsSuccess;
            var telemetryLevel = (int)record.Level;
            var tagBits = record.Tags.HasValue ? (int)record.Tags.Value : -1;
            var tagNames = record.Tags.HasValue ? record.Tags.Value.ToTagString() : string.Empty;
            var cpuTime = record.MetadataValue.CpuTime;
            var gpuTime = record.MetadataValue.GpuTime;
            var managedMem = record.MetadataValue.ManagedMem;
            var nativeMem = record.MetadataValue.NativeMem;
            var sceneFrom = record.MetadataValue.SceneFrom;
            var sceneTo = record.MetadataValue.SceneTo;
            var cameraTotalViewCount = record.MetadataValue.CameraTotalViewCount;
            var cameraAdditionalViewCount = record.MetadataValue.CameraAdditionalViewCount;
            var cameraBlendingViewCount = record.MetadataValue.CameraBlendingViewCount;
            var cameraMaxStackDepthTotal = record.MetadataValue.CameraMaxStackDepthTotal;
            var cameraViewId = record.MetadataValue.CameraViewId;
            var cameraActiveCameraHash = record.MetadataValue.CameraActiveCameraHash;
            var sessionId = record.SessionId;
            var producerSequence = record.ProducerSequence;
            var unityFrameAtStart = record.UnityFrameAtStart;
            var unityFrameAtEnd = record.UnityFrameAtEnd;

            var logLevel = MapLogLevel(record.Level);
            _logger.Log(
                logLevel,
                TelemetryZLoggerConstants.EventId,
                "[Telemetry] name={Name} kind={Kind} schemaVersion={SchemaVersion} success={IsSuccess} elapsedMs={ElapsedMs} traceId={TraceId} spanId={SpanId} parentSpanId={ParentSpanId} startTimestampUtcTicks={StartTimestampUtcTicks} endTimestampUtcTicks={EndTimestampUtcTicks} telemetryLevel={TelemetryLevel} tagBits={TagBits} tagNames={TagNames} payloadShape={PayloadShape} payloadTargetIdentity={PayloadTargetIdentity} payloadStage={PayloadStage} payloadManagedDeltaBytes={PayloadManagedDeltaBytes} payloadNativeDeltaBytes={PayloadNativeDeltaBytes} payloadFps={PayloadFps} payloadCpuMs={PayloadCpuMs} payloadGpuAvailable={PayloadGpuAvailable} payloadGcGen0Delta={PayloadGcGen0Delta} cpuTime={CpuTime} gpuTime={GpuTime} managedMem={ManagedMem} nativeMem={NativeMem} sceneFrom={SceneFrom} sceneTo={SceneTo} cameraTotalViewCount={CameraTotalViewCount} cameraAdditionalViewCount={CameraAdditionalViewCount} cameraBlendingViewCount={CameraBlendingViewCount} cameraMaxStackDepthTotal={CameraMaxStackDepthTotal} cameraViewId={CameraViewId} cameraActiveCameraHash={CameraActiveCameraHash} sessionId={SessionId} producerSequence={ProducerSequence} unityFrameAtStart={UnityFrameAtStart} unityFrameAtEnd={UnityFrameAtEnd}",
                name,
                kind,
                schemaVersion,
                isSuccess,
                elapsedMs,
                traceId,
                spanId,
                parentSpanId,
                startTimestampUtcTicks,
                endTimestampUtcTicks,
                telemetryLevel,
                tagBits,
                tagNames,
                payloadShape,
                payloadTargetIdentity,
                payloadStage,
                payloadManagedDeltaBytes,
                payloadNativeDeltaBytes,
                payloadFps,
                payloadCpuMs,
                payloadGpuAvailable,
                payloadGcGen0Delta,
                cpuTime,
                gpuTime,
                managedMem,
                nativeMem,
                sceneFrom,
                sceneTo,
                cameraTotalViewCount,
                cameraAdditionalViewCount,
                cameraBlendingViewCount,
                cameraMaxStackDepthTotal,
                cameraViewId,
                cameraActiveCameraHash,
                sessionId,
                producerSequence,
                unityFrameAtStart,
                unityFrameAtEnd);
        }

        /// <inheritdoc />
        public void Flush()
        {
            // flush は ZLogger provider 側の責務。
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _disposed = true;
        }

        private static LogLevel MapLogLevel(TelemetryLevel telemetryLevel)
        {
            return telemetryLevel switch
            {
                TelemetryLevel.Summary => LogLevel.Information,
                TelemetryLevel.Verbose => LogLevel.Debug,
                _ => LogLevel.Trace,
            };
        }
    }
}
