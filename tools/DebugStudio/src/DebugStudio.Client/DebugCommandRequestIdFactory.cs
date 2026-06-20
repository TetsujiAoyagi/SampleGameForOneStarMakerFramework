#nullable enable

using System;

namespace DebugStudio.Client;

public static class DebugCommandRequestIdFactory
{
    public static string Create(string commandType)
    {
        var normalizedCommandType = string.IsNullOrWhiteSpace(commandType) ? "command" : commandType.Trim();
        return $"{normalizedCommandType}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}";
    }
}
