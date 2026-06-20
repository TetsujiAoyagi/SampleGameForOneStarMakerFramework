#nullable enable

using System;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// DebugStudio 側の capability hello を組み立てる薄い service。
///
/// <para>
/// 交渉対象 capability を 1 箇所へ閉じ込めることで、
/// ViewModel や SessionService が「どの機能を広告するか」の知識を持たずに済む。
/// </para>
/// </summary>
public sealed class CapabilityHandshakeService
{
    private static readonly int[] SupportedMessageTypes =
    {
        (int)DebugSocketMessageType.Log,
        (int)DebugSocketMessageType.Telemetry,
        (int)DebugSocketMessageType.ServiceStatus,
        (int)DebugSocketMessageType.DebugCommand,
        (int)DebugSocketMessageType.CommandResult,
        (int)DebugSocketMessageType.CapabilityHello,
        (int)DebugSocketMessageType.CapabilityWelcome,
        (int)DebugSocketMessageType.HierarchySnapshot,
        (int)DebugSocketMessageType.HierarchyDelta,
        (int)DebugSocketMessageType.InspectorQuery,
        (int)DebugSocketMessageType.InspectorDetail,
    };

    private readonly string _clientInstanceId = Guid.NewGuid().ToString("N");

    public CapabilityHandshakeService()
    {
        LocalSupportedCapabilities =
            DebugStudioCapability.CapabilityNegotiation |
            DebugStudioCapability.LogStream |
            DebugStudioCapability.TelemetryStream |
            DebugStudioCapability.ServiceStatusStream |
            DebugStudioCapability.DebugCommand |
            DebugStudioCapability.CommandResult |
            DebugStudioCapability.HierarchySnapshot |
            DebugStudioCapability.HierarchyDelta |
            DebugStudioCapability.InspectorQuery |
            DebugStudioCapability.InspectorDetail;
    }

    public DebugStudioCapability LocalSupportedCapabilities { get; }

    public CapabilityHandshakeHelloEnvelopeV1 CreateHello()
    {
        return new CapabilityHandshakeHelloEnvelopeV1
        {
            ClientName = "DebugStudio.App",
            ClientInstanceId = _clientInstanceId,
            MinSchemaVersion = 1,
            MaxSchemaVersion = 1,
            SupportedCapabilities = LocalSupportedCapabilities,
            SupportedMessageTypes = SupportedMessageTypes,
        };
    }
}
