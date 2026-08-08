#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.App.Features.Commands;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Tests;

/// <summary>
/// command pending/result correlation の重要契約を検証する。
/// requestId 相関、timeout、disconnect、dispatch failure を中心に固定する。
/// </summary>
public sealed class CommandCorrelationTests
{
    [Fact]
    public void CommandStore_送信開始でPendingを追跡する()
    {
        var store = new CommandStore();
        var command = new DebugCommandEnvelopeV1
        {
            RequestId = "req-1",
            CommandType = "ping",
            PayloadJson = "{}",
        };

        var snapshot = store.TrackPending(command, 1000);

        Assert.Equal(1, snapshot.DispatchCount);
        Assert.Equal(1, snapshot.PendingCount);
        Assert.Single(snapshot.Entries);
        Assert.Equal(CommandDispatchState.Pending, snapshot.Entries[0].State);
    }

    [Fact]
    public void CommandStore_対応するresult到着でPendingが成功完了へ変わる()
    {
        var store = new CommandStore();
        store.TrackPending(new DebugCommandEnvelopeV1
        {
            RequestId = "req-1",
            CommandType = "ping",
            PayloadJson = "{}",
        }, 1000);

        var snapshot = store.AppendResult(new DebugCommandResultEnvelopeV1
        {
            RequestId = "req-1",
            Success = true,
            Message = "ok",
            PayloadJson = "{\"pong\":true}",
        });

        Assert.Equal(0, snapshot.PendingCount);
        Assert.Equal(1, snapshot.ResultCount);
        Assert.Equal(CommandDispatchState.Succeeded, snapshot.Entries[0].State);
        Assert.Contains("ok", snapshot.Entries[0].StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommandStore_未知requestIdのresultはOrphanedとして残す()
    {
        var store = new CommandStore();

        var snapshot = store.AppendResult(new DebugCommandResultEnvelopeV1
        {
            RequestId = "orphan-result",
            Success = false,
            Message = "no matching request",
            PayloadJson = "{}",
        });

        Assert.Single(snapshot.Entries);
        Assert.Equal(CommandDispatchState.Orphaned, snapshot.Entries[0].State);
        Assert.Equal("orphan-result", snapshot.Entries[0].RequestId);
    }

    [Fact]
    public void CommandStore_timeoutでPendingをTimedOutへ遷移させる()
    {
        var store = new CommandStore();
        store.TrackPending(new DebugCommandEnvelopeV1
        {
            RequestId = "timeout-1",
            CommandType = "slow-command",
            PayloadJson = "{}",
        }, 1000);

        var snapshot = store.ExpirePending(nowUnixTimeMilliseconds: 3000, timeoutMilliseconds: 1000);

        Assert.Equal(0, snapshot.PendingCount);
        Assert.Equal(CommandDispatchState.TimedOut, snapshot.Entries[0].State);
    }

    [Fact]
    public void CommandStore_disconnectでPendingをDisconnectedへ遷移させる()
    {
        var store = new CommandStore();
        store.TrackPending(new DebugCommandEnvelopeV1
        {
            RequestId = "req-1",
            CommandType = "ping",
            PayloadJson = "{}",
        }, 1000);

        var snapshot = store.MarkDisconnected("socket closed", 2000);

        Assert.Equal(0, snapshot.PendingCount);
        Assert.Equal(CommandDispatchState.Disconnected, snapshot.Entries[0].State);
        Assert.Contains("socket closed", snapshot.Entries[0].StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommandService_未交渉時は送信を拒否する()
    {
        var (_, _, _, _, _, commandService) = CreateHarness();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => commandService.SendAsync("ping", "{}"));

        Assert.Contains("advertised", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransportCommandSender_RequestId未指定でも相関付きで送信できる()
    {
        var (_, _, capabilityStateStore, commandStore, commandSender, _) = CreateHarness();
        capabilityStateStore.ApplyWelcome(new CapabilityHandshakeWelcomeEnvelopeV1
        {
            ServerName = "Unity",
            SessionId = "session-1",
            SelectedSchemaVersion = 1,
            ServerCapabilities = DebugStudioCapability.DebugCommand | DebugStudioCapability.CommandResult,
            NegotiatedCapabilities = DebugStudioCapability.DebugCommand | DebugStudioCapability.CommandResult,
        });

        var requestId = await commandSender.SendAsync("ping", "{}");
        commandStore.AppendResult(new DebugCommandResultEnvelopeV1
        {
            RequestId = requestId,
            Success = true,
            Message = "ok",
            PayloadJson = "{\"pong\":true}",
        });

        var snapshot = commandStore.GetSnapshot();
        Assert.StartsWith("ping-", requestId, StringComparison.Ordinal);
        Assert.Equal(0, snapshot.PendingCount);
        Assert.Equal(CommandDispatchState.Succeeded, snapshot.Entries[0].State);
    }

    [Fact]
    public async Task CommandService_送信失敗でもstoreにDispatchFailedを残す()
    {
        var (transport, _, capabilityStateStore, commandStore, _, commandService) = CreateHarness();
        capabilityStateStore.ApplyWelcome(new CapabilityHandshakeWelcomeEnvelopeV1
        {
            ServerName = "Unity",
            SessionId = "session-1",
            SelectedSchemaVersion = 1,
            ServerCapabilities = DebugStudioCapability.DebugCommand | DebugStudioCapability.CommandResult,
            NegotiatedCapabilities = DebugStudioCapability.DebugCommand | DebugStudioCapability.CommandResult,
        });
        transport.SendCommandException = new InvalidOperationException("DebugStudio session is not connected.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => commandService.SendAsync("ping", "{}"));

        var snapshot = commandStore.GetSnapshot();
        Assert.Equal(1, snapshot.DispatchCount);
        Assert.Equal(0, snapshot.PendingCount);
        Assert.Equal(CommandDispatchState.DispatchFailed, snapshot.Entries[0].State);
    }

    [Fact]
    public void CommandWindowViewModel_既定値はRuntimeDiagnosticsを送れる状態にある()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var (_, _, capabilityStateStore, commandStore, _, commandService) = CreateHarness();

        using var viewModel = new CommandWindowViewModel(dispatcher, commandStore, capabilityStateStore, commandService);

        Assert.Equal("debugsocket.runtime-diagnostics", viewModel.CommandType);
        Assert.Equal("{}", viewModel.PayloadJson);
        Assert.Contains("runtime-diagnostics", viewModel.BuiltInCommandGuide, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommandWindowViewModel_最新resultのPayloadJsonをそのまま表示する()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var (_, _, capabilityStateStore, commandStore, _, commandService) = CreateHarness();

        using var viewModel = new CommandWindowViewModel(dispatcher, commandStore, capabilityStateStore, commandService);

        commandStore.TrackPending(new DebugCommandEnvelopeV1
        {
            RequestId = "req-1",
            CommandType = "debugsocket.runtime-diagnostics",
            PayloadJson = "{}",
        }, 1000);

        commandStore.AppendResult(new DebugCommandResultEnvelopeV1
        {
            RequestId = "req-1",
            Success = true,
            Message = "Runtime diagnostics snapshot captured.",
            PayloadJson = "{\"pendingQueueLength\":0,\"droppedQueueOverflowCount\":0}",
        });

        Assert.Contains("Runtime diagnostics snapshot captured.", viewModel.LatestResult, StringComparison.Ordinal);
        Assert.Equal("{\"pendingQueueLength\":0,\"droppedQueueOverflowCount\":0}", viewModel.LatestPayloadJson);
    }

    private static (
        FakeSessionTransport Transport,
        SessionService SessionService,
        CapabilityStateStore CapabilityStateStore,
        CommandStore CommandStore,
        TransportCommandSender CommandSender,
        CommandService CommandService)
        CreateHarness()
    {
        var logStore = new LogStore(128);
        var hierarchyStore = new HierarchyStore();
        var inspectorStore = new InspectorStore();
        var telemetryStore = new TelemetryStore();
        var commandStore = new CommandStore();
        var capabilityHandshakeService = new CapabilityHandshakeService();
        var capabilityStateStore = new CapabilityStateStore(capabilityHandshakeService.LocalSupportedCapabilities);
        var transport = new FakeSessionTransport();
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
            capabilityStateStore,
            new TelemetrySessionAttributesStore());
        var capabilityCoordinator = new SessionCapabilityCoordinator(
            transport,
            capabilityHandshakeService,
            capabilityStateStore);
        var sessionService = new SessionService(
            transport,
            resetPolicy,
            messageRouter,
            capabilityCoordinator);
        var commandSender = new TransportCommandSender(sessionService, capabilityStateStore, commandStore);
        var commandService = new CommandService(commandSender);

        return (transport, sessionService, capabilityStateStore, commandStore, commandSender, commandService);
    }

#pragma warning disable CS0067
    private sealed class FakeSessionTransport : ISessionTransport
    {
        public DebugSocketConnectionState State { get; private set; } = DebugSocketConnectionState.Connected;

        public Exception? SendCommandException { get; set; }

        public event Action<DebugSocketConnectionSnapshot>? ConnectionStateChanged;
        public event Action<LogEnvelopeV1>? LogReceived;
        public event Action<DebugTelemetryEnvelopeV1>? TelemetryReceived;
        public event Action<DebugSocketServiceStatusEnvelopeV1>? ServiceStatusReceived;
        public event Action<DebugCommandResultEnvelopeV1>? CommandResultReceived;
        public event Action<CapabilityHandshakeWelcomeEnvelopeV1>? CapabilityWelcomeReceived;
        public event Action<HierarchySnapshotEnvelopeV1>? HierarchySnapshotReceived;
        public event Action<HierarchyDeltaEnvelopeV1>? HierarchyDeltaReceived;
        public event Action<InspectorDetailEnvelopeV1>? InspectorDetailReceived;

        public Task ConnectAsync(DebugSocketClientOptions options, CancellationToken cancellationToken = default)
        {
            State = DebugSocketConnectionState.Connected;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            State = DebugSocketConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        public Task SendCommandAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default)
        {
            return SendCommandException is null
                ? Task.CompletedTask
                : Task.FromException(SendCommandException);
        }

        public Task SendMessageAsync<TPayload>(
            DebugSocketMessageType messageType,
            TPayload payload,
            string? requestId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
#pragma warning restore CS0067
}
