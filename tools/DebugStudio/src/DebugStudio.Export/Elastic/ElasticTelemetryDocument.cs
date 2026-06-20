#nullable enable

namespace DebugStudio.Export.Elastic;

/// <summary>
/// Elastic 向けに additive 拡張した telemetry document。
/// 既存 export record の field を残しつつ、ECS-lite で見たい塊を別プロパティとして足す。
/// </summary>
public sealed class ElasticTelemetryDocument
{
    public required string TimestampUtc { get; init; }

    public required long TimestampUnixTimeMilliseconds { get; init; }

    public required string Stream { get; init; }

    public string Source { get; init; } = "debugstudio";

    public string? Name { get; init; }

    public bool? IsSuccess { get; init; }

    public long? TraceId { get; init; }

    public long? SpanId { get; init; }

    public long? ParentSpanId { get; init; }

    public string[]? Tags { get; init; }

    public required ElasticTelemetryEvent Event { get; init; }

    public required ElasticTelemetryTrace Trace { get; init; }

    public required ElasticTelemetrySpan Span { get; init; }

    public required ElasticTelemetryService Service { get; init; }
}

public sealed class ElasticTelemetryEvent
{
    public required string Category { get; init; }

    public string? Action { get; init; }
}

public sealed class ElasticTelemetryTrace
{
    public string? Id { get; init; }
}

public sealed class ElasticTelemetrySpan
{
    public string? Id { get; init; }

    public string? ParentId { get; init; }
}

public sealed class ElasticTelemetryService
{
    public required string Name { get; init; }
}
