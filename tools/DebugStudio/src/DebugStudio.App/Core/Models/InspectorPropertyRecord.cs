#nullable enable

using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Models;

/// <summary>
/// inspector 表示用の 1 行データ。
/// raw value や path も保持するが、UI は必要になるまで文字列表示を優先する。
/// </summary>
public sealed class InspectorPropertyRecord
{
    public required int PropertyId { get; init; }

    public required int ValueTypeId { get; init; }

    public required InspectorPropertyFlags Flags { get; init; }

    public required string DisplayName { get; init; }

    public required string ValueText { get; init; }

    public string? RawValue { get; init; }

    public string? Unit { get; init; }

    public string? Path { get; init; }

    public static InspectorPropertyRecord FromDto(InspectorPropertyDtoV1 dto)
    {
        return new InspectorPropertyRecord
        {
            PropertyId = dto.PropertyId,
            ValueTypeId = dto.ValueTypeId,
            Flags = dto.Flags,
            DisplayName = dto.DisplayName,
            ValueText = dto.ValueText,
            RawValue = dto.RawValue,
            Unit = dto.Unit,
            Path = dto.Path,
        };
    }
}
