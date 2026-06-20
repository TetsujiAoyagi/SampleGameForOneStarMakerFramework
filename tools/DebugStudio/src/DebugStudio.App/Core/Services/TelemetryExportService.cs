#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Formatting;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// retained telemetry / service status を normalized export record へ変換して永続化する app service。
///
/// <para>
/// UI は path と export 実行だけを調停し、schema 正規化や stream 統合はこの service に閉じ込める。
/// これにより、DebugStudio 上の表示都合と Elastic-ready な export contract を分離できる。
/// </para>
/// </summary>
public sealed class TelemetryExportService
{
    private readonly TelemetryStore _telemetryStore;
    private readonly IReadOnlyDictionary<TelemetryExportFormat, ITelemetryExportWriter> _writers;

    public TelemetryExportService(TelemetryStore telemetryStore, ITelemetryExportWriter writer)
        : this(telemetryStore, [writer])
    {
    }

    public TelemetryExportService(TelemetryStore telemetryStore, IEnumerable<ITelemetryExportWriter> writers)
    {
        _telemetryStore = telemetryStore ?? throw new ArgumentNullException(nameof(telemetryStore));

        ArgumentNullException.ThrowIfNull(writers);
        _writers = writers.ToDictionary(static writer => writer.Format);
        if (_writers.Count == 0)
        {
            throw new ArgumentException("At least one telemetry export writer is required.", nameof(writers));
        }
    }

    public Task ExportAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        return ExportAsync(outputPath, TelemetryExportFormat.Ndjson, cancellationToken);
    }

    public Task ExportAsync(string outputPath, TelemetryExportFormat format, CancellationToken cancellationToken = default)
    {
        var retainedSnapshot = _telemetryStore.GetRetainedSnapshot();
        var telemetry = retainedSnapshot.Telemetry;
        var serviceStatuses = retainedSnapshot.ServiceStatuses;

        var records = new List<TelemetryExportRecord>(telemetry.Count + serviceStatuses.Count);
        for (var index = 0; index < telemetry.Count; index++)
        {
            records.Add(CreateTelemetryRecord(telemetry[index]));
        }

        for (var index = 0; index < serviceStatuses.Count; index++)
        {
            records.Add(CreateServiceStatusRecord(serviceStatuses[index]));
        }

        records.Sort(static (left, right) => left.TimestampUnixTimeMilliseconds.CompareTo(right.TimestampUnixTimeMilliseconds));
        return ResolveWriter(format).WriteAsync(records, outputPath, cancellationToken);
    }

    private static TelemetryExportRecord CreateTelemetryRecord(DebugTelemetryEnvelopeV1 telemetry)
    {
        var timestampUnixTimeMilliseconds = ConvertTicksToUnixTimeMilliseconds(telemetry.EndTimestampUtcTicks);
        var tags = DebugTelemetryTagFormatter.ToNames(telemetry.TagBits);
        return new TelemetryExportRecord
        {
            TimestampUtc = FormatTimestampUtc(timestampUnixTimeMilliseconds),
            TimestampUnixTimeMilliseconds = timestampUnixTimeMilliseconds,
            Stream = "telemetry",
            Name = telemetry.Name,
            IsSuccess = telemetry.IsSuccess,
            ElapsedMs = telemetry.ElapsedMs,
            Level = telemetry.Level,
            TraceId = telemetry.TraceId,
            SpanId = telemetry.SpanId,
            ParentSpanId = telemetry.ParentSpanId,
            TagBits = telemetry.TagBits,
            Tags = tags.Length == 0 ? null : tags,
            CpuTime = telemetry.CpuTime,
            GpuTime = telemetry.GpuTime,
            ManagedMem = telemetry.ManagedMem,
            NativeMem = telemetry.NativeMem,
            SceneFrom = telemetry.SceneFrom,
            SceneTo = telemetry.SceneTo,
        };
    }

    private static TelemetryExportRecord CreateServiceStatusRecord(DebugSocketServiceStatusEnvelopeV1 serviceStatus)
    {
        return new TelemetryExportRecord
        {
            TimestampUtc = FormatTimestampUtc(serviceStatus.TimestampUnixTimeMilliseconds),
            TimestampUnixTimeMilliseconds = serviceStatus.TimestampUnixTimeMilliseconds,
            Stream = "serviceStatus",
            Status = serviceStatus.Status,
            Message = serviceStatus.Message,
        };
    }

    private static long ConvertTicksToUnixTimeMilliseconds(long utcTicks)
    {
        if (utcTicks <= 0)
        {
            return 0;
        }

        try
        {
            return new DateTimeOffset(new DateTime(utcTicks, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatTimestampUtc(long unixTimeMilliseconds)
    {
        if (unixTimeMilliseconds <= 0)
        {
            return "1970-01-01T00:00:00.0000000Z";
        }

        try
        {
            return DateTimeOffset
                .FromUnixTimeMilliseconds(unixTimeMilliseconds)
                .UtcDateTime
                .ToString("O", CultureInfo.InvariantCulture);
        }
        catch
        {
            return "1970-01-01T00:00:00.0000000Z";
        }
    }

    private ITelemetryExportWriter ResolveWriter(TelemetryExportFormat format)
    {
        if (_writers.TryGetValue(format, out var writer))
        {
            return writer;
        }

        throw new InvalidOperationException($"Telemetry export writer for format '{format}' is not registered.");
    }
}
