#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// telemetry export record を受けるための index template writer。
/// ECS-lite 寄せは後続タスクで行うため、ここでは現在の export shape を安全に受ける mapping を出す。
/// </summary>
public sealed class ElasticTelemetryIndexTemplateWriter
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

        return File.WriteAllTextAsync(outputPath, ElasticTelemetryIndexTemplateDefinition.CreateArtifactJson(), cancellationToken);
    }
}
