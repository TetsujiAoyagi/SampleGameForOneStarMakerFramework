#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// telemetry ingest pipeline の最小 writer。
/// まずは stream 固定と ingest 元の印付けだけを入れ、後続で schema alignment に合わせて processor を増やす。
/// </summary>
public sealed class ElasticTelemetryIngestPipelineWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public async Task WriteAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("An output path is required.", nameof(outputPath));
        }

        var directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var document = new
        {
            description = "debugstudio telemetry ingest pipeline",
            processors = new object[]
            {
                new
                {
                    set = new
                    {
                        field = "stream",
                        value = "telemetry",
                    }
                },
                new
                {
                    set = new
                    {
                        field = "observer.name",
                        value = "DebugStudio",
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(document, SerializerOptions);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken).ConfigureAwait(false);
    }
}
