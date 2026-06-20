#nullable enable

using System;
using MessagePack;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// hierarchy 全量スナップショット。
///
/// <para>
/// viewer-first な v1 では「定期的な全量再送」でも十分成立するため、
/// まずは snapshot を第一級に据える。
/// delta は後続最適化用だが、receiver は snapshot だけでも最低限の UX を提供できる。
/// </para>
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
