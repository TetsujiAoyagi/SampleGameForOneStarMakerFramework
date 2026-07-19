#nullable enable

using System;
using System.Globalization;
using DebugStudio.App.Core.Formatting;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Export.Models;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// Unity から受信した <see cref="DebugTelemetryEnvelopeV1"/> を
/// export / 自動永続で共有する <see cref="TelemetryExportRecord"/> へ正規化する mapper。
///
/// <para>
/// 手動 Export と rolling file 永続が同じ field mapping を使うことで、
/// NDJSON schema のドリフトを防ぐ。ServiceStatus は stream 意味と運用判断が異なるため、
/// 今回の自動永続対象外としてここへ含めない。
/// </para>
/// </summary>
public static class TelemetryRecordExportMapper
{
    /// <summary>
    /// telemetry envelope を Elastic-ready な export record へ変換する。
    /// UTC ticks → Unix milliseconds、tag bit → tag names の既存変換を維持する。
    /// </summary>
    public static TelemetryExportRecord ToExportRecord(DebugTelemetryEnvelopeV1 telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

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
            CameraTotalViewCount = telemetry.CameraTotalViewCount,
            CameraAdditionalViewCount = telemetry.CameraAdditionalViewCount,
            CameraBlendingViewCount = telemetry.CameraBlendingViewCount,
            CameraMaxStackDepthTotal = telemetry.CameraMaxStackDepthTotal,
            CameraViewId = telemetry.CameraViewId,
            CameraActiveCameraHash = telemetry.CameraActiveCameraHash,
            SessionId = string.IsNullOrEmpty(telemetry.SessionId) ? null : telemetry.SessionId,
            ProducerSequence = telemetry.ProducerSequence > 0 ? telemetry.ProducerSequence : null,
            UnityFrameAtStart = telemetry.UnityFrameAtStart,
            UnityFrameAtEnd = telemetry.UnityFrameAtEnd,
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
}
