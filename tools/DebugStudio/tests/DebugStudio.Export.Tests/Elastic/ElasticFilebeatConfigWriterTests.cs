#nullable enable

using System.IO;
using DebugStudio.Export.Elastic;

namespace DebugStudio.Export.Tests.Elastic;

/// <summary>
/// Filebeat sample config は運用配布物そのものなので、
/// まずは最低限必要な行が出ることを test-first で固定する。
/// </summary>
public sealed class ElasticFilebeatConfigWriterTests
{
    [Fact]
    public async Task SampleConfigはTelemetryNDJSONの取り込み設定を出力する()
    {
        var artifactLayout = ElasticArtifactLayout.CreateDefault(@"C:\ElasticArtifacts");
        var exportRootDirectory = @"C:\Users\void\Documents\DebugStudio\exports";
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-filebeat-sample-{Guid.NewGuid():N}.yml");

        try
        {
            var writer = new ElasticFilebeatConfigWriter();

            await writer.WriteAsync(outputPath, artifactLayout, exportRootDirectory);

            var yaml = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("filebeat.inputs:", yaml, StringComparison.Ordinal);
            Assert.Contains(@"C:\Users\void\Documents\DebugStudio\exports\telemetry\**\*.ndjson", yaml, StringComparison.Ordinal);
            Assert.Contains(@"C:\Users\void\Documents\DebugStudio\exports\logs\**\*.ndjson", yaml, StringComparison.Ordinal);
            Assert.Contains("pipeline: debugstudio-telemetry", yaml, StringComparison.Ordinal);
            Assert.Contains("pipeline: debugstudio-log", yaml, StringComparison.Ordinal);
            Assert.Contains("hosts: [\"http://localhost:9200\"]", yaml, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
