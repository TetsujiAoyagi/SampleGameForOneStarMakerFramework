#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.App.Tests.Services;

/// <summary>
/// LogExportService が表示条件と同じ条件で export できることを検証する。
/// </summary>
public sealed class LogExportServiceTests
{
    [Fact]
    public async Task ExportAsync_FilterCriteriaで絞り込んだ結果だけを書き出す()
    {
        var store = new LogStore(capacity: 32);
        store.Append(CreateLogEnvelope("Network connected", "Network"));
        store.Append(CreateLogEnvelope("UI clicked", "UI"));
        store.Append(CreateLogEnvelope("Network disconnected", "Network"));

        var writer = new RecordingLogExportWriter();
        var service = new LogExportService(store, new LogQueryService(), writer);

        await service.ExportAsync(
            "dummy.ndjson",
            new LogFilterCriteria
            {
                CategoryTags = ["Network"],
            });

        Assert.Equal(2, writer.LastLogs.Count);
        Assert.All(writer.LastLogs, log => Assert.Equal("Network", log.Category));
    }

    [Fact]
    public async Task ExportAsync_CsvFormatでCsvWriterを選択できる()
    {
        var store = new LogStore(capacity: 32);
        store.Append(CreateLogEnvelope("Network connected", "Network"));

        var ndjsonWriter = new RecordingLogExportWriter(LogExportFormat.Ndjson);
        var csvWriter = new RecordingLogExportWriter(LogExportFormat.Csv);
        var service = new LogExportService(store, new LogQueryService(), new ILogExportWriter[] { ndjsonWriter, csvWriter });

        await service.ExportAsync("dummy.csv", LogFilterCriteria.CreateEmpty(), LogExportFormat.Csv);

        Assert.Empty(ndjsonWriter.LastLogs);
        Assert.Single(csvWriter.LastLogs);
    }

    [Fact]
    public async Task ExportAsync_ElasticBulkFormatでBulkWriterを選択できる()
    {
        var store = new LogStore(capacity: 32);
        store.Append(CreateLogEnvelope("Network connected", "Network"));

        var ndjsonWriter = new RecordingLogExportWriter(LogExportFormat.Ndjson);
        var bulkWriter = new RecordingLogExportWriter(LogExportFormat.ElasticBulk);
        var service = new LogExportService(store, new LogQueryService(), new ILogExportWriter[] { ndjsonWriter, bulkWriter });

        await service.ExportAsync("dummy.bulk.ndjson", LogFilterCriteria.CreateEmpty(), LogExportFormat.ElasticBulk);

        Assert.Empty(ndjsonWriter.LastLogs);
        Assert.Single(bulkWriter.LastLogs);
    }

    [Fact]
    public async Task CsvLogExportWriter_カンマと引用符をエスケープして書き出す()
    {
        var writer = new CsvLogExportWriter();
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        var logs = new[]
        {
            new LogExportRecord
            {
                TimestampUtc = "2026-04-29T01:23:45.000Z",
                SequenceNumber = 1,
                ApplicationName = "TestApp",
                TimestampUnixTimeMilliseconds = DateTimeOffset.Parse("2026-04-29T01:23:45.000Z").ToUnixTimeMilliseconds(),
                TimestampLocal = "2026-04-29 10:23:45.000 +09:00",
                Kind = "Information",
                RawLogLevel = 2,
                Category = "Network",
                EventId = 0,
                Message = "value,\"quoted\"",
                ThreadId = Environment.CurrentManagedThreadId,
                LineNumber = 0,
            }
        };

        try
        {
            await writer.WriteAsync(logs, outputPath);

            var lines = await File.ReadAllLinesAsync(outputPath);
            Assert.True(lines.Length >= 2);
            Assert.Contains("sequenceNumber,applicationName", lines[0], StringComparison.Ordinal);
            Assert.Contains(",\"value,\"\"quoted\"\"\",", lines[1], StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static LogEnvelopeV1 CreateLogEnvelope(string message, string category)
    {
        return new LogEnvelopeV1
        {
            SchemaVersion = 1,
            ApplicationName = "TestApp",
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Category = category,
            LogLevel = 2,
            EventId = 0,
            Message = message,
            ThreadId = Environment.CurrentManagedThreadId,
        };
    }

    private sealed class RecordingLogExportWriter : ILogExportWriter
    {
        public RecordingLogExportWriter(LogExportFormat format = LogExportFormat.Ndjson)
        {
            Format = format;
        }

        public LogExportFormat Format { get; }

        public IReadOnlyList<LogExportRecord> LastLogs { get; private set; } = Array.Empty<LogExportRecord>();

        public Task WriteAsync(IReadOnlyList<LogExportRecord> logs, string outputPath, CancellationToken cancellationToken = default)
        {
            LastLogs = logs;
            return Task.CompletedTask;
        }
    }
}
