#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Models;

namespace DebugStudio.App.Core.Infrastructure;

/// <summary>
/// inspector export record を NDJSON へ書き出す infrastructure writer。
/// </summary>
public sealed class NdjsonInspectorExportWriter : IInspectorExportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public async Task WriteAsync(IReadOnlyList<InspectorExportRecord> records, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("An output path is required.", nameof(outputPath));
        }

        var directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        await using var writer = new StreamWriter(stream);

        for (var index = 0; index < records.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = JsonSerializer.Serialize(records[index], SerializerOptions);
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
