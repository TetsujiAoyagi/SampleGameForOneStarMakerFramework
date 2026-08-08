#nullable enable

using System.IO;
using System.Text;
using System.Text.Json;
using DebugStudio.Export.Elastic;

namespace DebugStudio.Export.Tests.Elastic;

/// <summary>
/// Elastic 側 artifact の shape を先に固定する。
/// schema alignment の前段として、まずは「どんな template / pipeline を出すか」だけを test で縛る。
/// </summary>
public sealed class ElasticArtifactWriterTests
{
    [Fact]
    public async Task TelemetryIndexTemplateは現在のexportフィールドを受けられるmappingを出力する()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-telemetry-index-template-{Guid.NewGuid():N}.json");

        try
        {
            var writer = new ElasticTelemetryIndexTemplateWriter();

            await writer.WriteAsync(outputPath);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var root = document.RootElement;

            Assert.Equal("debugstudio-telemetry-*", root.GetProperty("index_patterns")[0].GetString());

            var properties = root
                .GetProperty("template")
                .GetProperty("mappings")
                .GetProperty("properties");

            Assert.Equal("date", properties.GetProperty("@timestamp").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("stream").GetProperty("type").GetString());
            Assert.Equal("long", properties.GetProperty("traceId").GetProperty("type").GetString());
            Assert.Equal("long", properties.GetProperty("spanId").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("tags").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("sessionId").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("status").GetProperty("type").GetString());
            Assert.Equal("long", properties.GetProperty("producerSequence").GetProperty("type").GetString());
            Assert.Equal("long", properties.GetProperty("unityFrameAtStart").GetProperty("type").GetString());
            Assert.Equal("long", properties.GetProperty("unityFrameAtEnd").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("event").GetProperty("properties").GetProperty("category").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("event").GetProperty("properties").GetProperty("action").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("trace").GetProperty("properties").GetProperty("id").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("span").GetProperty("properties").GetProperty("id").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("span").GetProperty("properties").GetProperty("parent").GetProperty("properties").GetProperty("id").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("service").GetProperty("properties").GetProperty("name").GetProperty("type").GetString());
            Assert.Equal(
                "debugstudio-telemetry",
                root.GetProperty("template")
                    .GetProperty("settings")
                    .GetProperty("index")
                    .GetProperty("default_pipeline")
                    .GetString());
            Assert.False(
                root.GetProperty("template").GetProperty("settings").TryGetProperty("default_pipeline", out _));
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void TelemetryIndexTemplateのstatusはkeywordである()
    {
        using var document = JsonDocument.Parse(ElasticTelemetryIndexTemplateDefinition.CreateArtifactJson());
        var statusType = document.RootElement
            .GetProperty("template")
            .GetProperty("mappings")
            .GetProperty("properties")
            .GetProperty("status")
            .GetProperty("type")
            .GetString();

        Assert.Equal("keyword", statusType);
    }

    [Fact]
    public async Task TelemetryIngestPipelineは最低限の正規化processorを出力する()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-telemetry-ingest-pipeline-{Guid.NewGuid():N}.json");

        try
        {
            var writer = new ElasticTelemetryIngestPipelineWriter();

            await writer.WriteAsync(outputPath);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var root = document.RootElement;
            var processors = root.GetProperty("processors");

            Assert.True(processors.GetArrayLength() >= 2);
            Assert.Equal("debugstudio telemetry ingest pipeline", root.GetProperty("description").GetString());
            Assert.Equal("telemetry", processors[0].GetProperty("set").GetProperty("value").GetString());
            Assert.Equal("stream", processors[0].GetProperty("set").GetProperty("field").GetString());
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task LogIngestPipelineは最低限の正規化processorを出力する()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-log-ingest-pipeline-{Guid.NewGuid():N}.json");

        try
        {
            var writer = new ElasticLogIngestPipelineWriter();

            await writer.WriteAsync(outputPath);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var root = document.RootElement;
            var processors = root.GetProperty("processors");

            Assert.True(processors.GetArrayLength() >= 2);
            Assert.Equal("debugstudio log ingest pipeline", root.GetProperty("description").GetString());
            Assert.Equal("log", processors[0].GetProperty("set").GetProperty("value").GetString());
            Assert.Equal("stream", processors[0].GetProperty("set").GetProperty("field").GetString());
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task ServiceStatusIndexTemplateは状態表示用フィールドを受けられるmappingを出力する()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-service-status-index-template-{Guid.NewGuid():N}.json");

        try
        {
            var writer = new ElasticServiceStatusIndexTemplateWriter();

            await writer.WriteAsync(outputPath);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var root = document.RootElement;

            Assert.Equal("debugstudio-service-status-*", root.GetProperty("index_patterns")[0].GetString());

            var properties = root
                .GetProperty("template")
                .GetProperty("mappings")
                .GetProperty("properties");

            Assert.Equal("date", properties.GetProperty("@timestamp").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("stream").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("status").GetProperty("type").GetString());
            Assert.Equal("text", properties.GetProperty("message").GetProperty("type").GetString());
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task BulkImportCommandはElasticBulk投入用のPowerShell雛形を出力する()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"import-telemetry-{Guid.NewGuid():N}.ps1");
        var artifactLayout = ElasticArtifactLayout.CreateDefault(@"C:\ElasticArtifacts");

        try
        {
            var writer = new ElasticBulkImportCommandWriter();

            await writer.WriteAsync(outputPath, artifactLayout);

            var script = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("_index_template", script, StringComparison.Ordinal);
            Assert.Contains("_ingest/pipeline", script, StringComparison.Ordinal);
            Assert.Contains("debugstudio-telemetry.bulk.ndjson", script, StringComparison.Ordinal);
            Assert.Contains("debugstudio-log.bulk.ndjson", script, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task KibanaImportCommandはSavedObjectsImportAPIを呼ぶPowerShell雛形を出力する()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"import-kibana-{Guid.NewGuid():N}.ps1");
        var artifactLayout = ElasticArtifactLayout.CreateDefault(@"C:\ElasticArtifacts");

        try
        {
            var writer = new ElasticKibanaImportCommandWriter();

            await writer.WriteAsync(outputPath, artifactLayout);

            var script = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("api/saved_objects/_import", script, StringComparison.Ordinal);
            Assert.Contains("overwrite=true", script, StringComparison.Ordinal);
            Assert.Contains("debugstudio-overview.ndjson", script, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task IngestRunnerCommandはBulk投入とKibanaImportを順番に呼ぶ()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"invoke-ingest-{Guid.NewGuid():N}.ps1");
        var artifactLayout = ElasticArtifactLayout.CreateDefault(@"C:\ElasticArtifacts");

        try
        {
            var writer = new ElasticIngestRunnerCommandWriter();

            await writer.WriteAsync(outputPath, artifactLayout);

            var script = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("import-telemetry.ps1", script, StringComparison.Ordinal);
            Assert.Contains("import-kibana.ps1", script, StringComparison.Ordinal);
            Assert.True(
                script.IndexOf("import-telemetry.ps1", StringComparison.Ordinal) <
                script.IndexOf("import-kibana.ps1", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task KibanaSavedObjectsはOverviewDashboardとSavedSearchを出力する()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-kibana-overview-{Guid.NewGuid():N}.ndjson");

        try
        {
            var writer = new ElasticKibanaSavedObjectsWriter();

            await writer.WriteAsync(outputPath);

            var text = await File.ReadAllTextAsync(outputPath);
            Assert.False(string.IsNullOrWhiteSpace(text));

            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.Equal(5, lines.Length);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task KibanaSavedObjectsの出力先頭3バイトはBOMではない()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-kibana-overview-bom-{Guid.NewGuid():N}.ndjson");

        try
        {
            var writer = new ElasticKibanaSavedObjectsWriter();
            await writer.WriteAsync(outputPath);

            var bytes = await File.ReadAllBytesAsync(outputPath);
            Assert.True(bytes.Length >= 3);
            Assert.False(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task WriteAsyncの出力はReadSavedObjectsNdjsonと完全一致する()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-kibana-overview-match-{Guid.NewGuid():N}.ndjson");

        try
        {
            var writer = new ElasticKibanaSavedObjectsWriter();
            await writer.WriteAsync(outputPath);

            var written = await File.ReadAllTextAsync(outputPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Assert.Equal(ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson(), written);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void ManifestResourceStreamの内容はReadSavedObjectsNdjsonと完全一致する()
    {
        using var stream = typeof(ElasticKibanaSavedObjectsWriter).Assembly
            .GetManifestResourceStream(ElasticKibanaSavedObjectsWriter.ResourceName);
        Assert.NotNull(stream);

        using var reader = new StreamReader(stream!, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var fromStream = reader.ReadToEnd();

        Assert.Equal(ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson(), fromStream);
    }
}
