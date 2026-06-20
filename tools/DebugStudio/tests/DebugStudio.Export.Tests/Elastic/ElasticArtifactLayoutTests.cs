#nullable enable

using DebugStudio.Export.Elastic;

namespace DebugStudio.Export.Tests.Elastic;

/// <summary>
/// Elastic 用 artifact の置き場所と命名規約を先に固定する。
/// writer 本体より前に layout を決めることで、出力物の責務境界をぶらさない。
/// </summary>
public sealed class ElasticArtifactLayoutTests
{
    [Fact]
    public void Filebeat用artifactの既定パスを安定生成する()
    {
        var layout = ElasticArtifactLayout.CreateDefault(@"C:\ElasticArtifacts");

        Assert.Equal(@"C:\ElasticArtifacts\filebeat\debugstudio-filebeat.yml", layout.FilebeatConfigPath);
        Assert.Equal(@"C:\ElasticArtifacts\templates\debugstudio-telemetry.index-template.json", layout.TelemetryIndexTemplatePath);
        Assert.Equal(@"C:\ElasticArtifacts\templates\debugstudio-service-status.index-template.json", layout.ServiceStatusIndexTemplatePath);
        Assert.Equal(@"C:\ElasticArtifacts\templates\debugstudio-log.index-template.json", layout.LogIndexTemplatePath);
        Assert.Equal(@"C:\ElasticArtifacts\pipelines\debugstudio-telemetry.ingest-pipeline.json", layout.TelemetryIngestPipelinePath);
        Assert.Equal(@"C:\ElasticArtifacts\pipelines\debugstudio-log.ingest-pipeline.json", layout.LogIngestPipelinePath);
        Assert.Equal(@"C:\ElasticArtifacts\commands\import-telemetry.ps1", layout.BulkImportCommandPath);
        Assert.Equal(@"C:\ElasticArtifacts\commands\import-kibana.ps1", layout.KibanaImportCommandPath);
        Assert.Equal(@"C:\ElasticArtifacts\commands\invoke-ingest.ps1", layout.IngestRunnerCommandPath);
        Assert.Equal(@"C:\ElasticArtifacts\kibana\debugstudio-overview.ndjson", layout.KibanaSavedObjectsPath);
    }
}
