#nullable enable

using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Export.Elastic.Kibana;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// Elastic operator 向け artifact 一式をまとめて出力する。
/// </summary>
public sealed class ElasticArtifactBundleWriter
{
    public async Task<ElasticArtifactBundle> WriteAsync(
        string outputRootDirectory,
        string inputRootDirectory,
        CancellationToken cancellationToken = default)
    {
        var layout = ElasticArtifactLayout.CreateDefault(outputRootDirectory);

        await new ElasticTelemetryIndexTemplateWriter().WriteAsync(layout.TelemetryIndexTemplatePath, cancellationToken).ConfigureAwait(false);
        await new ElasticServiceStatusIndexTemplateWriter().WriteAsync(layout.ServiceStatusIndexTemplatePath, cancellationToken).ConfigureAwait(false);
        await new ElasticLogIndexTemplateWriter().WriteAsync(layout.LogIndexTemplatePath, cancellationToken).ConfigureAwait(false);
        await new ElasticTelemetryIngestPipelineWriter().WriteAsync(layout.TelemetryIngestPipelinePath, cancellationToken).ConfigureAwait(false);
        await new ElasticLogIngestPipelineWriter().WriteAsync(layout.LogIngestPipelinePath, cancellationToken).ConfigureAwait(false);
        await new ElasticFilebeatConfigWriter().WriteAsync(layout.FilebeatConfigPath, layout, inputRootDirectory, cancellationToken).ConfigureAwait(false);
        await new ElasticBulkImportCommandWriter().WriteAsync(layout.BulkImportCommandPath, layout, cancellationToken).ConfigureAwait(false);
        await new ElasticKibanaSavedObjectsWriter().WriteAsync(layout.KibanaSavedObjectsPath, cancellationToken).ConfigureAwait(false);
        await new ElasticKibanaImportCommandWriter().WriteAsync(layout.KibanaImportCommandPath, layout, cancellationToken).ConfigureAwait(false);
        await new ElasticIngestRunnerCommandWriter().WriteAsync(layout.IngestRunnerCommandPath, layout, cancellationToken).ConfigureAwait(false);

        return new ElasticArtifactBundle
        {
            Layout = layout,
        };
    }
}
