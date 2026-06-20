#nullable enable

using System;
using MessagePack;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// inspector detail 内の論理セクション。
/// GameObject ヘッダーや各 Component を 1 セクションとして束ねる想定。
/// </summary>
[MessagePackObject]
public sealed class InspectorSectionDtoV1
{
    [Key(0)]
    public int SectionId { get; set; }

    [Key(1)]
    public InspectorSectionKind Kind { get; set; } = InspectorSectionKind.Unknown;

    [Key(2)]
    public int TypeId { get; set; }

    [Key(3)]
    public string DisplayName { get; set; } = string.Empty;

    [Key(4)]
    public string? TypeName { get; set; }

    [Key(5)]
    public InspectorPropertyDtoV1[] Properties { get; set; } = Array.Empty<InspectorPropertyDtoV1>();
}
