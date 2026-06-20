#nullable enable

using System;
using System.Buffers;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Telemetry;
using ZLogger;

namespace OneStarMaker.Foundation.Logging
{
    /// <summary>
    /// ZLogger の realtime stream 出力を MessagePack + length-prefix 形式に変換する formatter。
    ///
    /// <para>
    /// この formatter は「見た目を整える」のではなく、
    /// sender/receiver 間でやり取りするバイナリフレームを組み立てるのが仕事。
    /// </para>
    ///
    /// <para>
    /// 出力形式は以下:
    /// </para>
    /// <code>
    /// [4byte little-endian length][MessagePack(DebugSocketEnvelopeV1)]
    /// </code>
    ///
    /// <para>
    /// つまり realtime log も DebugSocket の共通 envelope に包み、
    /// command / telemetry / service status と同じ protocol の上に載せる。
    /// receiver 側は envelope の message type を見てから
    /// <see cref="LogEnvelopeV1"/> を復号する。
    /// </para>
    /// </summary>
    internal sealed class MessagePackZLoggerFormatter : IZLoggerFormatter
    {
        private readonly string _applicationName;

        public MessagePackZLoggerFormatter(string applicationName)
        {
            _applicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
        }

        public bool WithLineBreak => false;

        public void FormatLogEntry(IBufferWriter<byte> writer, IZLoggerEntry entry)
        {
            // realtime transport では LogInfo にアクセスできる entry が必要。
            // 取れない場合は sender 実装前提が崩れているので例外で明示する。
            if (entry is not INonReturnableZLoggerEntry nonReturnableEntry)
            {
                throw new InvalidOperationException(
                    $"Realtime MessagePack logging requires {nameof(INonReturnableZLoggerEntry)} support.");
            }

            var logInfo = nonReturnableEntry.LogInfo;

            // telemetry は通常ログとは別の source-of-truth payload として扱う。
            // 同じ ZLogger entry でも EventId で判別し、message type を切り替える。
            if (TryCreateTelemetryEnvelope(logInfo, out var telemetryEnvelope))
            {
                DebugSocketProtocol.SerializeMessage(writer, DebugSocketMessageType.Telemetry, telemetryEnvelope);
                return;
            }

            DebugSocketProtocol.SerializeMessage(
                writer,
                DebugSocketMessageType.Log,
                CreateEnvelope(logInfo));
        }

        private LogEnvelopeV1 CreateEnvelope(in LogInfo logInfo)
        {
            // ここでは sender の内部型を receiver が知らなくてもよいように、
            // 必要な値だけを素直な DTO にコピーしている。
            return new LogEnvelopeV1
            {
                ApplicationName = _applicationName,
                TimestampUnixTimeMilliseconds = logInfo.Timestamp.Utc.ToUnixTimeMilliseconds(),
                Category = logInfo.Category.Name ?? string.Empty,
                LogLevel = (int)logInfo.LogLevel,
                EventId = logInfo.EventId.Id,
                EventName = logInfo.EventId.Name,
                Message = GetMessage(logInfo.Context, logInfo.Exception),
                Exception = logInfo.Exception?.ToString(),
                ThreadId = logInfo.ThreadInfo.ThreadId,
                ThreadName = logInfo.ThreadInfo.ThreadName,
                MemberName = logInfo.MemberName,
                FilePath = logInfo.FilePath,
                LineNumber = logInfo.LineNumber,
            };
        }

        private static string GetMessage(object? context, Exception? exception)
        {
            // ZLogger の structured log は IZLoggerFormattable を実装していることがある。
            // その場合は formatter 済み文字列を優先して receiver に渡す。
            if (context is IZLoggerFormattable formattable)
            {
                return formattable.ToString();
            }

            // structured でない通常ログは ToString() の結果を使う。
            if (context != null)
            {
                return context.ToString() ?? string.Empty;
            }

            // context がなく例外だけあるケースでは、最低限 Message 欄を空にしない。
            return exception?.Message ?? string.Empty;
        }

        private static bool TryCreateTelemetryEnvelope(in LogInfo logInfo, out DebugTelemetryEnvelopeV1 envelope)
        {
            envelope = new DebugTelemetryEnvelopeV1();

            if (logInfo.EventId.Id != TelemetryZLoggerConstants.EventId.Id ||
                !string.Equals(logInfo.EventId.Name, TelemetryZLoggerConstants.EventId.Name, StringComparison.Ordinal) ||
                logInfo.Context is not IZLoggerFormattable formattable)
            {
                return false;
            }

            var result = new DebugTelemetryEnvelopeV1();
            var hasAnyField = false;

            for (var i = 0; i < formattable.ParameterCount; i++)
            {
                var key = formattable.GetParameterKeyAsString(i);
                var value = formattable.GetParameterValue(i);

                switch (key)
                {
                    case "name":
                        if (value is TelemetryStartType startType)
                        {
                            result.Name = startType.ToStartTypeString();
                            hasAnyField = true;
                        }
                        else if (TryConvertInt32(value, out var nameCode))
                        {
                            result.Name = ((TelemetryStartType)nameCode).ToStartTypeString();
                            hasAnyField = true;
                        }
                        else if (value is string name)
                        {
                            result.Name = name;
                            hasAnyField = true;
                        }
                        break;
                    case "traceId":
                        if (TryConvertInt64(value, out var traceId))
                        {
                            result.TraceId = traceId;
                            hasAnyField = true;
                        }
                        break;
                    case "spanId":
                        if (TryConvertInt64(value, out var spanId))
                        {
                            result.SpanId = spanId;
                            hasAnyField = true;
                        }
                        break;
                    case "parentSpanId":
                        if (TryConvertInt64(value, out var parentSpanId))
                        {
                            result.ParentSpanId = parentSpanId;
                            hasAnyField = true;
                        }
                        break;
                    case "startTimestampUtcTicks":
                        if (TryConvertInt64(value, out var startTimestampUtcTicks))
                        {
                            result.StartTimestampUtcTicks = startTimestampUtcTicks;
                            hasAnyField = true;
                        }
                        break;
                    case "endTimestampUtcTicks":
                        if (TryConvertInt64(value, out var endTimestampUtcTicks))
                        {
                            result.EndTimestampUtcTicks = endTimestampUtcTicks;
                            hasAnyField = true;
                        }
                        break;
                    case "elapsedMs":
                        if (TryConvertDouble(value, out var elapsedMs))
                        {
                            result.ElapsedMs = elapsedMs;
                            hasAnyField = true;
                        }
                        break;
                    case "isSuccess":
                        if (value is bool isSuccess)
                        {
                            result.IsSuccess = isSuccess;
                            hasAnyField = true;
                        }
                        break;
                    case "telemetryLevel":
                        if (TryConvertInt32(value, out var telemetryLevel))
                        {
                            result.Level = telemetryLevel;
                            hasAnyField = true;
                        }
                        break;
                    case "tagBits":
                        if (TryConvertInt32(value, out var tagBits) && tagBits >= 0)
                        {
                            result.TagBits = tagBits;
                            hasAnyField = true;
                        }
                        else if (TryConvertInt32(value, out _) && tagBits < 0)
                        {
                            result.TagBits = null;
                            hasAnyField = true;
                        }
                        break;
                    case "cpuTime":
                        if (TryConvertSingle(value, out var cpuTime))
                        {
                            result.CpuTime = cpuTime;
                            hasAnyField = true;
                        }
                        break;
                    case "gpuTime":
                        if (TryConvertSingle(value, out var gpuTime))
                        {
                            result.GpuTime = gpuTime;
                            hasAnyField = true;
                        }
                        break;
                    case "managedMem":
                        if (TryConvertInt64(value, out var managedMem))
                        {
                            result.ManagedMem = managedMem;
                            hasAnyField = true;
                        }
                        break;
                    case "nativeMem":
                        if (TryConvertInt64(value, out var nativeMem))
                        {
                            result.NativeMem = nativeMem;
                            hasAnyField = true;
                        }
                        break;
                    case "sceneFrom":
                        if (TryConvertInt32(value, out var sceneFrom))
                        {
                            result.SceneFrom = sceneFrom;
                            hasAnyField = true;
                        }
                        break;
                    case "sceneTo":
                        if (TryConvertInt32(value, out var sceneTo))
                        {
                            result.SceneTo = sceneTo;
                            hasAnyField = true;
                        }
                        break;
                }
            }

            envelope = result;
            return hasAnyField;
        }

        private static bool TryConvertInt32(object? value, out int converted)
        {
            switch (value)
            {
                case int v:
                    converted = v;
                    return true;
                case long v:
                    converted = (int)v;
                    return true;
                case short v:
                    converted = v;
                    return true;
                case byte v:
                    converted = v;
                    return true;
                case TelemetryLevel v:
                    converted = (int)v;
                    return true;
                case TelemetryTagType v:
                    converted = (int)v;
                    return true;
                case TelemetryStartType v:
                    converted = (int)v;
                    return true;
                default:
                    converted = default;
                    return false;
            }
        }

        private static bool TryConvertInt64(object? value, out long converted)
        {
            switch (value)
            {
                case long v:
                    converted = v;
                    return true;
                case int v:
                    converted = v;
                    return true;
                case short v:
                    converted = v;
                    return true;
                case byte v:
                    converted = v;
                    return true;
                default:
                    converted = default;
                    return false;
            }
        }

        private static bool TryConvertDouble(object? value, out double converted)
        {
            switch (value)
            {
                case double v:
                    converted = v;
                    return true;
                case float v:
                    converted = v;
                    return true;
                default:
                    converted = default;
                    return false;
            }
        }

        private static bool TryConvertSingle(object? value, out float converted)
        {
            switch (value)
            {
                case float v:
                    converted = v;
                    return true;
                case double v:
                    converted = (float)v;
                    return true;
                default:
                    converted = default;
                    return false;
            }
        }
    }
}
