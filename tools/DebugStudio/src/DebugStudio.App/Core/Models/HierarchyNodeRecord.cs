#nullable enable

using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Models;

/// <summary>
/// WPF 側で保持する hierarchy node の読み取り専用 record。
///
/// <para>
/// transport DTO から一度 app model へ写しておくことで、
/// 後続で UI 専用の補助情報を足しても wire contract を汚さずに済む。
/// </para>
/// </summary>
public sealed class HierarchyNodeRecord
{
    public required long NodeId { get; init; }

    public required long ParentId { get; init; }

    public required int TypeId { get; init; }

    public required HierarchyNodeFlags Flags { get; init; }

    public required int Depth { get; init; }

    public required int SiblingIndex { get; init; }

    public required int ChildCount { get; init; }

    public required int TraversalIndex { get; init; }

    public required string Name { get; init; }

    public string? TypeName { get; init; }

    public static HierarchyNodeRecord FromDto(HierarchyNodeDtoV1 dto)
    {
        return new HierarchyNodeRecord
        {
            NodeId = dto.NodeId,
            ParentId = dto.ParentId,
            TypeId = dto.TypeId,
            Flags = dto.Flags,
            Depth = dto.Depth,
            SiblingIndex = dto.SiblingIndex,
            ChildCount = dto.ChildCount,
            TraversalIndex = dto.TraversalIndex,
            Name = dto.Name,
            TypeName = dto.TypeName,
        };
    }

    public static HierarchyNodeRecord FromChange(HierarchyNodeChangeDtoV1 change)
    {
        return new HierarchyNodeRecord
        {
            NodeId = change.NodeId,
            ParentId = change.ParentId,
            TypeId = change.TypeId,
            Flags = change.Flags,
            Depth = change.Depth,
            SiblingIndex = change.SiblingIndex,
            ChildCount = change.ChildCount,
            TraversalIndex = change.TraversalIndex,
            Name = change.Name,
            TypeName = change.TypeName,
        };
    }
}
