#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// telemetry ingest pipeline の最小 writer。
/// まずは stream 固定と ingest 元の印付けだけを入れ、後続で schema alignment に合わせて processor を増やす。
/// </summary>
public sealed class ElasticTelemetryIngestPipelineWriter
{
    public Task WriteAsync(string outputPath, CancellationToken cancellationToken = default)
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

        return File.WriteAllTextAsync(outputPath, ElasticTelemetryIngestPipelineDefinition.CreateArtifactJson(), cancellationToken);
    }
}
