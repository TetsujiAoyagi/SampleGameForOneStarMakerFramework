#nullable enable

using System;
using System.IO;
using System.Text;
using DebugStudio.Export.Elastic;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.Export.Tests.Elastic;

/// <summary>
/// file export と HTTP `_bulk` が同一 NDJSON を使うことを byte レベルで固定する。
/// </summary>
public sealed class ElasticBulkTelemetryNdjsonBuilderTests
{
    [Fact]
    public async Task BuildBulkPayload_ファイルexportとbyte完全一致する()
    {
        var records = CreateSampleRecords();
        var builderPayload = ElasticBulkTelemetryNdjsonBuilder.BuildBulkPayload(records);

        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-bulk-builder-{Guid.NewGuid():N}.ndjson");
        try
        {
            var writer = new ElasticBulkTelemetryExportWriter();
            await writer.WriteAsync(records, outputPath);

            var filePayload = await File.ReadAllBytesAsync(outputPath);
            Assert.Equal(builderPayload, filePayload);
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
    public void BuildBulkPayload_UTF8とapplication_x_ndjson向け終端改行を付ける()
    {
        var payload = ElasticBulkTelemetryNdjsonBuilder.BuildBulkPayload(CreateSampleRecords());
        var text = Encoding.UTF8.GetString(payload);

        Assert.EndsWith(Environment.NewLine, text, StringComparison.Ordinal);
        Assert.True(payload.Length > 0);
    }

    private static TelemetryExportRecord[] CreateSampleRecords()
    {
        return
        [
            new TelemetryExportRecord
            {
                TimestampUtc = "2026-04-29T01:00:02.0000000Z",
                TimestampUnixTimeMilliseconds = new DateTimeOffset(2026, 4, 29, 1, 0, 2, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                Stream = "telemetry",
                Source = "debugstudio",
                Name = "boot",
                TraceId = 10,
                SpanId = 11,
                ParentSpanId = 9,
            },
            new TelemetryExportRecord
            {
                TimestampUtc = "2026-04-29T01:00:03.0000000Z",
                TimestampUnixTimeMilliseconds = new DateTimeOffset(2026, 4, 29, 1, 0, 3, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                Stream = "serviceStatus",
                Source = "debugstudio",
                Status = "running",
            },
        ];
    }
}
