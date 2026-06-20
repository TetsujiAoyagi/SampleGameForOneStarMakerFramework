#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// Elastic Bulk を手動投入するための PowerShell 雛形を出力する。
/// QA やローカル検証で「まず入るか」を見る補助線として使う。
/// </summary>
public sealed class ElasticBulkImportCommandWriter
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

        var bulkFilePath = Path.Combine(
            Path.GetDirectoryName(outputPath) ?? string.Empty,
            "debugstudio-telemetry.bulk.ndjson");
        var logBulkFilePath = Path.Combine(
            Path.GetDirectoryName(outputPath) ?? string.Empty,
            "debugstudio-log.bulk.ndjson");
        var telemetryTemplateFilePath = artifactLayout.TelemetryIndexTemplatePath;
        var serviceStatusTemplateFilePath = artifactLayout.ServiceStatusIndexTemplatePath;
        var logTemplateFilePath = artifactLayout.LogIndexTemplatePath;
        var telemetryPipelineFilePath = artifactLayout.TelemetryIngestPipelinePath;
        var logPipelineFilePath = artifactLayout.LogIngestPipelinePath;

        var script = new StringBuilder()
            .AppendLine("param(")
            .AppendLine("    [string]$ElasticUrl = \"http://localhost:9200\",")
            .AppendLine("    [string]$BulkFile = \"" + bulkFilePath + "\"")
            .AppendLine(")")
            .AppendLine()
            .AppendLine("$LogBulkFile = \"" + logBulkFilePath + "\"")
            .AppendLine("$TelemetryTemplateFile = \"" + telemetryTemplateFilePath + "\"")
            .AppendLine("$ServiceStatusTemplateFile = \"" + serviceStatusTemplateFilePath + "\"")
            .AppendLine("$LogTemplateFile = \"" + logTemplateFilePath + "\"")
            .AppendLine("$TelemetryPipelineFile = \"" + telemetryPipelineFilePath + "\"")
            .AppendLine("$LogPipelineFile = \"" + logPipelineFilePath + "\"")
            .AppendLine()
            .AppendLine("$telemetryTemplateBody = Get-Content -Raw -Path $TelemetryTemplateFile")
            .AppendLine("Invoke-RestMethod -Method Put -Uri ($ElasticUrl + \"/_index_template/debugstudio-telemetry\") -ContentType \"application/json\" -Body $telemetryTemplateBody")
            .AppendLine("$serviceStatusTemplateBody = Get-Content -Raw -Path $ServiceStatusTemplateFile")
            .AppendLine("Invoke-RestMethod -Method Put -Uri ($ElasticUrl + \"/_index_template/debugstudio-service-status\") -ContentType \"application/json\" -Body $serviceStatusTemplateBody")
            .AppendLine("$logTemplateBody = Get-Content -Raw -Path $LogTemplateFile")
            .AppendLine("Invoke-RestMethod -Method Put -Uri ($ElasticUrl + \"/_index_template/debugstudio-log\") -ContentType \"application/json\" -Body $logTemplateBody")
            .AppendLine("$telemetryPipelineBody = Get-Content -Raw -Path $TelemetryPipelineFile")
            .AppendLine("Invoke-RestMethod -Method Put -Uri ($ElasticUrl + \"/_ingest/pipeline/debugstudio-telemetry\") -ContentType \"application/json\" -Body $telemetryPipelineBody")
            .AppendLine("$logPipelineBody = Get-Content -Raw -Path $LogPipelineFile")
            .AppendLine("Invoke-RestMethod -Method Put -Uri ($ElasticUrl + \"/_ingest/pipeline/debugstudio-log\") -ContentType \"application/json\" -Body $logPipelineBody")
            .AppendLine("$body = Get-Content -Raw -Path $BulkFile")
            .AppendLine("Invoke-RestMethod -Method Post -Uri ($ElasticUrl + \"/_bulk\") -ContentType \"application/x-ndjson\" -Body $body")
            .AppendLine("$logBody = Get-Content -Raw -Path $LogBulkFile")
            .AppendLine("Invoke-RestMethod -Method Post -Uri ($ElasticUrl + \"/_bulk\") -ContentType \"application/x-ndjson\" -Body $logBody")
            .ToString();

        await File.WriteAllTextAsync(outputPath, script, cancellationToken).ConfigureAwait(false);
    }
}
