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

            // formatter 側が復元しやすいよう、キー名を固定した structured log として流す。
            // 人間向けには十分読め、MessagePack 正本側は parameters から lossless に復元する。
            var name = record.Name.ToStartTypeString();
            var traceId = record.TraceId;
            var spanId = record.SpanId;
            var parentSpanId = record.ParentSpanId;
            var startTimestampUtcTicks = record.StartTimestampUtcTicks;
            var endTimestampUtcTicks = record.EndTimestampUtcTicks;
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

            var logLevel = MapLogLevel(record.Level);
            _logger.ZLog(
                logLevel: logLevel,
                eventId: TelemetryZLoggerConstants.EventId,
                message: $"[Telemetry] name={name} success={isSuccess} elapsedMs={elapsedMs:F1} traceId={traceId} spanId={spanId} parentSpanId={parentSpanId} startTimestampUtcTicks={startTimestampUtcTicks} endTimestampUtcTicks={endTimestampUtcTicks} telemetryLevel={telemetryLevel} tagBits={tagBits} tagNames={tagNames} cpuTime={cpuTime} gpuTime={gpuTime} managedMem={managedMem} nativeMem={nativeMem} sceneFrom={sceneFrom} sceneTo={sceneTo}",
                context: null);
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
