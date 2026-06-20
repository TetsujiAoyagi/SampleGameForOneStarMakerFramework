#nullable enable

using DebugStudio.Export.Models;
using DebugStudio.Export.Elastic;

namespace DebugStudio.Export.Tests.Elastic;

/// <summary>
/// 既存 export record を壊さずに ECS-lite へ寄せるための変換を先に固定する。
/// ここでは additive migration だけを狙い、元の field 互換性は残す。
/// </summary>
public sealed class ElasticTelemetryDocumentFactoryTests
{
    [Fact]
    public void TelemetryRecordをEcsLite互換documentへ変換できる()
    {
        var record = new TelemetryExportRecord
        {
            TimestampUtc = "2026-04-29T01:00:02.0000000Z",
            TimestampUnixTimeMilliseconds = 1745888402000,
            Stream = "telemetry",
            Source = "debugstudio",
            Name = "load-scene",
            IsSuccess = true,
            TraceId = 10,
            SpanId = 11,
            ParentSpanId = 9,
            Tags = new[] { "CpuTimeOver", "AllocSpike" },
        };

        var document = ElasticTelemetryDocumentFactory.Create(record);

        Assert.Equal(record.TimestampUtc, document.TimestampUtc);
        Assert.Equal("telemetry", document.Stream);
        Assert.Equal("load-scene", document.Name);
        Assert.Equal("debugstudio", document.Service.Name);
        Assert.Equal("telemetry", document.Event.Category);
        Assert.Equal("load-scene", document.Event.Action);
        Assert.Equal("10", document.Trace.Id);
        Assert.Equal("11", document.Span.Id);
        Assert.Equal("9", document.Span.ParentId);
        Assert.Equal(record.Tags, document.Tags);
    }
}
