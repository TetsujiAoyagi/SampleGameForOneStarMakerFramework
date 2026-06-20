#nullable enable

using System.Linq;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Models;

/// <summary>
/// inspector の論理セクション。
/// section 名と property 群をまとめて持つことで、UI は flatten / group のどちらにも対応しやすい。
/// </summary>
public sealed class InspectorSectionRecord
{
    public required int SectionId { get; init; }

    public required InspectorSectionKind Kind { get; init; }

    public required int TypeId { get; init; }

    public required string DisplayName { get; init; }

    public string? TypeName { get; init; }

    public required InspectorPropertyRecord[] Properties { get; init; }

    public static InspectorSectionRecord FromDto(InspectorSectionDtoV1 dto)
    {
        return new InspectorSectionRecord
        {
            SectionId = dto.SectionId,
            Kind = dto.Kind,
            TypeId = dto.TypeId,
            DisplayName = dto.DisplayName,
            TypeName = dto.TypeName,
            Properties = dto.Properties.Select(InspectorPropertyRecord.FromDto).ToArray(),
        };
    }
}
