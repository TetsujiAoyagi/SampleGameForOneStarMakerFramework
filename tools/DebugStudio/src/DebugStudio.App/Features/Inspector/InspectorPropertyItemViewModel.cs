#nullable enable

using DebugStudio.App.Core.Models;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Contracts.Schema;

namespace DebugStudio.App.Features.Inspector;

/// <summary>
/// inspector ListView へ流す flatten 済み行。
/// section 名を同梱しておくことで、WPF 側で group template を組まなくても読める表を作れる。
/// </summary>
public sealed class InspectorPropertyItemViewModel
{
    public required string SectionName { get; init; }

    public required string PropertyName { get; init; }

    public required string ValueText { get; init; }

    public required string TypeText { get; init; }

    public required string FlagsText { get; init; }

    public static InspectorPropertyItemViewModel FromRecord(string sectionName, InspectorPropertyRecord record)
    {
        return new InspectorPropertyItemViewModel
        {
            SectionName = sectionName,
            PropertyName = record.DisplayName,
            ValueText = string.IsNullOrWhiteSpace(record.Unit)
                ? record.ValueText
                : $"{record.ValueText} {record.Unit}",
            TypeText = record.ValueTypeId is >= 0 and <= (int)SchemaValueTypeId.UnixTimeMilliseconds
                ? ((SchemaValueTypeId)record.ValueTypeId).ToString()
                : $"Type {record.ValueTypeId}",
            FlagsText = FormatFlags(record.Flags),
        };
    }

    private static string FormatFlags(InspectorPropertyFlags flags)
    {
        if (flags == InspectorPropertyFlags.None)
        {
            return "None";
        }

        if ((flags & InspectorPropertyFlags.ReadOnly) != 0)
        {
            return "ReadOnly";
        }

        if ((flags & InspectorPropertyFlags.MissingValue) != 0)
        {
            return "Missing";
        }

        return flags.ToString();
    }
}
