#nullable enable

using System;
using System.Net.Sockets;
using System.Net.WebSockets;
using DebugStudio.App.Core.Formatting;

namespace DebugStudio.App.Tests;

public sealed class DebugSocketConnectionErrorFormatterTests
{
    private static readonly Uri ServerUri = new("ws://127.0.0.1:5011/debugsocket/");

    [Fact]
    public void ConnectionRefused時_UnityListener未起動ヒントを返す()
    {
        var exception = new InvalidOperationException(
            "outer",
            new SocketException((int)SocketError.ConnectionRefused));

        var detail = DebugSocketConnectionErrorFormatter.Format(ServerUri, exception);

        Assert.Contains("Connection refused", detail, StringComparison.Ordinal);
        Assert.Contains("debugSocket:enabled", detail, StringComparison.Ordinal);
        Assert.Contains("StartAsync", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void NotAWebSocket時_Path確認ヒントを返す()
    {
        var exception = new WebSocketException(WebSocketError.NotAWebSocket);

        var detail = DebugSocketConnectionErrorFormatter.Format(ServerUri, exception);

        Assert.Contains("WebSocket handshake failed", detail, StringComparison.Ordinal);
        Assert.Contains("path", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void それ以外は基底例外メッセージを含める()
    {
        var exception = new InvalidOperationException("top-level", new Exception("boom"));

        var detail = DebugSocketConnectionErrorFormatter.Format(ServerUri, exception);

        Assert.Contains("Failed to connect", detail, StringComparison.Ordinal);
        Assert.Contains("boom", detail, StringComparison.Ordinal);
    }
}
