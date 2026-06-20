#nullable enable

using MessagePack;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// DebugStudio から Unity へ送る inspector 取得要求。
    /// target id と query flags だけに絞った viewer-first 契約。
    /// </summary>
    [MessagePackObject]
    public sealed class InspectorQueryEnvelopeV1
    {
        [Key(0)]
        public int SchemaVersion { get; set; } = 1;

        [Key(1)]
        public long TargetId { get; set; }

        [Key(2)]
        public InspectorQueryFlags QueryFlags { get; set; } =
            InspectorQueryFlags.IncludeMetadata |
            InspectorQueryFlags.IncludeComponents |
            InspectorQueryFlags.IncludeProperties;
    }
}
