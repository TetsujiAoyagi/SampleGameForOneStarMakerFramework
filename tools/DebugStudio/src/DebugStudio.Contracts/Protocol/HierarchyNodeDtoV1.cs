#nullable enable

using MessagePack;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// hierarchy 表示に必要な最小ノード DTO。
///
/// <para>
/// node id / parent id / traversal index を持たせることで、
/// receiver 側は Unity の GameObject 参照を持たずに tree を再構築できる。
/// type はまず compact な int を主とし、表示補助として type name を optional で残す。
/// </para>
/// </summary>
[MessagePackObject]
public sealed class HierarchyNodeDtoV1
{
    [Key(0)]
    public long NodeId { get; set; }

    [Key(1)]
    public long ParentId { get; set; }

    [Key(2)]
    public int TypeId { get; set; }

    [Key(3)]
    public HierarchyNodeFlags Flags { get; set; } = HierarchyNodeFlags.None;

    [Key(4)]
    public int Depth { get; set; }

    [Key(5)]
    public int SiblingIndex { get; set; }

    [Key(6)]
    public int ChildCount { get; set; }

    [Key(7)]
    public int TraversalIndex { get; set; }

    [Key(8)]
    public string Name { get; set; } = string.Empty;

    [Key(9)]
    public string? TypeName { get; set; }
}
