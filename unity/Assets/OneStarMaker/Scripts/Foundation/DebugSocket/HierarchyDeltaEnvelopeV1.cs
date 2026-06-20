#nullable enable

using System;
using MessagePack;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// hierarchy 差分更新。
    /// base revision を含め、receiver は一致する delta だけを適用する。
    /// ずれた場合は次の snapshot で再同期する想定。
    /// </summary>
    [MessagePackObject]
    public sealed class HierarchyDeltaEnvelopeV1
    {
        [Key(0)]
        public int SchemaVersion { get; set; } = 1;

        [Key(1)]
        public long BaseRevision { get; set; }

        [Key(2)]
        public long Revision { get; set; }

        [Key(3)]
        public long CapturedAtUnixTimeMilliseconds { get; set; }

        [Key(4)]
        public string ScopeName { get; set; } = string.Empty;

        [Key(5)]
        public HierarchyNodeChangeDtoV1[] Changes { get; set; } = Array.Empty<HierarchyNodeChangeDtoV1>();
    }
}
