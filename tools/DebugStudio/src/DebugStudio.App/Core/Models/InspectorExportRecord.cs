#nullable enable

using System.Text.Json.Serialization;

namespace DebugStudio.App.Core.Models;

/// <summary>
/// inspector 文書を export 用の normalized record へ平坦化した 1 行。
/// section / property 単位へ落とすことで、後段では target や property 名で横断検索しやすくなる。
/// </summary>
public sealed class InspectorExportRecord
{
    [JsonPropertyName("@timestamp")]
    public required string TimestampUtc { get; init; }

    public required long TimestampUnixTimeMilliseconds { get; init; }

    public string Source { get; init; } = "debugstudio";

    public string Stream { get; init; } = "inspector";

    public required long TargetId { get; init; }

    public required string TargetName { get; init; }

    public string? TargetTypeName { get; init; }

    public required long Revision { get; init; }

    public required string State { get; init; }

    public required string Message { get; init; }

    public int? SectionId { get; init; }

    public string? SectionKind { get; init; }

    public string? SectionDisplayName { get; init; }

    public string? SectionTypeName { get; init; }

    public int? PropertyId { get; init; }

    public string? PropertyName { get; init; }

    public int? ValueTypeId { get; init; }

    public string? ValueText { get; init; }

    public string? RawValue { get; init; }

    public string? Unit { get; init; }

    public string? Path { get; init; }

    public string? Flags { get; init; }
}
