#nullable enable

using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
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
