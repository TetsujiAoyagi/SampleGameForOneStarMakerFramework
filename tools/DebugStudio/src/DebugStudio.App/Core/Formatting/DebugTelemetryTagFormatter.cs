#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using DebugStudio.Contracts.Schema;

namespace DebugStudio.App.Core.Formatting;

/// <summary>
/// telemetry の tag bitset を UI / export 向けの安定した文字列へ変換する helper。
///
/// <para>
/// transport は互換性を優先して int bitset のまま保持し、
/// 表示境界だけで「人間が読める分類名」へ戻す。
/// これにより wire shape を増やさずに taxonomy の意味を UI/export へ届かせる。
/// </para>
/// </summary>
public static class DebugTelemetryTagFormatter
{
    private const int KnownMask =
        (int)(
            DebugTelemetryTagBits.Bottleneck |
            DebugTelemetryTagBits.CpuTimeOver |
            DebugTelemetryTagBits.GpuTimeOver |
            DebugTelemetryTagBits.ManagedMemoryOver |
            DebugTelemetryTagBits.NativeMemoryOver |
            DebugTelemetryTagBits.FrameRateDrop |
            DebugTelemetryTagBits.AllocSpike |
            DebugTelemetryTagBits.InputLatency |
            DebugTelemetryTagBits.NetworkIssue |
            DebugTelemetryTagBits.FatalError);

    private static readonly (DebugTelemetryTagBits Bit, string Name)[] s_knownTags =
    {
        (DebugTelemetryTagBits.Bottleneck, nameof(DebugTelemetryTagBits.Bottleneck)),
        (DebugTelemetryTagBits.CpuTimeOver, nameof(DebugTelemetryTagBits.CpuTimeOver)),
        (DebugTelemetryTagBits.GpuTimeOver, nameof(DebugTelemetryTagBits.GpuTimeOver)),
        (DebugTelemetryTagBits.ManagedMemoryOver, nameof(DebugTelemetryTagBits.ManagedMemoryOver)),
        (DebugTelemetryTagBits.NativeMemoryOver, nameof(DebugTelemetryTagBits.NativeMemoryOver)),
        (DebugTelemetryTagBits.FrameRateDrop, nameof(DebugTelemetryTagBits.FrameRateDrop)),
        (DebugTelemetryTagBits.AllocSpike, nameof(DebugTelemetryTagBits.AllocSpike)),
        (DebugTelemetryTagBits.InputLatency, nameof(DebugTelemetryTagBits.InputLatency)),
        (DebugTelemetryTagBits.NetworkIssue, nameof(DebugTelemetryTagBits.NetworkIssue)),
        (DebugTelemetryTagBits.FatalError, nameof(DebugTelemetryTagBits.FatalError)),
    };

    public static string[] ToNames(int? tagBits)
    {
        if (!tagBits.HasValue || tagBits.Value == 0)
        {
            return Array.Empty<string>();
        }

        var rawBits = tagBits.Value;
        var names = new List<string>(4);
        var typedBits = (DebugTelemetryTagBits)rawBits;

        for (var index = 0; index < s_knownTags.Length; index++)
        {
            var (bit, name) = s_knownTags[index];
            if ((typedBits & bit) != 0)
            {
                names.Add(name);
            }
        }

        var unknownBits = rawBits & ~KnownMask;
        if (unknownBits != 0)
        {
            names.Add($"Unknown(0x{unknownBits:X})");
        }

        return names.Count == 0
            ? Array.Empty<string>()
            : names.ToArray();
    }

    public static string FormatInline(int? tagBits)
    {
        var names = ToNames(tagBits);
        return names.Length == 0
            ? string.Empty
            : string.Join(", ", names);
    }
}
