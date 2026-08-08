#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic.Kibana;

/// <summary>
/// Kibana saved objects import 用の PowerShell 雛形を出力する。
/// </summary>
public sealed class ElasticKibanaImportCommandWriter
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

        var script = new StringBuilder()
            .AppendLine("param(")
            .AppendLine("    [string]$KibanaUrl = \"http://localhost:5601\",")
            .AppendLine("    [string]$SavedObjectsFile = \"" + artifactLayout.KibanaSavedObjectsPath + "\"")
            .AppendLine(")")
            .AppendLine()
            .AppendLine("$form = @{ file = Get-Item -Path $SavedObjectsFile }")
            .AppendLine("Invoke-RestMethod -Method Post -Uri ($KibanaUrl + \"/api/saved_objects/_import?overwrite=true\") -Headers @{\"kbn-xsrf\"=\"true\"} -Form $form")
            .ToString();

        await File.WriteAllTextAsync(outputPath, script, cancellationToken).ConfigureAwait(false);
    }
}
