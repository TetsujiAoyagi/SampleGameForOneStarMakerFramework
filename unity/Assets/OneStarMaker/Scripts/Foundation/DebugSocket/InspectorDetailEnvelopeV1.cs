#nullable enable

using System;
using MessagePack;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// Unity 側から返す inspector detail。
    /// 現段階では表示スナップショットとして扱い、編集コマンドには踏み込まない。
    /// </summary>
    [MessagePackObject]
    public sealed class InspectorDetailEnvelopeV1
    {
        [Key(0)]
        public int SchemaVersion { get; set; } = 1;

        [Key(1)]
        public long Revision { get; set; }

        [Key(2)]
        public long CapturedAtUnixTimeMilliseconds { get; set; }

        [Key(3)]
        public long TargetId { get; set; }

        [Key(4)]
        public string TargetName { get; set; } = string.Empty;

        [Key(5)]
        public int TargetTypeId { get; set; }

        [Key(6)]
        public string? TargetTypeName { get; set; }

        [Key(7)]
        public InspectorDetailState State { get; set; } = InspectorDetailState.Unknown;

        [Key(8)]
        public string Message { get; set; } = string.Empty;

        [Key(9)]
        public InspectorSectionDtoV1[] Sections { get; set; } = Array.Empty<InspectorSectionDtoV1>();
    }
}
