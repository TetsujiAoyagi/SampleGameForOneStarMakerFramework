#nullable enable

using System;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// telemetry export 専用の出力 path policy。
/// Filebeat 側が拾いやすいよう、telemetry 用ディレクトリ配下へ NDJSON をまとめる。
/// </summary>
public sealed class TelemetryExportPathPolicy
{
    private readonly ExportPathPolicy _innerPolicy;

    public TelemetryExportPathPolicy(string? rootDirectory = null)
    {
        _innerPolicy = new ExportPathPolicy(rootDirectory);
    }

    public string CreateDefaultPath(string extension = ".ndjson", DateTimeOffset? now = null)
    {
        return _innerPolicy.CreateDefaultPath("telemetry", "debugstudio-telemetry", extension, now);
    }

    public string UpdateExtension(string currentPath, string extension)
    {
        return _innerPolicy.UpdateExtension(currentPath, CreateDefaultPath(extension), extension);
    }
}
