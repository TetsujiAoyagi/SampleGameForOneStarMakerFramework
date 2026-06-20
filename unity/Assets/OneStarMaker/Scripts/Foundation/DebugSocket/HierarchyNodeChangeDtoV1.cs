#nullable enable

using MessagePack;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// hierarchy delta の 1 要素。
    /// Upsert 時は full node 情報を丸ごと載せる。
    /// </summary>
    [MessagePackObject]
    public sealed class HierarchyNodeChangeDtoV1
    {
        [Key(0)]
        public HierarchyChangeKind ChangeKind { get; set; } = HierarchyChangeKind.Unknown;

        [Key(1)]
        public long NodeId { get; set; }

        [Key(2)]
        public long ParentId { get; set; }

        [Key(3)]
        public int TypeId { get; set; }

        [Key(4)]
        public HierarchyNodeFlags Flags { get; set; } = HierarchyNodeFlags.None;

        [Key(5)]
        public int Depth { get; set; }

        [Key(6)]
        public int SiblingIndex { get; set; }

        [Key(7)]
        public int ChildCount { get; set; }

        [Key(8)]
        public int TraversalIndex { get; set; }

        [Key(9)]
        public string Name { get; set; } = string.Empty;

        [Key(10)]
        public string? TypeName { get; set; }
    }
}
