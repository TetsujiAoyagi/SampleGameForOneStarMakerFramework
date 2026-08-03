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
/// <para>
/// Contract v3: Kind / Payload を正とする。
/// sample の ElapsedMs、瞬間 event の ElapsedMs=0、旧 flat の -1 / 無意味な 0 はキー省略する。
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
        var kind = string.IsNullOrEmpty(telemetry.Kind) ? "span" : telemetry.Kind;
        var isSample = string.Equals(kind, "sample", StringComparison.OrdinalIgnoreCase);
        var isEvent = string.Equals(kind, "event", StringComparison.OrdinalIgnoreCase);

        // sample: elapsed は意味を持たない。event で 0 は瞬間発火のプレースホルダなので省略。
        double? elapsedMs = null;
        if (!isSample && !(isEvent && telemetry.ElapsedMs <= 0.0))
        {
            elapsedMs = telemetry.ElapsedMs;
        }

        // TimingMemory / CameraCounters など payload が正のとき、無意味な flat 0/-1 を出さない。
        var payload = MapPayload(telemetry.Payload);
        var hasTimingMemory = payload?.Shape == "TimingMemory";
        var hasFrame = payload?.Shape == "Frame";
        var hasCamera = payload?.Shape == "CameraCounters";

        return new TelemetryExportRecord
        {
            TimestampUtc = FormatTimestampUtc(timestampUnixTimeMilliseconds),
            TimestampUnixTimeMilliseconds = timestampUnixTimeMilliseconds,
            Stream = "telemetry",
            Name = telemetry.Name,
            Kind = kind,
            SchemaVersion = telemetry.SchemaVersion > 0 ? telemetry.SchemaVersion : null,
            Payload = payload,
            IsSuccess = telemetry.IsSuccess,
            ElapsedMs = elapsedMs,
            Level = telemetry.Level,
            TraceId = telemetry.TraceId,
            SpanId = telemetry.SpanId,
            ParentSpanId = telemetry.ParentSpanId,
            TagBits = telemetry.TagBits,
            Tags = tags.Length == 0 ? null : tags,
            // Scene span 等に cpu/gpu 欄を持たせない（0 汚染防止）。Frame sample だけ flat 併記可。
            CpuTime = hasFrame || (!hasTimingMemory && telemetry.CpuTime != 0f) ? telemetry.CpuTime : null,
            GpuTime = hasFrame || (!hasTimingMemory && telemetry.GpuTime != 0f) ? telemetry.GpuTime : null,
            ManagedMem = hasTimingMemory || hasFrame || telemetry.ManagedMem != 0
                ? (long?)telemetry.ManagedMem
                : null,
            NativeMem = hasTimingMemory || hasFrame || telemetry.NativeMem != 0
                ? (long?)telemetry.NativeMem
                : null,
            SceneFrom = NullIfSentinel(telemetry.SceneFrom),
            SceneTo = NullIfSentinel(telemetry.SceneTo),
            // Camera は payload 正本があれば flat を省略（段階移行の読みやすさ優先）
            CameraTotalViewCount = hasCamera ? null : NullIfSentinel(telemetry.CameraTotalViewCount),
            CameraAdditionalViewCount = hasCamera ? null : NullIfSentinel(telemetry.CameraAdditionalViewCount),
            CameraBlendingViewCount = hasCamera ? null : NullIfSentinel(telemetry.CameraBlendingViewCount),
            CameraMaxStackDepthTotal = hasCamera ? null : NullIfSentinel(telemetry.CameraMaxStackDepthTotal),
            CameraViewId = NullIfSentinel(telemetry.CameraViewId),
            CameraActiveCameraHash = NullIfSentinel(telemetry.CameraActiveCameraHash),
            SessionId = string.IsNullOrEmpty(telemetry.SessionId) ? null : telemetry.SessionId,
            ProducerSequence = telemetry.ProducerSequence > 0 ? telemetry.ProducerSequence : null,
            UnityFrameAtStart = telemetry.UnityFrameAtStart,
            UnityFrameAtEnd = telemetry.UnityFrameAtEnd,
        };
    }

    private static int? NullIfSentinel(int value) => value < 0 ? null : value;

    private static TelemetryExportPayload? MapPayload(DebugTelemetryPayloadV1? payload)
    {
        if (payload == null || payload.Shape == 0)
        {
            return null;
        }

        var shapeName = payload.Shape switch
        {
            1 => "TimingMemory",
            2 => "Frame",
            3 => "EventDetail",
            4 => "CameraCounters",
            _ => "Unknown",
        };

        return new TelemetryExportPayload
        {
            Shape = shapeName,
            TargetIdentity = payload.TargetIdentity,
            Stage = payload.Stage,
            ManagedBeforeBytes = payload.ManagedBeforeBytes,
            NativeBeforeBytes = payload.NativeBeforeBytes,
            ManagedAfterBytes = payload.ManagedAfterBytes,
            NativeAfterBytes = payload.NativeAfterBytes,
            ManagedDeltaBytes = payload.ManagedDeltaBytes,
            NativeDeltaBytes = payload.NativeDeltaBytes,
            Fps = payload.Fps,
            CpuMs = payload.CpuMs,
            GpuMs = payload.GpuMs,
            GpuAvailable = payload.GpuAvailable,
            ManagedBytes = payload.ManagedBytes,
            NativeBytes = payload.NativeBytes,
            GcGen0Delta = payload.GcGen0Delta,
            UnityFrame = payload.UnityFrame,
            CameraTotalViewCount = payload.CameraTotalViewCount,
            CameraAdditionalViewCount = payload.CameraAdditionalViewCount,
            CameraBlendingViewCount = payload.CameraBlendingViewCount,
            CameraMaxStackDepthTotal = payload.CameraMaxStackDepthTotal,
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
