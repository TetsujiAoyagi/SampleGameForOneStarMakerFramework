#nullable enable

using System.Text;
using System.Text.Json;
using DebugStudio.Export.Elastic;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Tests.Elastic;

/// <summary>
/// セッション属性 5 keyword が index template / `_bulk` NDJSON に載ることを固定する。
/// </summary>
public sealed class TelemetrySessionAttributesElasticTests
{
    [Fact]
    public void ElasticTelemetryIndexTemplate_5keywordが含まれる()
    {
        using var document = JsonDocument.Parse(ElasticTelemetryIndexTemplateDefinition.CreateBootstrapJson());
        var properties = document.RootElement
            .GetProperty("template")
            .GetProperty("mappings")
            .GetProperty("properties");

        Assert.Equal("keyword", properties.GetProperty("buildVersion").GetProperty("type").GetString());
        Assert.Equal("keyword", properties.GetProperty("platform").GetProperty("type").GetString());
        Assert.Equal("keyword", properties.GetProperty("deviceModel").GetProperty("type").GetString());
        Assert.Equal("keyword", properties.GetProperty("osVersion").GetProperty("type").GetString());
        Assert.Equal("keyword", properties.GetProperty("engineVersion").GetProperty("type").GetString());
    }

    [Fact]
    public void ElasticBulkTelemetryNdjsonBuilder_属性ありで5キーが_bulk_NDJSONに出る()
    {
        var records = new[]
        {
            new TelemetryExportRecord
            {
                TimestampUtc = "2026-08-08T00:00:00.0000000Z",
                TimestampUnixTimeMilliseconds = 1,
                Stream = "telemetry",
                Name = "Span",
                SessionId = "sess-bulk-attrs",
                BuildVersion = "1.4.2",
                Platform = "WindowsPlayer",
                DeviceModel = "PC",
                OsVersion = "Windows 11",
                EngineVersion = "6000.5.0f1",
            },
        };

        var payloadText = Encoding.UTF8.GetString(ElasticBulkTelemetryNdjsonBuilder.BuildBulkPayload(records));

        Assert.Contains("\"buildVersion\":\"1.4.2\"", payloadText, StringComparison.Ordinal);
        Assert.Contains("\"platform\":\"WindowsPlayer\"", payloadText, StringComparison.Ordinal);
        Assert.Contains("\"deviceModel\":\"PC\"", payloadText, StringComparison.Ordinal);
        Assert.Contains("\"osVersion\":\"Windows 11\"", payloadText, StringComparison.Ordinal);
        Assert.Contains("\"engineVersion\":\"6000.5.0f1\"", payloadText, StringComparison.Ordinal);
    }

    [Fact]
    public void ElasticBulkTelemetryNdjsonBuilder_属性nullのときキー自体が出ない()
    {
        var records = new[]
        {
            new TelemetryExportRecord
            {
                TimestampUtc = "2026-08-08T00:00:00.0000000Z",
                TimestampUnixTimeMilliseconds = 1,
                Stream = "telemetry",
                Name = "Span",
                SessionId = "sess-bulk-no-attrs",
            },
        };

        var payloadText = Encoding.UTF8.GetString(ElasticBulkTelemetryNdjsonBuilder.BuildBulkPayload(records));

        Assert.DoesNotContain("buildVersion", payloadText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"platform\"", payloadText, StringComparison.Ordinal);
        Assert.DoesNotContain("deviceModel", payloadText, StringComparison.Ordinal);
        Assert.DoesNotContain("osVersion", payloadText, StringComparison.Ordinal);
        Assert.DoesNotContain("engineVersion", payloadText, StringComparison.Ordinal);
    }
}
