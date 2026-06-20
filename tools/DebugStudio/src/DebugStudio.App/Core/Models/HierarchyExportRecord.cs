#nullable enable

using System.Text.Json.Serialization;

namespace DebugStudio.App.Core.Models;

/// <summary>
/// hierarchy node を export 用の normalized record へ写した 1 行。
/// node 単位で平坦化することで、後段の検索・集計基盤でも tree 全体を扱いやすくする。
/// </summary>
public sealed class HierarchyExportRecord
{
    [JsonPropertyName("@timestamp")]
    public required string TimestampUtc { get; init; }

    public required long TimestampUnixTimeMilliseconds { get; init; }

    public string Source { get; init; } = "debugstudio";

    public string Stream { get; init; } = "hierarchy";

    public required string ScopeName { get; init; }

    public required long Revision { get; init; }

    public long? SelectedNodeId { get; init; }

    public required long NodeId { get; init; }

    public required long ParentId { get; init; }

    public required int TypeId { get; init; }

    public string? TypeName { get; init; }

    public required string Name { get; init; }

    public required int Depth { get; init; }

    public required int SiblingIndex { get; init; }

    public required int ChildCount { get; init; }

    public required int TraversalIndex { get; init; }

    public required string Flags { get; init; }
}
