#nullable enable

using DebugStudio.App.Core.Formatting;
using DebugStudio.Contracts.Schema;

namespace DebugStudio.App.Tests;

/// <summary>
/// telemetry tag bitset の読み替え規約を固定する。
/// UI/export の両方がここに依存するため、語彙の崩れを小さい単位で検知する。
/// </summary>
public sealed class DebugTelemetryTagFormatterTests
{
    [Fact]
    public void ToNames_既知bitsetをtag名へ復元できる()
    {
        var names = DebugTelemetryTagFormatter.ToNames(
            (int)(DebugTelemetryTagBits.Bottleneck | DebugTelemetryTagBits.FatalError));

        Assert.Equal(new[] { "Bottleneck", "FatalError" }, names);
    }

    [Fact]
    public void ToNames_未知bitはraw表現を残す()
    {
        var names = DebugTelemetryTagFormatter.ToNames(1 << 12);

        Assert.Equal(new[] { "Unknown(0x1000)" }, names);
    }
}
