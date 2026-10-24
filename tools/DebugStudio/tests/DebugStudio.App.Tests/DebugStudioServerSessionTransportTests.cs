#nullable enable

using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Channels;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Services;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Server;

namespace DebugStudio.App.Tests;

[Collection("DebugStudioHttpListener")]
public sealed class DebugStudioServerSessionTransportTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AutoListen_idle_deadline_keeps_same_accept_without_fault_or_backoff_and_stop_cancels_it()
    {
        var timeouts = Channel.CreateUnbounded<CancellationTokenSource>();
        var options = new DebugStudioServerOptions();
        var creates = 0;
        CancellationTokenSource Timeout(TimeSpan _)
        {
            Interlocked.Increment(ref creates);
            var cts = new CancellationTokenSource();
            timeouts.Writer.TryWrite(cts);
            return cts;
        }
        await using var transport = new DebugStudioServerSessionTransport(options, true,
            o => new DebugStudioWebSocketServer(o, Timeout));
        var faults = 0;
        var backoffs = 0;
        var connects = 0;
        transport.ConnectionStateChanged += s => { if (s.State == DebugSocketConnectionState.Faulted) faults++; };
        await using var auto = new AutoListenService(
            (o, token) => { Interlocked.Increment(ref connects); return transport.ConnectAsync(o, token); },
            () => transport.DisconnectAsync(),
            h => transport.ConnectionStateChanged += h,
            h => transport.ConnectionStateChanged -= h,
            (_, _) => { backoffs++; return Task.CompletedTask; });
        auto.Start(new DebugSocketClientOptions { ServerUri = new Uri($"ws://127.0.0.1:{AllocateFreePort()}/debugsocket/") });
        await timeouts.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout); // existing default deadline
        var idle = await timeouts.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout);
        idle.Cancel();
        await timeouts.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout); // resumed same GetContext
        Assert.Equal(DebugSocketConnectionState.Connecting, transport.State);
        Assert.Equal(0, faults);
        Assert.Equal(0, backoffs);
        Assert.Equal(1, connects);
        await auto.StopAsync().WaitAsync(TestTimeout);
        var afterStop = creates;
        Assert.False(auto.CanStop);
        Assert.Equal(DebugSocketConnectionState.Disconnected, transport.State);
        Assert.Equal(0, faults);
        Assert.Equal(afterStop, creates);
        Assert.Equal(1, connects);
    }

    [Fact]
    public async Task AutoListen_idle_deadline_does_not_orphan_accept_before_real_connection()
    {
        var timeouts = Channel.CreateUnbounded<CancellationTokenSource>();
        CancellationTokenSource Timeout(TimeSpan _)
        {
            var cts = new CancellationTokenSource();
            timeouts.Writer.TryWrite(cts);
            return cts;
        }
        await using var transport = new DebugStudioServerSessionTransport(null, true,
            o => new DebugStudioWebSocketServer(o, Timeout));
        var uri = new Uri($"ws://127.0.0.1:{AllocateFreePort()}/debugsocket/");
        var connect = transport.ConnectAsync(new DebugSocketClientOptions { ServerUri = uri });
        await timeouts.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout);
        var idle = await timeouts.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout);
        idle.Cancel();
        await timeouts.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout);
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(uri, CancellationToken.None).WaitAsync(TestTimeout);
        await connect.WaitAsync(TestTimeout);
        Assert.Equal(DebugSocketConnectionState.Connected, transport.State);
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None).WaitAsync(TestTimeout);
        await transport.DisconnectAsync().WaitAsync(TestTimeout);
    }

    [Fact]
    public async Task Default_idle_deadline_preserves_timeout_fault()
    {
        var timeouts = Channel.CreateUnbounded<CancellationTokenSource>();
        CancellationTokenSource Timeout(TimeSpan _)
        {
            var cts = new CancellationTokenSource();
            timeouts.Writer.TryWrite(cts);
            return cts;
        }
        await using var transport = new DebugStudioServerSessionTransport(null, false,
            o => new DebugStudioWebSocketServer(o, Timeout));
        var connect = transport.ConnectAsync(new DebugSocketClientOptions
        {
            ServerUri = new Uri($"ws://127.0.0.1:{AllocateFreePort()}/debugsocket/")
        });
        var deadline = await timeouts.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout);
        deadline.Cancel();
        await Assert.ThrowsAsync<TimeoutException>(() => connect.WaitAsync(TestTimeout));
        Assert.Equal(DebugSocketConnectionState.Faulted, transport.State);
    }

    [Fact]
    public async Task Default_deadline_is_not_renewed_by_ignored_http_requests()
    {
        var timeouts = Channel.CreateUnbounded<CancellationTokenSource>();
        var created = 0;
        CancellationTokenSource Timeout(TimeSpan _)
        {
            Interlocked.Increment(ref created);
            var cts = new CancellationTokenSource();
            timeouts.Writer.TryWrite(cts);
            return cts;
        }
        await using var transport = new DebugStudioServerSessionTransport(null, false,
            o => new DebugStudioWebSocketServer(o, Timeout));
        var uri = new Uri($"ws://127.0.0.1:{AllocateFreePort()}/debugsocket/");
        var connect = transport.ConnectAsync(new DebugSocketClientOptions { ServerUri = uri });
        var originalDeadline = await timeouts.Reader.ReadAsync().AsTask().WaitAsync(TestTimeout);
        using var http = new System.Net.Http.HttpClient();
        var httpUri = new UriBuilder(uri) { Scheme = "http" }.Uri;
        for (var request = 0; request < 2; request++)
        {
            using var response = await http.GetAsync(httpUri).WaitAsync(TestTimeout);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // Advance the deadline created before either HTTP request, rather than waiting wall time.
        originalDeadline.Cancel();
        var error = await Assert.ThrowsAsync<TimeoutException>(() => connect.WaitAsync(TestTimeout));
        Assert.Contains("Accepting a WebSocket connection timed out", error.Message);
        Assert.Equal(DebugSocketConnectionState.Faulted, transport.State);
        Assert.Equal(1, Volatile.Read(ref created));
    }

    [Fact]
    public async Task ConnectAsync_着信WebSocketを受けると送受信を仲介できる()
    {
        var port = AllocateFreePort();
        var serverUri = new Uri($"ws://127.0.0.1:{port}/debugsocket/");

        await using var transport = new DebugStudioServerSessionTransport(new DebugStudioServerOptions
        {
            AcceptTimeoutSeconds = 5,
        });

        var logReceived = new TaskCompletionSource<LogEnvelopeV1>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.LogReceived += envelope => logReceived.TrySetResult(envelope);

        var connectTask = transport.ConnectAsync(new DebugSocketClientOptions
        {
            ServerUri = serverUri,
        });

        using var clientSocket = new ClientWebSocket();
        await clientSocket.ConnectAsync(serverUri, CancellationToken.None).WaitAsync(TestTimeout);
        await connectTask.WaitAsync(TestTimeout);

        Assert.Equal(DebugSocketConnectionState.Connected, transport.State);

        var outboundLog = new LogEnvelopeV1
        {
            ApplicationName = "Unity",
            Category = "tests",
            LogLevel = 2,
            Message = "hello-from-client",
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        var outboundFrame = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Log, outboundLog);
        await clientSocket.SendAsync(
            new ArraySegment<byte>(outboundFrame),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None);

        var receivedLog = await logReceived.Task.WaitAsync(TestTimeout);
        Assert.Equal(outboundLog.Message, receivedLog.Message);

        var command = new DebugCommandEnvelopeV1
        {
            RequestId = "req-1",
            CommandType = "debugsocket.ping",
            PayloadJson = "{}",
        };

        await transport.SendCommandAsync(command).WaitAsync(TestTimeout);

        var inboundFrame = await ReceiveBinaryMessageAsync(clientSocket);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(inboundFrame, out var envelope));
        Assert.NotNull(envelope);
        Assert.Equal(DebugSocketMessageType.DebugCommand, (DebugSocketMessageType)envelope!.MessageType);
        Assert.True(DebugSocketProtocol.TryDeserializePayload<DebugCommandEnvelopeV1>(envelope, out var receivedCommand));
        Assert.NotNull(receivedCommand);
        Assert.Equal(command.RequestId, receivedCommand!.RequestId);
        Assert.Equal(command.CommandType, receivedCommand.CommandType);

        // server 側 receive loop が Close を観測できるよう、先に peer socket を閉じる。
        // Disconnect 前に閉じないと Accept/Receive 待ちが残り、testhost 終了がハングしうる。
        //
        // CloseAsync ではなく CloseOutputAsync を使う。CloseAsync は close frame を送った後に
        // peer からの close 応答を待つため、teardown 自体がハングしうる。
        if (clientSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            using var closeTimeout = new CancellationTokenSource(TestTimeout);
            await clientSocket.CloseOutputAsync(
                WebSocketCloseStatus.NormalClosure,
                "session-transport-test-complete",
                closeTimeout.Token);
        }

        await transport.DisconnectAsync().WaitAsync(TestTimeout);
        Assert.Equal(DebugSocketConnectionState.Disconnected, transport.State);
    }

    private static async Task<byte[]> ReceiveBinaryMessageAsync(ClientWebSocket socket)
    {
        var buffer = new byte[8192];
        using var memoryStream = new MemoryStream();
        using var timeoutCts = new CancellationTokenSource(TestTimeout);

        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeoutCts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("The client socket closed before a binary message was received.");
            }

            memoryStream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return memoryStream.ToArray();
            }
        }
    }

    private static int AllocateFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}
