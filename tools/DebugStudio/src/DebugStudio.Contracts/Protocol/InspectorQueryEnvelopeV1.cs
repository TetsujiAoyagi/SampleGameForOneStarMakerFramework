#nullable enable

using MessagePack;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// DebugStudio から Unity へ送る inspector 取得要求。
///
/// <para>
/// 今回は target id と query flag に留め、
/// ランタイム編集や双方向同期に踏み込まない viewer-first 契約としている。
/// </para>
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
