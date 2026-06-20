#nullable enable

using System;
using System.Globalization;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// 既存 telemetry export record から ECS-lite 互換の document を組み立てる。
/// schema alignment は段階導入なので、まずは additive に event / trace / span / service を足す。
/// </summary>
public static class ElasticTelemetryDocumentFactory
{
    public static ElasticTelemetryDocument Create(TelemetryExportRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new ElasticTelemetryDocument
        {
            TimestampUtc = record.TimestampUtc,
            TimestampUnixTimeMilliseconds = record.TimestampUnixTimeMilliseconds,
            Stream = record.Stream,
            Source = record.Source,
            Name = record.Name,
            IsSuccess = record.IsSuccess,
            TraceId = record.TraceId,
            SpanId = record.SpanId,
            ParentSpanId = record.ParentSpanId,
            Tags = record.Tags,
            Event = new ElasticTelemetryEvent
            {
                Category = record.Stream,
                Action = record.Name,
            },
            Trace = new ElasticTelemetryTrace
            {
                Id = FormatNullableInt64(record.TraceId),
            },
            Span = new ElasticTelemetrySpan
            {
                Id = FormatNullableInt64(record.SpanId),
                ParentId = FormatNullableInt64(record.ParentSpanId),
            },
            Service = new ElasticTelemetryService
            {
                Name = record.Source,
            }
        };
    }

    private static string? FormatNullableInt64(long? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture);
    }
}
