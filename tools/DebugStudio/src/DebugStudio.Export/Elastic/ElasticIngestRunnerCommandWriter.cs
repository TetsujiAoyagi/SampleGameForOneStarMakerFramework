#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// Elastic と Kibana への投入を順番付きで呼ぶ PowerShell runner。
/// </summary>
public sealed class ElasticIngestRunnerCommandWriter
{
    public async Task WriteAsync(
        string outputPath,
        ElasticArtifactLayout artifactLayout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("An output path is required.", nameof(outputPath));
        }

        ArgumentNullException.ThrowIfNull(artifactLayout);

        var directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var bulkScriptPath = artifactLayout.BulkImportCommandPath;
        var kibanaScriptPath = artifactLayout.KibanaImportCommandPath;

        var script = new StringBuilder()
            .AppendLine("param(")
            .AppendLine("    [string]$ElasticUrl = \"http://localhost:9200\",")
            .AppendLine("    [string]$KibanaUrl = \"http://localhost:5601\"")
            .AppendLine(")")
            .AppendLine()
            .AppendLine("& \"" + bulkScriptPath + "\" -ElasticUrl $ElasticUrl")
            .AppendLine("& \"" + kibanaScriptPath + "\" -KibanaUrl $KibanaUrl")
            .ToString();

        await File.WriteAllTextAsync(outputPath, script, cancellationToken).ConfigureAwait(false);
    }
}
