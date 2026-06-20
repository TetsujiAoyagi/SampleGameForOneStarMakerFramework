#nullable enable

using System;

namespace DebugStudio.Client;

public sealed class DebugSocketClientOptions
{
    public static readonly Uri DefaultServerUri = new("ws://127.0.0.1:5010/debugsocket/");

    public Uri ServerUri { get; init; } = DefaultServerUri;

    public TimeSpan KeepAliveInterval { get; init; } = TimeSpan.FromSeconds(10);

    public string? OriginHeader { get; init; }
}
