#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using DebugStudio.App.Core.Models;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Contracts.Schema;

namespace DebugStudio.App.Core.Formatting;

/// <summary>
/// transport/model から UI 表示文字列を生成する薄い formatter 集約。
///
/// <para>
/// 「文字列化」は WPF 依存ではないが allocation を伴いやすいため、
/// schema や store から分離して app 側の表示境界へ寄せている。
/// これにより将来、同じ raw record を別 UI (CLI / web / export) へ流す際にも
/// schema 自体を汚さずに済む。
/// </para>
/// </summary>
public static class DebugStudioTextFormatter
{
    public static string FormatLog(LogRecord log)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{FormatUnixTime(log.TimestampUnixTimeMilliseconds)}] [log] {FormatLogKind(log.Kind, log.RawLogLevel)} {log.Category}: {log.Message}");
    }

    public static string FormatTelemetry(DebugTelemetryEnvelopeV1 telemetry)
    {
        var successText = telemetry.IsSuccess ? "success" : "failure";
        var tagsText = DebugTelemetryTagFormatter.FormatInline(telemetry.TagBits);
        var kind = string.IsNullOrEmpty(telemetry.Kind) ? "span" : telemetry.Kind;
        // sample / 瞬間 event では elapsedMs=0 を表示しない（Contract v3）
        var showElapsed = !string.Equals(kind, "sample", StringComparison.OrdinalIgnoreCase)
            && !(string.Equals(kind, "event", StringComparison.OrdinalIgnoreCase) && telemetry.ElapsedMs <= 0.0);
        var elapsedPart = showElapsed
            ? string.Create(CultureInfo.InvariantCulture, $"{telemetry.ElapsedMs:F2} ms ")
            : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{FormatTicks(telemetry.EndTimestampUtcTicks)}] [telemetry/{kind}] {telemetry.Name} {elapsedPart}{successText} trace={telemetry.TraceId} span={telemetry.SpanId}{(string.IsNullOrEmpty(tagsText) ? string.Empty : $" tags={tagsText}")}");
    }

    public static string FormatServiceStatus(DebugSocketServiceStatusEnvelopeV1 status)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{FormatUnixTime(status.TimestampUnixTimeMilliseconds)}] [status] {status.Status}: {status.Message}");
    }

    public static string FormatCommandResult(DebugCommandResultEnvelopeV1 result)
    {
        var outcome = result.Success ? "success" : "failure";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[command-result] {outcome} request={result.RequestId} message={result.Message}");
    }

    public static string FormatCommandState(CommandDispatchState state)
    {
        return state switch
        {
            CommandDispatchState.Pending => "Pending",
            CommandDispatchState.Succeeded => "Succeeded",
            CommandDispatchState.Failed => "Failed",
            CommandDispatchState.DispatchFailed => "DispatchFailed",
            CommandDispatchState.TimedOut => "TimedOut",
            CommandDispatchState.Disconnected => "Disconnected",
            CommandDispatchState.Orphaned => "Orphaned",
            _ => state.ToString(),
        };
    }

    public static string FormatCommandTiming(long startedAtUnixTimeMilliseconds, long? completedAtUnixTimeMilliseconds)
    {
        var startedText = FormatUnixTime(startedAtUnixTimeMilliseconds);
        if (completedAtUnixTimeMilliseconds == null)
        {
            return $"started={startedText}";
        }

        return $"started={startedText} completed={FormatUnixTime(completedAtUnixTimeMilliseconds.Value)}";
    }

    public static string FormatCapabilityWelcome(CapabilityHandshakeWelcomeEnvelopeV1 welcome)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[capabilities] server={welcome.ServerName} negotiated={FormatCapabilities(welcome.NegotiatedCapabilities)}");
    }

    public static string FormatHierarchySnapshot(HierarchySnapshotEnvelopeV1 snapshot)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[hierarchy] snapshot rev={snapshot.Revision} nodes={snapshot.Nodes.Length} scope={snapshot.ScopeName}");
    }

    public static string FormatHierarchyDelta(HierarchyDeltaEnvelopeV1 delta)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[hierarchy] delta rev={delta.Revision} changes={delta.Changes.Length} scope={delta.ScopeName}");
    }

    public static string FormatInspectorDetail(InspectorDetailEnvelopeV1 detail)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[inspector] target={detail.TargetName} state={detail.State} sections={detail.Sections.Length}");
    }

    public static string FormatHierarchySummary(string scopeName, long revision, int nodeCount)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{scopeName} / rev {revision} / {nodeCount} nodes");
    }

    public static string FormatCapabilities(DebugStudioCapability capabilities)
    {
        if (capabilities == DebugStudioCapability.None)
        {
            return "None";
        }

        var parts = new List<string>();
        if ((capabilities & DebugStudioCapability.CapabilityNegotiation) != 0)
        {
            parts.Add("Handshake");
        }

        if ((capabilities & DebugStudioCapability.LogStream) != 0)
        {
            parts.Add("Logs");
        }

        if ((capabilities & DebugStudioCapability.TelemetryStream) != 0)
        {
            parts.Add("Telemetry");
        }

        if ((capabilities & DebugStudioCapability.ServiceStatusStream) != 0)
        {
            parts.Add("Status");
        }

        if ((capabilities & DebugStudioCapability.DebugCommand) != 0)
        {
            parts.Add("Commands");
        }

        if ((capabilities & DebugStudioCapability.CommandResult) != 0)
        {
            parts.Add("Results");
        }

        if ((capabilities & DebugStudioCapability.HierarchySnapshot) != 0)
        {
            parts.Add("HierarchySnapshot");
        }

        if ((capabilities & DebugStudioCapability.HierarchyDelta) != 0)
        {
            parts.Add("HierarchyDelta");
        }

        if ((capabilities & DebugStudioCapability.InspectorQuery) != 0)
        {
            parts.Add("InspectorQuery");
        }

        if ((capabilities & DebugStudioCapability.InspectorDetail) != 0)
        {
            parts.Add("InspectorDetail");
        }

        return string.Join(", ", parts);
    }

    public static string FormatUnixTime(long unixTimeMilliseconds)
    {
        try
        {
            return DateTimeOffset
                .FromUnixTimeMilliseconds(unixTimeMilliseconds)
                .ToLocalTime()
                .ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }
        catch
        {
            return unixTimeMilliseconds.ToString(CultureInfo.InvariantCulture);
        }
    }

    public static string FormatTicks(long ticks)
    {
        try
        {
            return new DateTime(ticks, DateTimeKind.Utc)
                .ToLocalTime()
                .ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }
        catch
        {
            return ticks.ToString(CultureInfo.InvariantCulture);
        }
    }

    public static string FormatLogKind(LogEntryKind kind, int rawLogLevel)
    {
        return kind switch
        {
            LogEntryKind.Trace => "Trace",
            LogEntryKind.Debug => "Debug",
            LogEntryKind.Information => "Information",
            LogEntryKind.Warning => "Warning",
            LogEntryKind.Error => "Error",
            LogEntryKind.Critical => "Critical",
            LogEntryKind.None => "None",
            _ => $"Level({rawLogLevel})",
        };
    }
}
