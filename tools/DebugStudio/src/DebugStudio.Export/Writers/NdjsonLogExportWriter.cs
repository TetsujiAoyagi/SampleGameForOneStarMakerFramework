#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Writers;

/// <summary>
/// log export record を NDJSON へ書き出す。
/// </summary>
public sealed class NdjsonLogExportWriter : ILogExportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public LogExportFormat Format => LogExportFormat.Ndjson;

    public async Task WriteAsync(IReadOnlyList<LogExportRecord> logs, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logs);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("An output path is required.", nameof(outputPath));
        }

        var directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await using var stream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        await using var writer = new StreamWriter(stream);

        foreach (var log in logs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var json = JsonSerializer.Serialize(log, SerializerOptions);
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
