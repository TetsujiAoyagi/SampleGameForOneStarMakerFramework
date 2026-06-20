namespace DebugStudio.Contracts.Protocol;

public enum DebugSocketMessageType
{
    Unknown = 0,
    Log = 1,
    Telemetry = 2,
    ServiceStatus = 3,
    DebugCommand = 4,
    CommandResult = 5,
    CapabilityHello = 6,
    CapabilityWelcome = 7,
    HierarchySnapshot = 8,
    HierarchyDelta = 9,
    InspectorQuery = 10,
    InspectorDetail = 11,
    ControlCommandRequest = 12,
    ControlCommandResponse = 13,
}
