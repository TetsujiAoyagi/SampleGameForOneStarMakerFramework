#nullable enable

using System;
using MessagePack;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// Unity 側から返る inspector detail。
///
/// <para>
/// hierarchy node と 1:1 に結び付く viewer 用スナップショットであり、
/// 現時点では「表示に必要な情報をまとめて返す」ことを優先する。
/// property ごとの編集コマンドは後続 wave へ分離する前提。
/// </para>
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
