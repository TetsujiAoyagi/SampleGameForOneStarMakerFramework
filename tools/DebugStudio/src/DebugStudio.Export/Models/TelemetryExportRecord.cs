#nullable enable

using System.Text.Json.Serialization;

namespace DebugStudio.Export.Models;

/// <summary>
/// telemetry / service status を Elastic-ready な共通 shape へ正規化した export record。
///
/// <para>
/// DebugStudio.App から切り出したのは、WPF UI ではなく export contract 自体を別 project の責務にしたいため。
/// これにより Elastic adapter や将来の CLI / CI artifact でも同じ schema を共有できる。
/// </para>
/// </summary>
public sealed class TelemetryExportRecord
{
    [JsonPropertyName("@timestamp")]
    public required string TimestampUtc { get; init; }

    public required long TimestampUnixTimeMilliseconds { get; init; }

    public required string Stream { get; init; }

    public string Source { get; init; } = "debugstudio";

    public string? Name { get; init; }

    public string? Status { get; init; }

    public string? Message { get; init; }

    public bool? IsSuccess { get; init; }

    public double? ElapsedMs { get; init; }

    public int? Level { get; init; }

    public long? TraceId { get; init; }

    public long? SpanId { get; init; }

    public long? ParentSpanId { get; init; }

    public int? TagBits { get; init; }

    public string[]? Tags { get; init; }

    public float? CpuTime { get; init; }

    public float? GpuTime { get; init; }

    public long? ManagedMem { get; init; }

    public long? NativeMem { get; init; }

    public int? SceneFrom { get; init; }

    public int? SceneTo { get; init; }
}
