#nullable enable

using System;

namespace DebugStudio.Client;

public sealed class DebugCommandRequest
{
    public Uri ServerUri { get; init; } = new("ws://127.0.0.1:5011/debugsocket/");

    public string CommandType { get; init; } = string.Empty;

    public string PayloadJson { get; init; } = "{}";

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);

    public string? RequestId { get; init; }
}
