#nullable enable

using DebugStudio.Contracts.Schema;
using MessagePack;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// 現行 Unity DebugSocket protocol と互換の log envelope。
///
/// <para>
/// この DTO 自体は transport 契約なので、WPF 用の表示文字列や列幅といった
/// ViewModel 都合の状態は持たせない。UI はこの契約を app 層で
/// schema-aware な model へ写像してから扱う。
/// その分離が後続 wave の typed inspector / hierarchy で再利用しやすい土台になる。
/// </para>
/// </summary>
[MessagePackObject]
public sealed class LogEnvelopeV1
{
    [Key(0)]
    public int SchemaVersion { get; set; } = 1;

    [Key(1)]
    public string ApplicationName { get; set; } = string.Empty;

    [Key(2)]
    public long TimestampUnixTimeMilliseconds { get; set; }

    [Key(3)]
    public string Category { get; set; } = string.Empty;

    [Key(4)]
    public int LogLevel { get; set; }

    [Key(5)]
    public int EventId { get; set; }

    [Key(6)]
    public string? EventName { get; set; }

    [Key(7)]
    public string Message { get; set; } = string.Empty;

    [Key(8)]
    public string? Exception { get; set; }

    [Key(9)]
    public int ThreadId { get; set; }

    [Key(10)]
    public string? ThreadName { get; set; }

    [Key(11)]
    public string? MemberName { get; set; }

    [Key(12)]
    public string? FilePath { get; set; }

    [Key(13)]
    public int LineNumber { get; set; }

    /// <summary>
    /// Unity 起動単位の session ID。DebugSocket handshake Welcome と同一。
    /// </summary>
    [Key(14)]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// session 内で Telemetry と共有する producer 順序。
    /// </summary>
    [Key(15)]
    public long ProducerSequence { get; set; }

    /// <summary>
    /// formatter が envelope を組み立てた時点の Unity player-loop frame。main thread 以外は null。
    /// </summary>
    [Key(16)]
    public int? UnityFrameAtEmit { get; set; }

    /// <summary>
    /// active telemetry span がある場合のみ付与。span 外 Log は null。
    /// </summary>
    [Key(17)]
    public long? TraceId { get; set; }

    /// <summary>
    /// active telemetry span がある場合のみ付与。span 外 Log は null。
    /// </summary>
    [Key(18)]
    public long? SpanId { get; set; }

    /// <summary>
    /// wire 上の int level を、共有 schema で再利用できる kind へ写した読み取り専用ビュー。
    /// MessagePack key を増やさず transport 互換性を維持するため、serialize 対象にはしない。
    /// </summary>
    [IgnoreMember]
    public LogEntryKind Kind => LogLevel switch
    {
        0 => LogEntryKind.Trace,
        1 => LogEntryKind.Debug,
        2 => LogEntryKind.Information,
        3 => LogEntryKind.Warning,
        4 => LogEntryKind.Error,
        5 => LogEntryKind.Critical,
        6 => LogEntryKind.None,
        _ => LogEntryKind.Unknown,
    };
}
