#nullable enable

using DebugStudio.App.Core.Formatting;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Contracts.Schema;

namespace DebugStudio.App.Core.Models;

/// <summary>
/// app 層で保持する log の生レコード。
///
/// <para>
/// これは WPF item 表示専用の ViewModel ではなく、
/// transport DTO から「検索・保持・export に必要な意味」を写した中間モデルである。
/// UI はこの record を読むが、保持責務や schema 解釈は UI に漏らさない。
/// </para>
/// </summary>
public sealed class LogRecord
{
    private LogRecord(
        long sequenceNumber,
        int schemaVersion,
        string applicationName,
        long timestampUnixTimeMilliseconds,
        string category,
        LogEntryKind kind,
        int rawLogLevel,
        int eventId,
        string? eventName,
        string message,
        string? exception,
        int threadId,
        string? threadName,
        string? memberName,
        string? filePath,
        int lineNumber,
        string sessionId,
        long producerSequence,
        int? unityFrameAtEmit,
        long? traceId,
        long? spanId)
    {
        SequenceNumber = sequenceNumber;
        SchemaVersion = schemaVersion;
        ApplicationName = applicationName;
        TimestampUnixTimeMilliseconds = timestampUnixTimeMilliseconds;
        Category = category;
        Kind = kind;
        RawLogLevel = rawLogLevel;
        EventId = eventId;
        EventName = eventName;
        Message = message;
        Exception = exception;
        ThreadId = threadId;
        ThreadName = threadName;
        MemberName = memberName;
        FilePath = filePath;
        LineNumber = lineNumber;
        SessionId = sessionId;
        ProducerSequence = producerSequence;
        UnityFrameAtEmit = unityFrameAtEmit;
        TraceId = traceId;
        SpanId = spanId;
    }

    public long SequenceNumber { get; }

    public int SchemaVersion { get; }

    public string ApplicationName { get; }

    public long TimestampUnixTimeMilliseconds { get; }

    public string TimestampText => DebugStudioTextFormatter.FormatUnixTime(TimestampUnixTimeMilliseconds);

    public string Category { get; }

    public LogEntryKind Kind { get; }

    /// <summary>
    /// ログエントリのレベル。Kind と同等ですが、テスト互換性のためのエイリアスとして提供。
    /// </summary>
    public LogEntryKind LogLevel => Kind;

    public string KindText => DebugStudioTextFormatter.FormatLogKind(Kind, RawLogLevel);

    public int RawLogLevel { get; }

    public int EventId { get; }

    public string? EventName { get; }

    public string Message { get; }

    public string? Exception { get; }

    public int ThreadId { get; }

    public string? ThreadName { get; }

    public string? MemberName { get; }

    public string? FilePath { get; }

    public int LineNumber { get; }

    /// <summary>
    /// Unity producer が wire 化時に付与した session ID。export 時の後付けは行わない。
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// session 内 producer 順序。DebugStudio 受信順 (<see cref="SequenceNumber"/>) とは別軸。
    /// </summary>
    public long ProducerSequence { get; }

    /// <summary>
    /// Log formatter が envelope を組み立てた時点の Unity frame。未観測時は null。
    /// </summary>
    public int? UnityFrameAtEmit { get; }

    /// <summary>active telemetry span 内の Log のみ。span 外は null。</summary>
    public long? TraceId { get; }

    /// <summary>active telemetry span 内の Log のみ。span 外は null。</summary>
    public long? SpanId { get; }

    public string Summary => DebugStudioTextFormatter.FormatLog(this);

    public static LogRecord FromEnvelope(long sequenceNumber, LogEnvelopeV1 envelope)
    {
        return new LogRecord(
            sequenceNumber,
            envelope.SchemaVersion,
            envelope.ApplicationName,
            envelope.TimestampUnixTimeMilliseconds,
            envelope.Category,
            envelope.Kind,
            envelope.LogLevel,
            envelope.EventId,
            envelope.EventName,
            envelope.Message,
            envelope.Exception,
            envelope.ThreadId,
            envelope.ThreadName,
            envelope.MemberName,
            envelope.FilePath,
            envelope.LineNumber,
            envelope.SessionId,
            envelope.ProducerSequence,
            envelope.UnityFrameAtEmit,
            envelope.TraceId,
            envelope.SpanId);
    }
}
