#nullable enable

using MessagePack;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// inspector の 1 プロパティ行。
///
/// <para>
/// value はまず人間向けの文字列表現を主とし、
/// 必要なら raw value / path / unit で補助する。
/// 「編集可能な typed 値」をまだ厳密契約化しないのが v1 の意図。
/// </para>
/// </summary>
[MessagePackObject]
public sealed class InspectorPropertyDtoV1
{
    [Key(0)]
    public int PropertyId { get; set; }

    [Key(1)]
    public int ValueTypeId { get; set; }

    [Key(2)]
    public InspectorPropertyFlags Flags { get; set; } = InspectorPropertyFlags.None;

    [Key(3)]
    public string DisplayName { get; set; } = string.Empty;

    [Key(4)]
    public string ValueText { get; set; } = string.Empty;

    [Key(5)]
    public string? RawValue { get; set; }

    [Key(6)]
    public string? Unit { get; set; }

    [Key(7)]
    public string? Path { get; set; }
}
