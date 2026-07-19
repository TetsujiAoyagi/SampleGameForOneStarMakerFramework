#nullable enable

using System;
using System.Buffers;
using OneStarMaker.Foundation.DebugSocket;
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
    /// つまり realtime log を DebugSocket の共通 envelope に包み、
    /// command / service status と同じ protocol の上に載せる。
    /// receiver 側は envelope の message type を見てから
    /// <see cref="LogEnvelopeV1"/> を復号する。
    /// </para>
    ///
    /// <para>
    /// telemetry は <c>DebugSocketTelemetrySink</c> 専用経路でのみ DebugStudio へ送る。
    /// 同じ ZLogger entry が rolling file と realtime stream の両方を通るため、
    /// ここでは telemetry EventId の entry を意図的に捨て、二重送信を防ぐ。
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

            // telemetry は DebugSocketTelemetrySink 専用。realtime stream では捨てる。
            if (logInfo.EventId.Id == TelemetryZLoggerConstants.EventId.Id &&
                string.Equals(logInfo.EventId.Name, TelemetryZLoggerConstants.EventId.Name, StringComparison.Ordinal))
            {
                return;
            }

            DebugSocketProtocol.SerializeMessage(
                writer,
                DebugSocketMessageType.Log,
                CreateEnvelope(logInfo, entry));
        }

        private LogEnvelopeV1 CreateEnvelope(in LogInfo logInfo, IZLoggerEntry entry)
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
                Message = GetMessage(entry, logInfo.Context, logInfo.Exception),
                Exception = logInfo.Exception?.ToString(),
                ThreadId = logInfo.ThreadInfo.ThreadId,
                ThreadName = logInfo.ThreadInfo.ThreadName,
                MemberName = logInfo.MemberName,
                FilePath = logInfo.FilePath,
                LineNumber = logInfo.LineNumber,
                SessionId = UnitySessionCorrelationContext.SessionId,
                ProducerSequence = UnitySessionCorrelationContext.NextProducerSequence(),
                // emit frame は「formatter が wire envelope を組み立てた時点」の frame。
                // ZLogger queue 遅延によりログ呼び出し元 frame と一致しない場合がある。
                UnityFrameAtEmit = UnityPlayerLoopFrameObservation.TryGetCurrentFrame(),
                TraceId = AppTelemetry.CurrentTraceId,
                SpanId = AppTelemetry.CurrentSpanId,
            };
        }

        private static string GetMessage(IZLoggerEntry entry, object? context, Exception? exception)
        {
            // ZLogger entry 本体が formatter 済み文字列を持つケースを先に見る。
            if (entry is IZLoggerFormattable entryFormattable)
            {
                return entryFormattable.ToString();
            }

            // ZLogger の structured log は IZLoggerFormattable を context 側に載せることもある。
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
    }
}
