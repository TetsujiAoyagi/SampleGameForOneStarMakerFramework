#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Export.Elastic;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Writers;

/// <summary>
/// telemetry export record を Elastic `_bulk` API へそのまま流し込みやすい NDJSON へ変換する writer。
///
/// <para>
/// Elastic 固有の責務は WPF app ではなく export project 側へ閉じ込める。
/// これにより UI と ops artifact の境界を保ったまま、CLI や CI からも再利用しやすくする。
/// </para>
/// </summary>
public sealed class ElasticBulkTelemetryExportWriter : ITelemetryExportWriter
{
    public TelemetryExportFormat Format => TelemetryExportFormat.ElasticBulk;

    public async Task WriteAsync(IReadOnlyList<TelemetryExportRecord> records, string outputPath, CancellationToken cancellationToken = default)
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

        await using var stream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        cancellationToken.ThrowIfCancellationRequested();
        ElasticBulkTelemetryNdjsonBuilder.WriteBulkPayload(records, stream);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
