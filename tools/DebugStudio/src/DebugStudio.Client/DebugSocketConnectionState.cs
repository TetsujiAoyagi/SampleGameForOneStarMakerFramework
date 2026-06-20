namespace DebugStudio.Client;

public enum DebugSocketConnectionState
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Disconnecting = 3,
    Faulted = 4,
}
