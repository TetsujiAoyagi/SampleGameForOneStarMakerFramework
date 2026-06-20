#nullable enable

using System.Text.Json.Serialization;

namespace DebugStudio.Export.Models;

/// <summary>
/// log export 用の共通 contract。
/// 既存 NDJSON 互換を残しつつ、Elastic 用 additive field も持てる shape にしておく。
/// </summary>
public sealed class LogExportRecord
{
    [JsonPropertyName("@timestamp")]
    public required string TimestampUtc { get; init; }

    public required long SequenceNumber { get; init; }

    public required string ApplicationName { get; init; }

    public required long TimestampUnixTimeMilliseconds { get; init; }

    public required string TimestampLocal { get; init; }

    public required string Kind { get; init; }

    public required int RawLogLevel { get; init; }

    public required string Category { get; init; }

    public required int EventId { get; init; }

    public string? EventName { get; init; }

    public required string Message { get; init; }

    public string? Exception { get; init; }

    public required int ThreadId { get; init; }

    public string? ThreadName { get; init; }

    public string? MemberName { get; init; }

    public string? FilePath { get; init; }

    public required int LineNumber { get; init; }

    public string? ServiceName { get; init; }

    public string? LogLevel { get; init; }
}
