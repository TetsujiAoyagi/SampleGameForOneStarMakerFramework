#nullable enable

using System;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// hierarchy export 専用の出力 path policy。
/// </summary>
public sealed class HierarchyExportPathPolicy
{
    private readonly ExportPathPolicy _innerPolicy;

    public HierarchyExportPathPolicy(string? rootDirectory = null)
    {
        _innerPolicy = new ExportPathPolicy(rootDirectory);
    }

    public string CreateDefaultPath(string extension = ".ndjson", DateTimeOffset? now = null)
    {
        return _innerPolicy.CreateDefaultPath("hierarchy", "debugstudio-hierarchy", extension, now);
    }
}
