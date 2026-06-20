#nullable enable

using System.IO;
using System.Text.Json;
using DebugStudio.Export.Elastic;

namespace DebugStudio.Export.Tests.Elastic;

/// <summary>
/// log export も telemetry と同じ Elastic 導線へ乗せるため、
/// まずは log 用 index template の shape を test-first で固定する。
/// </summary>
public sealed class ElasticLogIndexTemplateWriterTests
{
    [Fact]
    public async Task LogIndexTemplateは現在のNDJSONログフィールドを受けられるmappingを出力する()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-log-index-template-{Guid.NewGuid():N}.json");

        try
        {
            var writer = new ElasticLogIndexTemplateWriter();

            await writer.WriteAsync(outputPath);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var root = document.RootElement;
            var properties = root
                .GetProperty("template")
                .GetProperty("mappings")
                .GetProperty("properties");

            Assert.Equal("debugstudio-log-*", root.GetProperty("index_patterns")[0].GetString());
            Assert.Equal("date", properties.GetProperty("@timestamp").GetProperty("type").GetString());
            Assert.Equal("long", properties.GetProperty("sequenceNumber").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("applicationName").GetProperty("type").GetString());
            Assert.Equal("long", properties.GetProperty("timestampUnixTimeMilliseconds").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("category").GetProperty("type").GetString());
            Assert.Equal("text", properties.GetProperty("message").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("event").GetProperty("properties").GetProperty("name").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("log").GetProperty("properties").GetProperty("level").GetProperty("type").GetString());
            Assert.Equal("keyword", properties.GetProperty("service").GetProperty("properties").GetProperty("name").GetProperty("type").GetString());
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
