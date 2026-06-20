#nullable enable

using System;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Client.Internal;

/// <summary>
/// 受信した framed message を envelope として decode し、
/// 型ごとの event へルーティングする。
/// </summary>
internal sealed class DebugSocketInboundRouter
{
    public event Action<LogEnvelopeV1>? LogReceived;
    public event Action<DebugTelemetryEnvelopeV1>? TelemetryReceived;
    public event Action<DebugSocketServiceStatusEnvelopeV1>? ServiceStatusReceived;
    public event Action<DebugCommandResultEnvelopeV1>? CommandResultReceived;
    public event Action<CapabilityHandshakeWelcomeEnvelopeV1>? CapabilityWelcomeReceived;
    public event Action<HierarchySnapshotEnvelopeV1>? HierarchySnapshotReceived;
    public event Action<HierarchyDeltaEnvelopeV1>? HierarchyDeltaReceived;
    public event Action<InspectorDetailEnvelopeV1>? InspectorDetailReceived;

    public void RouteInboundFrame(ReadOnlyMemory<byte> framedMessage)
    {
        // まず framed envelope として decode し、schema と message type を取り出す。
        if (!DebugSocketProtocol.TryDeserializeEnvelope(framedMessage, out var envelope) || envelope == null)
        {
            PublishProtocolError("Failed to decode a framed DebugSocket envelope.");
            return;
        }

        // 現在は schema 1 のみ受理する。未知 schema を既知 DTO へ読まないように先に遮断する。
        if (envelope.SchemaVersion != 1)
        {
            PublishSyntheticStatus("schema-mismatch", $"Unsupported DebugSocket schema version: {envelope.SchemaVersion}.");
            return;
        }

        // message type ごとに payload decode → typed event 発火を行う。
        // decode 失敗は transport 全体を落とさず、protocol error として観測面へ流す。
        switch ((DebugSocketMessageType)envelope.MessageType)
        {
            case DebugSocketMessageType.Log:
                if (DebugSocketProtocol.TryDeserializePayload<LogEnvelopeV1>(envelope, out var log) && log != null)
                {
                    LogReceived?.Invoke(log);
                    return;
                }

                PublishProtocolError("Failed to decode a log payload.");
                return;

            case DebugSocketMessageType.Telemetry:
                if (DebugSocketProtocol.TryDeserializePayload<DebugTelemetryEnvelopeV1>(envelope, out var telemetry) &&
                    telemetry != null)
                {
                    TelemetryReceived?.Invoke(telemetry);
                    return;
                }

                PublishProtocolError("Failed to decode a telemetry payload.");
                return;

            case DebugSocketMessageType.ServiceStatus:
                if (DebugSocketProtocol.TryDeserializePayload<DebugSocketServiceStatusEnvelopeV1>(envelope, out var status) &&
                    status != null)
                {
                    ServiceStatusReceived?.Invoke(status);
                    return;
                }

                PublishProtocolError("Failed to decode a service status payload.");
                return;

            case DebugSocketMessageType.CommandResult:
                if (DebugSocketProtocol.TryDeserializePayload<DebugCommandResultEnvelopeV1>(envelope, out var commandResult) &&
                    commandResult != null)
                {
                    CommandResultReceived?.Invoke(commandResult);
                    return;
                }

                PublishProtocolError("Failed to decode a command result payload.");
                return;

            case DebugSocketMessageType.CapabilityWelcome:
                if (DebugSocketProtocol.TryDeserializePayload<CapabilityHandshakeWelcomeEnvelopeV1>(envelope, out var welcome) &&
                    welcome != null)
                {
                    CapabilityWelcomeReceived?.Invoke(welcome);
                    return;
                }

                PublishProtocolError("Failed to decode a capability welcome payload.");
                return;

            case DebugSocketMessageType.HierarchySnapshot:
                if (DebugSocketProtocol.TryDeserializePayload<HierarchySnapshotEnvelopeV1>(envelope, out var hierarchySnapshot) &&
                    hierarchySnapshot != null)
                {
                    HierarchySnapshotReceived?.Invoke(hierarchySnapshot);
                    return;
                }

                PublishProtocolError("Failed to decode a hierarchy snapshot payload.");
                return;

            case DebugSocketMessageType.HierarchyDelta:
                if (DebugSocketProtocol.TryDeserializePayload<HierarchyDeltaEnvelopeV1>(envelope, out var hierarchyDelta) &&
                    hierarchyDelta != null)
                {
                    HierarchyDeltaReceived?.Invoke(hierarchyDelta);
                    return;
                }

                PublishProtocolError("Failed to decode a hierarchy delta payload.");
                return;

            case DebugSocketMessageType.InspectorDetail:
                if (DebugSocketProtocol.TryDeserializePayload<InspectorDetailEnvelopeV1>(envelope, out var inspectorDetail) &&
                    inspectorDetail != null)
                {
                    InspectorDetailReceived?.Invoke(inspectorDetail);
                    return;
                }

                PublishProtocolError("Failed to decode an inspector detail payload.");
                return;

            default:
                PublishProtocolError($"Unsupported inbound message type: {(DebugSocketMessageType)envelope.MessageType}.");
                return;
        }
    }

    public void PublishProtocolError(string message)
    {
        PublishSyntheticStatus("protocol-error", message);
    }

    private void PublishSyntheticStatus(string status, string message)
    {
        // protocol 専用の UI はまだ無いので、現状は service status envelope に畳んで通知する。
        ServiceStatusReceived?.Invoke(new DebugSocketServiceStatusEnvelopeV1
        {
            Status = status,
            Message = message,
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
    }
}
