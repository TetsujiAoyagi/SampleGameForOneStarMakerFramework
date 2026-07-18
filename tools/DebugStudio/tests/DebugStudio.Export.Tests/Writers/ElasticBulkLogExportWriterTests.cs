#nullable enable

using System;
using System.IO;
using System.Text.Json;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.Export.Tests.Writers;

/// <summary>
/// log も telemetry と同じ `_bulk` へ流せるようにし、
/// Elastic 側の横断可視化へ繋ぐ。
/// </summary>
public sealed class ElasticBulkLogExportWriterTests
{
    [Fact]
    public async Task WriteAsync_日別logindexへcreateアクションとpayloadを書き出す()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-log-bulk-{Guid.NewGuid():N}.ndjson");
        var records = new[]
        {
            new LogExportRecord
            {
                TimestampUtc = "2026-04-29T01:23:45.000Z",
                SequenceNumber = 42,
                ApplicationName = "GameClient",
                TimestampUnixTimeMilliseconds = DateTimeOffset.Parse("2026-04-29T01:23:45.000Z").ToUnixTimeMilliseconds(),
                TimestampLocal = "2026-04-29 10:23:45.000 +09:00",
                Kind = "Warning",
                RawLogLevel = 3,
                Category = "Network",
                EventId = 7,
                EventName = "PacketTimeout",
                Message = "timeout detected",
                ThreadId = 11,
                LineNumber = 0,
                ServiceName = "GameClient",
                LogLevel = "warning",
            }
        };

        try
        {
            var writer = new ElasticBulkLogExportWriter();

            await writer.WriteAsync(records, outputPath);

            var lines = await File.ReadAllLinesAsync(outputPath);
            Assert.Equal(2, lines.Length);

            using var actionDocument = JsonDocument.Parse(lines[0]);
            using var payloadDocument = JsonDocument.Parse(lines[1]);

            Assert.Equal(
                "debugstudio-log-2026.04.29",
                actionDocument.RootElement.GetProperty("create").GetProperty("_index").GetString());
            Assert.Equal("timeout detected", payloadDocument.RootElement.GetProperty("message").GetString());
            Assert.Equal(
                "2026-04-29T01:23:45.000Z",
                payloadDocument.RootElement.GetProperty("@timestamp").GetString());
            Assert.Equal("warning", payloadDocument.RootElement.GetProperty("log").GetProperty("level").GetString());
            Assert.Equal("GameClient", payloadDocument.RootElement.GetProperty("service").GetProperty("name").GetString());
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
