#nullable enable

using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Server;

namespace DebugStudio.App.Tests;

[Collection("DebugStudioHttpListener")]
public sealed class CliControlPlaneRoundtripTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Cli_control_planeはUnity本線を維持したままCommandResultを返す()
    {
        var unityPort = AllocateFreePort();
        var controlPort = AllocateFreePort();
        var unityServerUri = new Uri($"ws://127.0.0.1:{unityPort}/debugsocket/");
        var controlUri = new Uri($"ws://127.0.0.1:{controlPort}/cli-control/");

        await using var harness = CreateHarness(controlUri);
        using var unitySocket = new ClientWebSocket();
        using var cliSocket = new ClientWebSocket();

        var connectTask = harness.SessionService.ConnectAsync(new DebugSocketClientOptions
        {
            ServerUri = unityServerUri,
        });

        await unitySocket.ConnectAsync(unityServerUri, CancellationToken.None);
        await connectTask;

        var helloEnvelope = await ReceiveEnvelopeAsync(unitySocket);
        Assert.Equal(DebugSocketMessageType.CapabilityHello, (DebugSocketMessageType)helloEnvelope.MessageType);

        await SendMessageAsync(unitySocket, DebugSocketMessageType.CapabilityWelcome, new CapabilityHandshakeWelcomeEnvelopeV1
        {
            SessionId = "unity-session-cli-control",
            ServerName = "Unity Editor",
            SelectedSchemaVersion = 1,
            ServerCapabilities =
                DebugStudioCapability.CapabilityNegotiation |
                DebugStudioCapability.DebugCommand |
                DebugStudioCapability.CommandResult,
            NegotiatedCapabilities =
                DebugStudioCapability.DebugCommand |
                DebugStudioCapability.CommandResult,
            SupportedMessageTypes = new[]
            {
                (int)DebugSocketMessageType.CapabilityHello,
                (int)DebugSocketMessageType.CapabilityWelcome,
                (int)DebugSocketMessageType.DebugCommand,
                (int)DebugSocketMessageType.CommandResult,
            },
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            StatusMessage = "CLI control plane ready.",
        });

        await cliSocket.ConnectAsync(controlUri, CancellationToken.None);
        await SendMessageAsync(cliSocket, DebugSocketMessageType.ControlCommandRequest, new ControlCommandRequestEnvelopeV1
        {
            RequestId = "cli-control-req-1",
            CommandType = "debugsocket.runtime-diagnostics",
            PayloadJson = "{\"scope\":\"cli-control\"}",
            TimeoutMilliseconds = 5000,
        }, "cli-control-req-1");

        var forwardedCommandEnvelope = await ReceiveEnvelopeAsync(unitySocket);
        Assert.Equal(DebugSocketMessageType.DebugCommand, (DebugSocketMessageType)forwardedCommandEnvelope.MessageType);
        Assert.True(DebugSocketProtocol.TryDeserializePayload<DebugCommandEnvelopeV1>(forwardedCommandEnvelope, out var forwardedCommand));
        Assert.NotNull(forwardedCommand);
        Assert.Equal("cli-control-req-1", forwardedCommand!.RequestId);
        Assert.Equal("debugsocket.runtime-diagnostics", forwardedCommand.CommandType);
        Assert.Equal("{\"scope\":\"cli-control\"}", forwardedCommand.PayloadJson);
        Assert.Equal(WebSocketState.Open, unitySocket.State);

        await SendMessageAsync(unitySocket, DebugSocketMessageType.CommandResult, new DebugCommandResultEnvelopeV1
        {
            RequestId = forwardedCommand.RequestId,
            Success = true,
            Message = "Runtime diagnostics captured.",
            PayloadJson = "{\"pendingQueueLength\":0}",
        }, forwardedCommand.RequestId);

        var cliResponseEnvelope = await ReceiveEnvelopeAsync(cliSocket);
        Assert.Equal(DebugSocketMessageType.ControlCommandResponse, (DebugSocketMessageType)cliResponseEnvelope.MessageType);
        Assert.True(DebugSocketProtocol.TryDeserializePayload<ControlCommandResponseEnvelopeV1>(cliResponseEnvelope, out var cliResponse));
        Assert.NotNull(cliResponse);
        Assert.Equal(ControlCommandRoundtripStatus.Completed, cliResponse!.Status);
        Assert.NotNull(cliResponse.CommandResult);
        Assert.Equal("cli-control-req-1", cliResponse.CommandResult!.RequestId);
        Assert.Equal("{\"pendingQueueLength\":0}", cliResponse.CommandResult.PayloadJson);
        Assert.Equal(WebSocketState.Open, unitySocket.State);
    }

    private static CliControlHarness CreateHarness(Uri controlUri)
    {
        var logStore = new LogStore(128);
        var hierarchyStore = new HierarchyStore();
        var inspectorStore = new InspectorStore();
        var telemetryStore = new TelemetryStore();
        var commandStore = new CommandStore();
        var capabilityHandshakeService = new CapabilityHandshakeService();
        var capabilityStateStore = new CapabilityStateStore(capabilityHandshakeService.LocalSupportedCapabilities);
        var transport = new DebugStudioServerSessionTransport(new DebugStudioServerOptions
        {
            AcceptTimeoutSeconds = 10,
        });

        var resetPolicy = new SessionResetPolicy(
            logStore,
            hierarchyStore,
            inspectorStore,
            telemetryStore,
            commandStore,
            capabilityStateStore);
        var messageRouter = new SessionMessageRouter(
            logStore,
            hierarchyStore,
            inspectorStore,
            telemetryStore,
            commandStore,
            capabilityStateStore);
        var capabilityCoordinator = new SessionCapabilityCoordinator(
            transport,
            capabilityHandshakeService,
            capabilityStateStore);
        var sessionService = new SessionService(
            transport,
            resetPolicy,
            messageRouter,
            capabilityCoordinator);
        var commandService = new CommandService(sessionService, capabilityStateStore, commandStore);
        var controlService = new DebugStudioCliControlService(
            sessionService,
            commandService,
            new DebugStudioCliControlOptions
            {
                ControlUri = controlUri,
                AcceptTimeoutSeconds = 5,
            });

        controlService.StartAsync().GetAwaiter().GetResult();
        return new CliControlHarness(sessionService, controlService);
    }

    private static async Task SendMessageAsync<TPayload>(
        ClientWebSocket socket,
        DebugSocketMessageType messageType,
        TPayload payload,
        string? requestId = null)
    {
        var frame = DebugSocketProtocol.SerializeMessage(messageType, payload, requestId);
        await socket.SendAsync(
            new ArraySegment<byte>(frame),
            WebSocketMessageType.Binary,
            true,
            CancellationToken.None);
    }

    private static async Task<DebugSocketEnvelopeV1> ReceiveEnvelopeAsync(ClientWebSocket socket)
    {
        var buffer = new byte[8192];
        using var memoryStream = new MemoryStream();
        using var timeoutCts = new CancellationTokenSource(TestTimeout);

        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeoutCts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("The socket closed before a binary message was received.");
            }

            memoryStream.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                continue;
            }

            var frame = memoryStream.ToArray();
            Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(frame, out var envelope));
            Assert.NotNull(envelope);
            return envelope!;
        }
    }

    private static int AllocateFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private sealed class CliControlHarness : IAsyncDisposable
    {
        public CliControlHarness(SessionService sessionService, DebugStudioCliControlService controlService)
        {
            SessionService = sessionService;
            ControlService = controlService;
        }

        public SessionService SessionService { get; }

        public DebugStudioCliControlService ControlService { get; }

        public async ValueTask DisposeAsync()
        {
            await ControlService.DisposeAsync().ConfigureAwait(false);
            await SessionService.DisposeAsync().ConfigureAwait(false);
        }
    }
}
