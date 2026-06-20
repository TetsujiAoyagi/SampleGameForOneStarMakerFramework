#nullable enable

using System;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// inspector export 専用の出力 path policy。
/// </summary>
public sealed class InspectorExportPathPolicy
{
    private readonly ExportPathPolicy _innerPolicy;

    public InspectorExportPathPolicy(string? rootDirectory = null)
    {
        _innerPolicy = new ExportPathPolicy(rootDirectory);
    }

    public string CreateDefaultPath(string extension = ".ndjson", DateTimeOffset? now = null)
    {
        return _innerPolicy.CreateDefaultPath("inspector", "debugstudio-inspector", extension, now);
    }
}
