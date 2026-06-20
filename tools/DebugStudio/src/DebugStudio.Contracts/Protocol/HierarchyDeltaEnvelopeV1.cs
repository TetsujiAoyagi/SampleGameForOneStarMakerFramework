#nullable enable

using System;
using MessagePack;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// hierarchy 差分更新。
///
/// <para>
/// base revision を含めることで sender / receiver のずれを検出する。
/// receiver は current revision と一致する delta だけを適用し、
/// 欠落や順序逆転が疑われる場合は次の snapshot で再同期する。
/// </para>
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
