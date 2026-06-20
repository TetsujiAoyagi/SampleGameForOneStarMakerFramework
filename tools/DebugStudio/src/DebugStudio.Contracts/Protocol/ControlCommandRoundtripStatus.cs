#nullable enable

namespace DebugStudio.Contracts.Protocol;

public enum ControlCommandRoundtripStatus
{
    Completed = 0,
    TimedOut = 1,
    Failed = 2,
}
