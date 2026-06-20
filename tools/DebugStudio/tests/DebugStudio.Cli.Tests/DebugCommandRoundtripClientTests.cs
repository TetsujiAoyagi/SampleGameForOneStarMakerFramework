using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Cli.Tests;

public sealed class DebugCommandRoundtripClientTests
{
    [Fact]
    public async Task SendAsync_一致したrequestIdのresultだけで完了する()
    {
        await using var session = new FakeCommandSession();
        var client = new DebugCommandRoundtripClient(session);

        session.OnSendCommand = command =>
        {
            session.PublishCommandResult(new DebugCommandResultEnvelopeV1
            {
                RequestId = "other-request",
                Success = true,
                Message = "ignored",
                PayloadJson = "{}",
            });

            session.PublishCommandResult(new DebugCommandResultEnvelopeV1
            {
                RequestId = command.RequestId,
                Success = true,
                Message = "pong",
                PayloadJson = "{\"pong\":true}",
            });

            return Task.CompletedTask;
        };

        var result = await client.SendAsync(new DebugCommandRequest
        {
            ServerUri = new Uri("ws://127.0.0.1:5011/debugsocket/"),
            CommandType = "debugsocket.ping",
            PayloadJson = "{}",
            Timeout = TimeSpan.FromSeconds(5),
        });

        Assert.Equal(DebugCommandRoundtripStatus.Completed, result.Status);
        Assert.NotNull(result.CommandResult);
        Assert.Equal("pong", result.CommandResult!.Message);
        Assert.Equal("{\"pong\":true}", result.CommandResult.PayloadJson);
    }

    [Fact]
    public async Task SendAsync_timeout時はTimedOutを返す()
    {
        await using var session = new FakeCommandSession();
        var client = new DebugCommandRoundtripClient(session);

        var result = await client.SendAsync(new DebugCommandRequest
        {
            ServerUri = new Uri("ws://127.0.0.1:5011/debugsocket/"),
            CommandType = "debugsocket.ping",
            PayloadJson = "{}",
            Timeout = TimeSpan.FromMilliseconds(50),
        });

        Assert.Equal(DebugCommandRoundtripStatus.TimedOut, result.Status);
        Assert.Null(result.CommandResult);
        Assert.Contains("Timed out", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cliプロセスはcontrol_planeへ接続してCommandResultを表示できる()
    {
        var port = AllocateFreePort();
        var listenerPrefix = $"http://127.0.0.1:{port}/cli-control/";
        var controlUri = $"ws://127.0.0.1:{port}/cli-control/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(listenerPrefix);
        listener.Start();

        var serverTask = Task.Run(async () =>
        {
            var httpContext = await listener.GetContextAsync();
            var webSocketContext = await httpContext.AcceptWebSocketAsync(subProtocol: null);
            using var socket = webSocketContext.WebSocket;

            var inboundFrame = await ReceiveBinaryMessageAsync(socket);
            Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(inboundFrame, out var envelope));
            Assert.NotNull(envelope);
            Assert.Equal(DebugSocketMessageType.ControlCommandRequest, (DebugSocketMessageType)envelope!.MessageType);
            Assert.True(DebugSocketProtocol.TryDeserializePayload<ControlCommandRequestEnvelopeV1>(envelope!, out var commandRequest));
            Assert.NotNull(commandRequest);

            var responseFrame = DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.ControlCommandResponse,
                new ControlCommandResponseEnvelopeV1
                {
                    RequestId = commandRequest!.RequestId,
                    Status = ControlCommandRoundtripStatus.Completed,
                    Detail = "pong",
                    CommandResult = new DebugCommandResultEnvelopeV1
                    {
                        RequestId = commandRequest.RequestId,
                        Success = true,
                        Message = "pong",
                        PayloadJson = "{\"pong\":true}",
                    },
                },
                commandRequest.RequestId);

            await socket.SendAsync(
                new ArraySegment<byte>(responseFrame),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken: CancellationToken.None);

            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
            }
            catch (WebSocketException)
            {
            }
        });

        var cliAssemblyPath = typeof(DebugStudio.Cli.CliArgumentParser).Assembly.Location;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            Arguments =
                $"\"{cliAssemblyPath}\" send --control-uri {controlUri} --command debugsocket.ping --payload \"{{}}\" --timeout-seconds 5",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var stdoutTask = process!.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await serverTask;

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("CommandResult request=", stdout, StringComparison.Ordinal);
        Assert.Contains("success=true", stdout, StringComparison.Ordinal);
        Assert.Contains("{\"pong\":true}", stdout, StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(stderr), stderr);
    }

    private static async Task<byte[]> ReceiveBinaryMessageAsync(WebSocket socket)
    {
        var buffer = new byte[8192];
        await using var memoryStream = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("The CLI socket closed before a binary message was received.");
            }

            await memoryStream.WriteAsync(buffer.AsMemory(0, result.Count));
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

    private sealed class FakeCommandSession : IDebugStudioCommandSession
    {
        public event Action<DebugSocketConnectionSnapshot>? ConnectionStateChanged;
        public event Action<DebugCommandResultEnvelopeV1>? CommandResultReceived;

        public Func<DebugCommandEnvelopeV1, Task>? OnSendCommand { get; set; }

        public Task ConnectAsync(DebugSocketClientOptions options, CancellationToken cancellationToken = default)
        {
            ConnectionStateChanged?.Invoke(new DebugSocketConnectionSnapshot(
                DebugSocketConnectionState.Connected,
                options.ServerUri,
                $"Connected to {options.ServerUri}.",
                DateTimeOffset.UtcNow));
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            ConnectionStateChanged?.Invoke(new DebugSocketConnectionSnapshot(
                DebugSocketConnectionState.Disconnected,
                null,
                "Disconnected.",
                DateTimeOffset.UtcNow));
            return Task.CompletedTask;
        }

        public async Task SendCommandAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default)
        {
            if (OnSendCommand != null)
            {
                await OnSendCommand(command);
            }
        }

        public void PublishCommandResult(DebugCommandResultEnvelopeV1 result)
        {
            CommandResultReceived?.Invoke(result);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
