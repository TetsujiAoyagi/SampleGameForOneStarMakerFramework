#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Server;

namespace DebugStudio.App.Tests;

/// <summary>
/// DebugStudio server-inversion 経路を、実 transport と app store 群を通して縦に検証する。
/// Unity 実行環境は使えないため、WebSocket peer だけを実クライアントとして模擬する。
/// </summary>
public sealed class ServerInversionVerticalSliceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ServerInversion_hello_welcome_command_resultが実スタックを通って相関更新される()
    {
        var port = AllocateFreePort();
        var serverUri = new Uri($"ws://127.0.0.1:{port}/debugsocket/");

        await using var harness = CreateHarness();
        using var clientSocket = new ClientWebSocket();

        var stateSnapshots = new List<DebugSocketConnectionState>();
        var listeningStarted = new TaskCompletionSource<DebugSocketConnectionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var connected = new TaskCompletionSource<DebugSocketConnectionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var welcomeReceived = new TaskCompletionSource<CapabilityHandshakeWelcomeEnvelopeV1>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandResultApplied = new TaskCompletionSource<CommandStoreSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        harness.SessionService.ConnectionStateChanged += snapshot =>
        {
            lock (stateSnapshots)
            {
                stateSnapshots.Add(snapshot.State);
            }

            if (snapshot.State == DebugSocketConnectionState.Connecting)
            {
                listeningStarted.TrySetResult(snapshot);
            }

            if (snapshot.State == DebugSocketConnectionState.Connected)
            {
                connected.TrySetResult(snapshot);
            }
        };

        harness.SessionService.CapabilityWelcomeReceived += welcome =>
        {
            welcomeReceived.TrySetResult(welcome);
        };

        var expectedRequestId = string.Empty;
        harness.CommandStore.Changed += snapshot =>
        {
            // CommandStore は pending 追加時にもイベントを出すため、
            // 今回待ちたい「result が同一 requestId へ相関済み」の状態だけを絞って捕まえる。
            if (!string.IsNullOrWhiteSpace(expectedRequestId) &&
                snapshot.LatestResult?.RequestId == expectedRequestId &&
                snapshot.PendingCount == 0)
            {
                commandResultApplied.TrySetResult(snapshot);
            }
        };

        var connectTask = harness.SessionService.ConnectAsync(new DebugSocketClientOptions
        {
            ServerUri = serverUri,
        });

        var listeningSnapshot = await listeningStarted.Task.WaitAsync(TestTimeout);
        Assert.Equal(DebugSocketConnectionState.Connecting, listeningSnapshot.State);
        Assert.Contains("Waiting for an inbound DebugStudio.Server WebSocket", listeningSnapshot.Detail, StringComparison.Ordinal);

        await clientSocket.ConnectAsync(serverUri, CancellationToken.None);
        await connectTask;
        await connected.Task.WaitAsync(TestTimeout);

        var helloEnvelope = await ReceiveEnvelopeAsync(clientSocket);
        Assert.Equal((int)DebugSocketMessageType.CapabilityHello, helloEnvelope.MessageType);
        Assert.True(DebugSocketProtocol.TryDeserializePayload<CapabilityHandshakeHelloEnvelopeV1>(helloEnvelope, out var hello));
        Assert.NotNull(hello);
        Assert.Equal("DebugStudio.App", hello!.ClientName);
        Assert.Equal(1, hello.MinSchemaVersion);
        Assert.Equal(1, hello.MaxSchemaVersion);
        Assert.True((hello.SupportedCapabilities & DebugStudioCapability.DebugCommand) == DebugStudioCapability.DebugCommand);
        Assert.True((hello.SupportedCapabilities & DebugStudioCapability.CommandResult) == DebugStudioCapability.CommandResult);

        var welcome = new CapabilityHandshakeWelcomeEnvelopeV1
        {
            SessionId = "unity-session-vertical-slice",
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
            TimestampUnixTimeMilliseconds = 1234567890,
            StatusMessage = "Negotiated command loopback for vertical slice.",
        };

        await SendMessageAsync(clientSocket, DebugSocketMessageType.CapabilityWelcome, welcome);

        var receivedWelcome = await welcomeReceived.Task.WaitAsync(TestTimeout);
        Assert.Equal("unity-session-vertical-slice", receivedWelcome.SessionId);

        var capabilitySnapshot = harness.CapabilityStateStore.GetSnapshot();
        Assert.Equal("Negotiated", capabilitySnapshot.HandshakeState);
        Assert.Equal("Unity Editor", capabilitySnapshot.RemoteName);
        Assert.Equal("unity-session-vertical-slice", capabilitySnapshot.SessionId);
        Assert.True(harness.CapabilityStateStore.Supports(DebugStudioCapability.DebugCommand));
        Assert.True(harness.CapabilityStateStore.Supports(DebugStudioCapability.CommandResult));

        expectedRequestId = await harness.CommandService.SendAsync(
            "debugsocket.runtime-diagnostics",
            "{\"scope\":\"vertical-slice\"}");

        var pendingSnapshot = harness.CommandStore.GetSnapshot();
        Assert.Equal(1, pendingSnapshot.PendingCount);
        Assert.Equal(CommandDispatchState.Pending, pendingSnapshot.Entries[0].State);
        Assert.Equal(expectedRequestId, pendingSnapshot.Entries[0].RequestId);

        var commandEnvelope = await ReceiveEnvelopeAsync(clientSocket);
        Assert.Equal((int)DebugSocketMessageType.DebugCommand, commandEnvelope.MessageType);
        Assert.Equal(expectedRequestId, commandEnvelope.RequestId);
        Assert.True(DebugSocketProtocol.TryDeserializePayload<DebugCommandEnvelopeV1>(commandEnvelope, out var command));
        Assert.NotNull(command);
        Assert.Equal("debugsocket.runtime-diagnostics", command!.CommandType);
        Assert.Equal("{\"scope\":\"vertical-slice\"}", command.PayloadJson);

        var result = new DebugCommandResultEnvelopeV1
        {
            RequestId = expectedRequestId,
            Success = true,
            Message = "Runtime diagnostics captured.",
            PayloadJson = "{\"pendingQueueLength\":0,\"droppedQueueOverflowCount\":0}",
        };

        await SendMessageAsync(clientSocket, DebugSocketMessageType.CommandResult, result, expectedRequestId);

        var completedSnapshot = await commandResultApplied.Task.WaitAsync(TestTimeout);
        Assert.Equal(0, completedSnapshot.PendingCount);
        Assert.Equal(1, completedSnapshot.ResultCount);
        Assert.Equal(CommandDispatchState.Succeeded, completedSnapshot.Entries[0].State);
        Assert.Equal(expectedRequestId, completedSnapshot.Entries[0].RequestId);
        Assert.Equal(result.PayloadJson, completedSnapshot.Entries[0].ResultPayloadJson);
        Assert.Equal(expectedRequestId, completedSnapshot.LatestResult?.RequestId);

        lock (stateSnapshots)
        {
            Assert.Contains(DebugSocketConnectionState.Connecting, stateSnapshots);
            Assert.Contains(DebugSocketConnectionState.Connected, stateSnapshots);
        }

        // server 側 receive loop が Close を観測できるよう、先に peer socket を閉じる。
        // Disconnect 前に閉じないと Accept/Receive 待ちが残り、testhost 終了がハングする。
        if (clientSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await clientSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "vertical-slice-complete",
                CancellationToken.None);
        }

        await harness.SessionService.DisconnectAsync();
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
                throw new InvalidOperationException("The client socket closed before a binary message was received.");
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

    private static VerticalSliceHarness CreateHarness()
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

        return new VerticalSliceHarness(sessionService, capabilityStateStore, commandStore, commandService);
    }

    private static int AllocateFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private sealed class VerticalSliceHarness : IAsyncDisposable
    {
        public VerticalSliceHarness(
            SessionService sessionService,
            CapabilityStateStore capabilityStateStore,
            CommandStore commandStore,
            CommandService commandService)
        {
            SessionService = sessionService;
            CapabilityStateStore = capabilityStateStore;
            CommandStore = commandStore;
            CommandService = commandService;
        }

        public SessionService SessionService { get; }
        public CapabilityStateStore CapabilityStateStore { get; }
        public CommandStore CommandStore { get; }
        public CommandService CommandService { get; }

        public ValueTask DisposeAsync() => SessionService.DisposeAsync();
    }
}
