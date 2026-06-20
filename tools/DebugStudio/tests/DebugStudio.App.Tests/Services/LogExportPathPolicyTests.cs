#nullable enable

using System;
using System.IO;
using DebugStudio.App.Core.Services;

namespace DebugStudio.App.Tests.Services;

/// <summary>
/// R3 の export path policy を固定する。
/// directory / naming / extension ルールが将来の export writer 拡張でもぶれないことを確認する。
/// </summary>
public sealed class LogExportPathPolicyTests
{
    [Fact]
    public void CreateDefaultPath_日付ディレクトリ配下へtimestamp付きファイルを作る()
    {
        var policy = new LogExportPathPolicy(@"C:\TelemetryRoot");
        var now = new DateTimeOffset(2026, 4, 29, 10, 45, 12, TimeSpan.FromHours(9));

        var path = policy.CreateDefaultPath(".ndjson", now);

        Assert.Equal(
            @"C:\TelemetryRoot\logs\2026-04-29\debugstudio-log-20260429-104512.ndjson",
            path);
    }

    [Fact]
    public void UpdateExtension_ディレクトリとファイル名を保って拡張子だけ差し替える()
    {
        var policy = new LogExportPathPolicy(@"C:\TelemetryRoot");

        var path = policy.UpdateExtension(
            @"C:\TelemetryRoot\2026-04-29\debugstudio-log-20260429-104512.ndjson",
            ".csv");

        Assert.Equal(
            @"C:\TelemetryRoot\2026-04-29\debugstudio-log-20260429-104512.csv",
            path);
    }

    [Fact]
    public void UpdateExtension_空パスならfallbackを生成する()
    {
        var policy = new LogExportPathPolicy(@"C:\TelemetryRoot");
        var now = new DateTimeOffset(2026, 4, 29, 10, 45, 12, TimeSpan.FromHours(9));

        var path = policy.UpdateExtension(string.Empty, ".csv", now);

        Assert.Equal(
            @"C:\TelemetryRoot\logs\2026-04-29\debugstudio-log-20260429-104512.csv",
            path);
    }
}
