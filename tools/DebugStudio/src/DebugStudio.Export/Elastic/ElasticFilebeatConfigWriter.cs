#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// DebugStudio export を取り込むための Filebeat sample config writer。
/// まずは telemetry export ディレクトリを読む最小構成だけを出し、
/// 後続で template / pipeline / data stream 連携を厚くしていく。
/// </summary>
public sealed class ElasticFilebeatConfigWriter
{
    public async Task WriteAsync(
        string outputPath,
        ElasticArtifactLayout artifactLayout,
        string exportRootDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("An output path is required.", nameof(outputPath));
        }

        ArgumentNullException.ThrowIfNull(artifactLayout);

        if (string.IsNullOrWhiteSpace(exportRootDirectory))
        {
            throw new ArgumentException("An export root directory is required.", nameof(exportRootDirectory));
        }

        var directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var telemetryPath = Path.Combine(exportRootDirectory, "telemetry", "**", "*.ndjson");
        var logPath = Path.Combine(exportRootDirectory, "logs", "**", "*.ndjson");
        var yaml = new StringBuilder()
            .AppendLine("filebeat.inputs:")
            .AppendLine("- type: filestream")
            .AppendLine("  id: debugstudio-telemetry")
            .AppendLine("  enabled: true")
            .AppendLine("  paths:")
            .Append("    - \"").Append(telemetryPath).AppendLine("\"")
            .AppendLine("  pipeline: debugstudio-telemetry")
            .AppendLine("- type: filestream")
            .AppendLine("  id: debugstudio-log")
            .AppendLine("  enabled: true")
            .AppendLine("  paths:")
            .Append("    - \"").Append(logPath).AppendLine("\"")
            .AppendLine("  pipeline: debugstudio-log")
            .AppendLine()
            .AppendLine("output.elasticsearch:")
            .AppendLine("  hosts: [\"http://localhost:9200\"]")
            .ToString();

        await File.WriteAllTextAsync(outputPath, yaml, cancellationToken).ConfigureAwait(false);
    }
}
