using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using DebugStudio.Server;

namespace DebugStudio.Server.Tests;

public sealed class DebugStudioServerTests
{
    [Fact]
    public void StartListening_待受開始でListening状態になる()
    {
        var options = CreateOptions();
        using var server = new DebugStudioWebSocketServer(options);

        server.StartListening();

        Assert.True(server.IsListening);
        Assert.Equal(DebugStudioServerTransportState.Listening, server.State);
        Assert.Equal(options.GetListenerPrefix(), server.ListenerPrefix);
    }

    [Fact]
    public async Task AcceptWebSocketAsync_タイムアウトするとTimeoutExceptionが発生する()
    {
        var options = CreateOptions(acceptTimeoutSeconds: 1);
        using var server = new DebugStudioWebSocketServer(options);
        server.StartListening();

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => server.AcceptWebSocketAsync());

        Assert.Contains("timed out", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DebugStudioServerTransportState.Listening, server.State);
    }

    [Fact]
    public async Task AcceptWebSocketAsync_WebSocketでないHTTP要求は400を返して待受を継続する()
    {
        var options = CreateOptions(acceptTimeoutSeconds: 5);
        using var server = new DebugStudioWebSocketServer(options);
        server.StartListening();

        var acceptTask = server.AcceptWebSocketAsync();
        using var httpClient = new HttpClient();

        // 非 WebSocket リクエストで 400 を返した後も、同じ accept 待機が有効なままであることを確認する。
        using var invalidResponse = await httpClient.GetAsync(server.ListenerPrefix);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.False(acceptTask.IsCompleted);

        using var client = new ClientWebSocket();
        await client.ConnectAsync(CreateWebSocketUri(options), CancellationToken.None);

        var acceptedContext = await acceptTask;

        Assert.Equal(DebugStudioServerTransportState.Connected, server.State);
        Assert.NotNull(server.CurrentSocket);
        Assert.Equal(WebSocketState.Open, acceptedContext.WebSocket.State);
        Assert.Equal(WebSocketState.Open, client.State);
    }

    [Fact]
    public void Stop_複数回呼んでも安全にIdleへ戻る()
    {
        var options = CreateOptions();
        using var server = new DebugStudioWebSocketServer(options);
        server.StartListening();

        server.Stop();
        server.Stop();

        Assert.False(server.IsListening);
        Assert.Equal(DebugStudioServerTransportState.Idle, server.State);
        Assert.Null(server.CurrentSocket);
    }

    [Fact]
    public async Task Dispose_複数回呼んでも安全で以後の操作はObjectDisposedExceptionになる()
    {
        var options = CreateOptions();
        var server = new DebugStudioWebSocketServer(options);
        server.StartListening();

        server.Dispose();
        server.Dispose();

        Assert.False(server.IsListening);
        Assert.Equal(DebugStudioServerTransportState.Disposed, server.State);
        Assert.Null(server.CurrentSocket);
        Assert.Throws<ObjectDisposedException>(() => server.StartListening());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => server.AcceptWebSocketAsync());
    }

    private static DebugStudioServerOptions CreateOptions(int acceptTimeoutSeconds = 5)
    {
        return new DebugStudioServerOptions
        {
            Host = IPAddress.Loopback.ToString(),
            Port = AllocateFreePort(),
            WebSocketPath = "/debugsocket/",
            AcceptTimeoutSeconds = acceptTimeoutSeconds,
        };
    }

    private static Uri CreateWebSocketUri(DebugStudioServerOptions options)
    {
        var listenerPrefix = options.GetListenerPrefix();
        return new Uri(listenerPrefix.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase));
    }

    private static int AllocateFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}
