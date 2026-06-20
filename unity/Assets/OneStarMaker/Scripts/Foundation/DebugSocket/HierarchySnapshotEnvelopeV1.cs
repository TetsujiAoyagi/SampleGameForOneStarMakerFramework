#nullable enable

using System;
using MessagePack;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// hierarchy 全量スナップショット。
    /// viewer-first な v1 ではまず snapshot を基本単位とする。
    /// </summary>
    [MessagePackObject]
    public sealed class HierarchySnapshotEnvelopeV1
    {
        [Key(0)]
        public int SchemaVersion { get; set; } = 1;

        [Key(1)]
        public long Revision { get; set; }

        [Key(2)]
        public long CapturedAtUnixTimeMilliseconds { get; set; }

        [Key(3)]
        public string ScopeName { get; set; } = string.Empty;

        [Key(4)]
        public HierarchyNodeDtoV1[] Nodes { get; set; } = Array.Empty<HierarchyNodeDtoV1>();
    }
}
