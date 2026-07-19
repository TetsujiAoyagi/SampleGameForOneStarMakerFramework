#nullable enable

using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Contracts.Schema;
using DebugStudio.Export.Elastic;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.App.Tests;

/// <summary>
/// 受信 envelope → store → export record まで相関 field が落ちないことを検証する。
/// </summary>
public sealed class CorrelationExportMapperTests
{
    [Fact]
    public void LogRecord_FromEnvelope_相関fieldを保持する()
    {
        var envelope = new LogEnvelopeV1
        {
            ApplicationName = "Game",
            TimestampUnixTimeMilliseconds = 1000,
            Category = "Cat",
            LogLevel = 2,
            Message = "msg",
            ThreadId = 1,
            SessionId = "sess-1",
            ProducerSequence = 9,
            UnityFrameAtEmit = 33,
            TraceId = 100,
            SpanId = 200,
        };

        var record = LogRecord.FromEnvelope(sequenceNumber: 1, envelope);

        Assert.Equal("sess-1", record.SessionId);
        Assert.Equal(9, record.ProducerSequence);
        Assert.Equal(33, record.UnityFrameAtEmit);
        Assert.Equal(100, record.TraceId);
        Assert.Equal(200, record.SpanId);
    }

    [Fact]
    public void LogExportMapper_NDJSONとElasticBulkが同一相関shapeを出す()
    {
        var record = LogRecord.FromEnvelope(1, new LogEnvelopeV1
        {
            ApplicationName = "Game",
            TimestampUnixTimeMilliseconds = 1000,
            Category = "Cat",
            LogLevel = 2,
            Message = "msg",
            ThreadId = 1,
            SessionId = "sess-2",
            ProducerSequence = 4,
            UnityFrameAtEmit = 10,
            TraceId = 50,
            SpanId = 60,
        });

        var exportRecord = LogRecordExportMapper.ToExportRecord(record);
        var ndjson = NdjsonLogRecordSerializer.Serialize(exportRecord);

        Assert.Contains("\"sessionId\":\"sess-2\"", ndjson, StringComparison.Ordinal);
        Assert.Contains("\"producerSequence\":4", ndjson, StringComparison.Ordinal);
        Assert.Contains("\"unityFrameAtEmit\":10", ndjson, StringComparison.Ordinal);
        Assert.Contains("\"traceId\":50", ndjson, StringComparison.Ordinal);
        Assert.Contains("\"spanId\":60", ndjson, StringComparison.Ordinal);
    }

    [Fact]
    public void TelemetryExportMapper_相関fieldをexportRecordへ写す()
    {
        var telemetry = new DebugTelemetryEnvelopeV1
        {
            Name = "SceneLoad",
            TraceId = 1,
            SpanId = 2,
            EndTimestampUtcTicks = DateTime.UtcNow.Ticks,
            ElapsedMs = 5,
            IsSuccess = true,
            Level = 0,
            SessionId = "sess-t",
            ProducerSequence = 8,
            UnityFrameAtStart = 40,
            UnityFrameAtEnd = 45,
        };

        var exportRecord = TelemetryRecordExportMapper.ToExportRecord(telemetry);

        Assert.Equal("sess-t", exportRecord.SessionId);
        Assert.Equal(8, exportRecord.ProducerSequence);
        Assert.Equal(40, exportRecord.UnityFrameAtStart);
        Assert.Equal(45, exportRecord.UnityFrameAtEnd);
    }

    [Fact]
    public void TelemetryStore_Clone_相関fieldを落とさない()
    {
        var store = new TelemetryStore();
        store.AppendTelemetry(new DebugTelemetryEnvelopeV1
        {
            Name = "Span",
            EndTimestampUtcTicks = DateTime.UtcNow.Ticks,
            SessionId = "sess-clone",
            ProducerSequence = 2,
            UnityFrameAtStart = 1,
            UnityFrameAtEnd = 3,
        });

        var snapshot = store.GetTelemetrySnapshot();
        Assert.Single(snapshot);
        Assert.Equal("sess-clone", snapshot[0].SessionId);
        Assert.Equal(2, snapshot[0].ProducerSequence);
        Assert.Equal(1, snapshot[0].UnityFrameAtStart);
        Assert.Equal(3, snapshot[0].UnityFrameAtEnd);
    }

    [Fact]
    public void ElasticBulkTelemetry_相関fieldをpayloadへ含める()
    {
        var records = new[]
        {
            new TelemetryExportRecord
            {
                TimestampUtc = "2026-07-19T00:00:00.0000000Z",
                TimestampUnixTimeMilliseconds = 1,
                Stream = "telemetry",
                Name = "Span",
                SessionId = "sess-bulk",
                ProducerSequence = 6,
                UnityFrameAtStart = 10,
                UnityFrameAtEnd = 12,
            }
        };

        var payloadText = System.Text.Encoding.UTF8.GetString(
            ElasticBulkTelemetryNdjsonBuilder.BuildBulkPayload(records));

        Assert.Contains("\"sessionId\":\"sess-bulk\"", payloadText, StringComparison.Ordinal);
        Assert.Contains("\"producerSequence\":6", payloadText, StringComparison.Ordinal);
        Assert.Contains("\"unityFrameAtStart\":10", payloadText, StringComparison.Ordinal);
        Assert.Contains("\"unityFrameAtEnd\":12", payloadText, StringComparison.Ordinal);
    }

    [Fact]
    public void CorrelationFixture_sessionFrameSequenceでLogとSpanを突合できる()
    {
        const string sessionId = "fixture-session";
        const int frame = 100;

        var telemetryEnvelope = new DebugTelemetryEnvelopeV1
        {
            Name = "MultiFrameSpan",
            EndTimestampUtcTicks = DateTime.UtcNow.Ticks,
            ElapsedMs = 16.6,
            IsSuccess = true,
            SessionId = sessionId,
            ProducerSequence = 1,
            UnityFrameAtStart = frame,
            UnityFrameAtEnd = frame + 2,
            TraceId = 9001,
            SpanId = 9002,
        };

        var logEnvelope = new LogEnvelopeV1
        {
            ApplicationName = "Fixture",
            TimestampUnixTimeMilliseconds = 2000,
            Category = "Scene",
            LogLevel = (int)LogEntryKind.Information,
            Message = "during span",
            ThreadId = 1,
            SessionId = sessionId,
            ProducerSequence = 2,
            UnityFrameAtEmit = frame + 1,
            TraceId = 9001,
            SpanId = 9002,
        };

        var workerLogEnvelope = new LogEnvelopeV1
        {
            ApplicationName = "Fixture",
            TimestampUnixTimeMilliseconds = 2001,
            Category = "Worker",
            LogLevel = (int)LogEntryKind.Warning,
            Message = "worker without frame",
            ThreadId = 99,
            SessionId = sessionId,
            ProducerSequence = 3,
            UnityFrameAtEmit = null,
            TraceId = null,
            SpanId = null,
        };

        var spanExport = TelemetryRecordExportMapper.ToExportRecord(telemetryEnvelope);
        var mainLogExport = LogRecordExportMapper.ToExportRecord(LogRecord.FromEnvelope(1, logEnvelope));
        var workerLogExport = LogRecordExportMapper.ToExportRecord(LogRecord.FromEnvelope(2, workerLogEnvelope));

        Assert.Equal(sessionId, spanExport.SessionId);
        Assert.Equal(sessionId, mainLogExport.SessionId);
        Assert.True(spanExport.UnityFrameAtStart < spanExport.UnityFrameAtEnd);
        Assert.Equal(frame + 1, mainLogExport.UnityFrameAtEmit);
        Assert.Null(workerLogExport.UnityFrameAtEmit);
        Assert.Equal(9001, mainLogExport.TraceId);
        Assert.Null(workerLogExport.TraceId);
        Assert.True(mainLogExport.ProducerSequence > spanExport.ProducerSequence);
    }
}
