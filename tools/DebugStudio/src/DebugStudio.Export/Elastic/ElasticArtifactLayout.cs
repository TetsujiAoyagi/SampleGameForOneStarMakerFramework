#nullable enable

using System;
using System.IO;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// Elastic 運用向け artifact の既定配置を表す。
/// まずは path 生成だけを責務に絞り、writer 本体や template 本体の生成とは分けて育てる。
/// </summary>
public sealed class ElasticArtifactLayout
{
    private ElasticArtifactLayout(
        string filebeatConfigPath,
        string telemetryIndexTemplatePath,
        string serviceStatusIndexTemplatePath,
        string logIndexTemplatePath,
        string telemetryIngestPipelinePath,
        string logIngestPipelinePath,
        string bulkImportCommandPath,
        string kibanaImportCommandPath,
        string ingestRunnerCommandPath,
        string kibanaSavedObjectsPath)
    {
        FilebeatConfigPath = filebeatConfigPath;
        TelemetryIndexTemplatePath = telemetryIndexTemplatePath;
        ServiceStatusIndexTemplatePath = serviceStatusIndexTemplatePath;
        LogIndexTemplatePath = logIndexTemplatePath;
        TelemetryIngestPipelinePath = telemetryIngestPipelinePath;
        LogIngestPipelinePath = logIngestPipelinePath;
        BulkImportCommandPath = bulkImportCommandPath;
        KibanaImportCommandPath = kibanaImportCommandPath;
        IngestRunnerCommandPath = ingestRunnerCommandPath;
        KibanaSavedObjectsPath = kibanaSavedObjectsPath;
    }

    public string FilebeatConfigPath { get; }

    public string TelemetryIndexTemplatePath { get; }

    public string ServiceStatusIndexTemplatePath { get; }

    public string LogIndexTemplatePath { get; }

    public string TelemetryIngestPipelinePath { get; }

    public string LogIngestPipelinePath { get; }

    public string BulkImportCommandPath { get; }

    public string KibanaImportCommandPath { get; }

    public string IngestRunnerCommandPath { get; }

    public string KibanaSavedObjectsPath { get; }

    public static ElasticArtifactLayout CreateDefault(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("A root directory is required.", nameof(rootDirectory));
        }

        return new ElasticArtifactLayout(
            filebeatConfigPath: Path.Combine(rootDirectory, "filebeat", "debugstudio-filebeat.yml"),
            telemetryIndexTemplatePath: Path.Combine(rootDirectory, "templates", "debugstudio-telemetry.index-template.json"),
            serviceStatusIndexTemplatePath: Path.Combine(rootDirectory, "templates", "debugstudio-service-status.index-template.json"),
            logIndexTemplatePath: Path.Combine(rootDirectory, "templates", "debugstudio-log.index-template.json"),
            telemetryIngestPipelinePath: Path.Combine(rootDirectory, "pipelines", "debugstudio-telemetry.ingest-pipeline.json"),
            logIngestPipelinePath: Path.Combine(rootDirectory, "pipelines", "debugstudio-log.ingest-pipeline.json"),
            bulkImportCommandPath: Path.Combine(rootDirectory, "commands", "import-telemetry.ps1"),
            kibanaImportCommandPath: Path.Combine(rootDirectory, "commands", "import-kibana.ps1"),
            ingestRunnerCommandPath: Path.Combine(rootDirectory, "commands", "invoke-ingest.ps1"),
            kibanaSavedObjectsPath: Path.Combine(rootDirectory, "kibana", "debugstudio-overview.ndjson"));
    }
}
