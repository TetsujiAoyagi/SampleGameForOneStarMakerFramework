#nullable enable

using System.IO;
using DebugStudio.Export.Elastic;

namespace DebugStudio.Export.Tests.Elastic;

/// <summary>
/// Filebeat sample config は運用配布物そのものなので、
/// L0 flat NDJSON の復元・routing と secret 非含有を test-first で固定する。
/// </summary>
public sealed class ElasticFilebeatConfigWriterTests
{
    [Fact]
    public async Task SampleConfigはL0永続化のflatNDJSONを監視する()
    {
        var artifactLayout = ElasticArtifactLayout.CreateDefault(@"C:\ElasticArtifacts");
        var inputRootDirectory = @"C:\Users\void\AppData\Local\DebugStudio";
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-filebeat-sample-{Guid.NewGuid():N}.yml");

        try
        {
            var writer = new ElasticFilebeatConfigWriter();

            await writer.WriteAsync(outputPath, artifactLayout, inputRootDirectory);

            var yaml = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("filebeat.inputs:", yaml, StringComparison.Ordinal);
            Assert.Contains(@"C:\Users\void\AppData\Local\DebugStudio\telemetry\*.ndjson", yaml, StringComparison.Ordinal);
            Assert.Contains(@"C:\Users\void\AppData\Local\DebugStudio\logs\*.ndjson", yaml, StringComparison.Ordinal);
            Assert.Contains("parsers:", yaml, StringComparison.Ordinal);
            Assert.Contains("- ndjson:", yaml, StringComparison.Ordinal);
            Assert.Contains("target: \"\"", yaml, StringComparison.Ordinal);
            Assert.Contains("overwrite_keys: true", yaml, StringComparison.Ordinal);
            Assert.Contains("add_error_key: true", yaml, StringComparison.Ordinal);
            Assert.Contains("pipeline: debugstudio-telemetry", yaml, StringComparison.Ordinal);
            Assert.Contains("pipeline: debugstudio-log", yaml, StringComparison.Ordinal);
            Assert.Contains("debugstudio.route: telemetry", yaml, StringComparison.Ordinal);
            Assert.Contains("debugstudio.route: log", yaml, StringComparison.Ordinal);
            Assert.Contains("hosts: [\"http://localhost:9200\"]", yaml, StringComparison.Ordinal);
            Assert.Contains("index: \"debugstudio-telemetry-%{+yyyy.MM.dd}\"", yaml, StringComparison.Ordinal);
            Assert.Contains("index: \"debugstudio-log-%{+yyyy.MM.dd}\"", yaml, StringComparison.Ordinal);
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
    public async Task SampleConfigはAPIキー値を含まず日本語で秘密注入手順を示す()
    {
        var artifactLayout = ElasticArtifactLayout.CreateDefault(@"C:\ElasticArtifacts");
        var inputRootDirectory = @"C:\Users\void\AppData\Local\DebugStudio";
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-filebeat-sample-{Guid.NewGuid():N}.yml");

        try
        {
            var writer = new ElasticFilebeatConfigWriter();

            await writer.WriteAsync(outputPath, artifactLayout, inputRootDirectory);

            var yaml = await File.ReadAllTextAsync(outputPath);
            Assert.DoesNotContain("Authorization:", yaml, StringComparison.Ordinal);
            Assert.DoesNotContain("api_key:", yaml, StringComparison.Ordinal);
            Assert.Contains("秘密", yaml, StringComparison.Ordinal);
            Assert.Contains("api_key", yaml, StringComparison.OrdinalIgnoreCase);
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
