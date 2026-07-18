#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Contracts.Schema;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.App.Tests.Services;

/// <summary>
/// telemetry export の R3 基盤を検証する。
/// normalized record 生成、timestamp 順ソート、telemetry/service-status の統合を固定する。
/// </summary>
public sealed class TelemetryExportServiceTests
{
    [Fact]
    public async Task ExportAsync_telemetryとserviceStatusを統合して時系列順に流す()
    {
        var store = new TelemetryStore(retainedCapacity: 8);
        store.AppendTelemetry(new DebugTelemetryEnvelopeV1
        {
            Name = "load-scene",
            EndTimestampUtcTicks = new DateTime(2026, 4, 29, 1, 0, 2, DateTimeKind.Utc).Ticks,
            ElapsedMs = 12.5,
            IsSuccess = true,
            TraceId = 10,
            SpanId = 11,
            TagBits = (int)(DebugTelemetryTagBits.CpuTimeOver | DebugTelemetryTagBits.AllocSpike),
        });
        store.AppendServiceStatus(new DebugSocketServiceStatusEnvelopeV1
        {
            Status = "running",
            Message = "steady",
            TimestampUnixTimeMilliseconds = new DateTimeOffset(2026, 4, 29, 1, 0, 1, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        });

        var writer = new RecordingTelemetryExportWriter();
        var service = new TelemetryExportService(store, writer);

        await service.ExportAsync(@"C:\exports\telemetry.ndjson");

        Assert.Equal(@"C:\exports\telemetry.ndjson", writer.LastOutputPath);
        Assert.Equal(2, writer.LastRecords.Count);
        Assert.Equal("serviceStatus", writer.LastRecords[0].Stream);
        Assert.Equal("telemetry", writer.LastRecords[1].Stream);
        Assert.Equal("running", writer.LastRecords[0].Status);
        Assert.Equal("load-scene", writer.LastRecords[1].Name);
        Assert.NotNull(writer.LastRecords[1].TimestampUtc);
        Assert.Equal("debugstudio", writer.LastRecords[1].Source);
        Assert.Equal((int)(DebugTelemetryTagBits.CpuTimeOver | DebugTelemetryTagBits.AllocSpike), writer.LastRecords[1].TagBits);
        Assert.Equal(new[] { "CpuTimeOver", "AllocSpike" }, writer.LastRecords[1].Tags);
    }

    [Fact]
    public async Task ExportAsync_cameraFieldsをexportRecordへ写す()
    {
        var store = new TelemetryStore(retainedCapacity: 8);
        store.AppendTelemetry(new DebugTelemetryEnvelopeV1
        {
            Name = "CameraSystemSnapshot",
            EndTimestampUtcTicks = new DateTime(2026, 4, 29, 1, 0, 2, DateTimeKind.Utc).Ticks,
            CameraTotalViewCount = 3,
            CameraAdditionalViewCount = 2,
            CameraBlendingViewCount = 1,
            CameraMaxStackDepthTotal = 4,
            CameraViewId = 5,
            CameraActiveCameraHash = 6,
        });

        var writer = new RecordingTelemetryExportWriter();
        var service = new TelemetryExportService(store, writer);

        await service.ExportAsync(@"C:\exports\telemetry.ndjson");

        var record = Assert.Single(writer.LastRecords);
        Assert.Equal(3, record.CameraTotalViewCount);
        Assert.Equal(2, record.CameraAdditionalViewCount);
        Assert.Equal(1, record.CameraBlendingViewCount);
        Assert.Equal(4, record.CameraMaxStackDepthTotal);
        Assert.Equal(5, record.CameraViewId);
        Assert.Equal(6, record.CameraActiveCameraHash);
    }

    [Fact]
    public async Task ExportAsync_ElasticBulk指定時は対応writerへ委譲する()
    {
        var store = new TelemetryStore(retainedCapacity: 8);
        store.AppendTelemetry(new DebugTelemetryEnvelopeV1
        {
            Name = "boot",
            EndTimestampUtcTicks = new DateTime(2026, 4, 29, 1, 0, 2, DateTimeKind.Utc).Ticks,
            ElapsedMs = 12.5,
            IsSuccess = true,
        });

        var ndjsonWriter = new RecordingTelemetryExportWriter(TelemetryExportFormat.Ndjson);
        var bulkWriter = new RecordingTelemetryExportWriter(TelemetryExportFormat.ElasticBulk);
        var service = new TelemetryExportService(store, new ITelemetryExportWriter[] { ndjsonWriter, bulkWriter });

        await service.ExportAsync(@"C:\exports\telemetry.bulk.ndjson", TelemetryExportFormat.ElasticBulk);

        Assert.Empty(ndjsonWriter.LastRecords);
        Assert.Single(bulkWriter.LastRecords);
        Assert.Equal(@"C:\exports\telemetry.bulk.ndjson", bulkWriter.LastOutputPath);
    }

    [Fact]
    public void TelemetryExportPathPolicy_telemetry用ディレクトリへtimestamp付きファイルを作る()
    {
        var policy = new TelemetryExportPathPolicy(@"C:\TelemetryRoot");
        var now = new DateTimeOffset(2026, 4, 29, 10, 55, 30, TimeSpan.FromHours(9));

        var path = policy.CreateDefaultPath(now: now);

        Assert.Equal(
            @"C:\TelemetryRoot\telemetry\2026-04-29\debugstudio-telemetry-20260429-105530.ndjson",
            path);
    }

    [Fact]
    public async Task ElasticBulkTelemetryExportWriter_BulkAction行とPayload行を交互に書く()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"debugstudio-telemetry-bulk-test-{Guid.NewGuid():N}.ndjson");
        try
        {
            var writer = new ElasticBulkTelemetryExportWriter();
            await writer.WriteAsync(
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
                ],
                outputPath);

            var lines = await File.ReadAllLinesAsync(outputPath);
            Assert.Equal(4, lines.Length);
            Assert.Contains("\"create\":{\"_index\":\"debugstudio-telemetry-2026.04.29\"}", lines[0], StringComparison.OrdinalIgnoreCase);
            using var telemetryPayload = JsonDocument.Parse(lines[1]);
            Assert.Equal("2026-04-29T01:00:02.0000000Z", telemetryPayload.RootElement.GetProperty("@timestamp").GetString());
            Assert.Equal("telemetry", telemetryPayload.RootElement.GetProperty("stream").GetString());
            Assert.Equal("telemetry", telemetryPayload.RootElement.GetProperty("event").GetProperty("category").GetString());
            Assert.Equal("boot", telemetryPayload.RootElement.GetProperty("event").GetProperty("action").GetString());
            Assert.Equal("10", telemetryPayload.RootElement.GetProperty("trace").GetProperty("id").GetString());
            Assert.Equal("11", telemetryPayload.RootElement.GetProperty("span").GetProperty("id").GetString());
            Assert.Equal("9", telemetryPayload.RootElement.GetProperty("span").GetProperty("parent").GetProperty("id").GetString());
            Assert.Equal("debugstudio", telemetryPayload.RootElement.GetProperty("service").GetProperty("name").GetString());
            Assert.Contains("\"create\":{\"_index\":\"debugstudio-service-status-2026.04.29\"}", lines[2], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"stream\":\"serviceStatus\"", lines[3], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private sealed class RecordingTelemetryExportWriter : ITelemetryExportWriter
    {
        public RecordingTelemetryExportWriter(TelemetryExportFormat format = TelemetryExportFormat.Ndjson)
        {
            Format = format;
        }

        public TelemetryExportFormat Format { get; }

        public string LastOutputPath { get; private set; } = string.Empty;

        public IReadOnlyList<TelemetryExportRecord> LastRecords { get; private set; } = Array.Empty<TelemetryExportRecord>();

        public Task WriteAsync(IReadOnlyList<TelemetryExportRecord> records, string outputPath, CancellationToken cancellationToken = default)
        {
            LastOutputPath = outputPath;
            LastRecords = records;
            return Task.CompletedTask;
        }
    }
}
