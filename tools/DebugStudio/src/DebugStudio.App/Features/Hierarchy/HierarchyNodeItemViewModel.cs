#nullable enable

using DebugStudio.App.Core.Models;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Features.Hierarchy;

/// <summary>
/// hierarchy 一覧に表示する軽量 item。
/// WPF 専用の整形済み文字列はここで持ち、store の raw record へ逆流させない。
/// </summary>
public sealed class HierarchyNodeItemViewModel
{
    public required long NodeId { get; init; }

    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    public required string TypeLabel { get; init; }

    public required string FlagsText { get; init; }

    public static HierarchyNodeItemViewModel FromRecord(HierarchyNodeRecord record)
    {
        return new HierarchyNodeItemViewModel
        {
            NodeId = record.NodeId,
            Name = record.Name,
            DisplayName = $"{new string(' ', record.Depth * 2)}{record.Name}",
            TypeLabel = string.IsNullOrWhiteSpace(record.TypeName) ? $"Type {record.TypeId}" : record.TypeName,
            FlagsText = FormatFlags(record.Flags, record.ChildCount),
        };
    }

    private static string FormatFlags(HierarchyNodeFlags flags, int childCount)
    {
        var activeText = (flags & HierarchyNodeFlags.ActiveInHierarchy) != 0 ? "Active" : "Inactive";
        var rootText = (flags & HierarchyNodeFlags.SceneRoot) != 0 ? ", Root" : string.Empty;
        var childText = childCount > 0 ? $", Children={childCount}" : string.Empty;
        return $"{activeText}{rootText}{childText}";
    }
}
