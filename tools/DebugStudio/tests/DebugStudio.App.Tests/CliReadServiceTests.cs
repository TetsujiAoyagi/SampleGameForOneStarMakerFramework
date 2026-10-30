#nullable enable

using System.Reflection;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Tests;

public sealed class CliReadServiceTests
{
    [Fact]
    public async Task Hierarchy_initial_clear_and_received_empty_are_distinct_even_disconnected()
    {
        var h = new Harness();
        Assert.False((await h.Read("hierarchy")).Hierarchy!.HasSnapshot);
        h.Hierarchy.ApplySnapshot(new() { Revision = 1, CapturedAtUnixTimeMilliseconds = 50 });
        var retained = await h.Read("hierarchy");
        Assert.Equal("ok", retained.Status);
        Assert.True(retained.Hierarchy!.HasSnapshot);
        Assert.Empty(retained.Hierarchy.Nodes);
        h.Hierarchy.Clear();
        Assert.False((await h.Read("hierarchy")).Hierarchy!.HasSnapshot);
    }

    [Theory]
    [InlineData(InspectorDetailState.Ready, "ok", "ready")]
    [InlineData(InspectorDetailState.NotFound, "error", "not-found")]
    [InlineData(InspectorDetailState.Faulted, "error", "faulted")]
    [InlineData(InspectorDetailState.Unsupported, "error", "unsupported")]
    public async Task Inspect_accepts_first_same_target_detail_without_BeginQuery(InspectorDetailState state, string status, string reason)
    {
        var h = new Harness();
        h.Ready();
        h.Inspector.BeginQuery(42, "GUI target", null);
        var read = h.Read("inspect");
        Assert.Equal(1, h.Subscriptions());
        h.Transport.Detail(new() { TargetId = 43, State = InspectorDetailState.Ready });
        Assert.False(read.IsCompleted);
        // The wire detail has no query origin. The same-target GUI response may satisfy this read.
        h.Transport.Detail(new() { TargetId = 42, State = state, TargetName = "GUI refreshed",
            Sections = [new() { Properties = [new() { ValueTypeId = 1, RawValue = "true" }] }] });
        var response = await read;
        Assert.Equal(status, response.Status);
        Assert.Equal(reason, response.Reason);
        Assert.Equal(42, h.Inspector.GetRetainedSnapshot().State.TargetId);
        Assert.Equal("GUI refreshed", h.Inspector.GetRetainedSnapshot().State.TargetName);
        Assert.Equal("true", response.Inspector!.Sections[0].Properties[0].RawValue);
        Assert.Equal(0, h.Subscriptions());
        Assert.Equal(InspectorQueryFlags.IncludeMetadata | InspectorQueryFlags.IncludeComponents |
            InspectorQueryFlags.IncludeProperties | InspectorQueryFlags.IncludeRawValues, h.Transport.Query!.QueryFlags);
    }

    [Theory]
    [InlineData("timeout", true)]
    [InlineData("cancelled", true)]
    [InlineData("disconnected", true)]
    [InlineData("timeout", false)]
    [InlineData("cancelled", false)]
    [InlineData("disconnected", false)]
    public async Task Inspect_cleanup_releases_all_request_subscriptions_including_stalled_send(string reason, bool stallSend)
    {
        var h = new Harness(); h.Ready();
        h.Transport.StallSend = stallSend;
        using var cancel = new CancellationTokenSource();
        var read = h.Service.ReadAsync(new() { Operation = "inspect", NodeId = 42 }, cancel.Token);
        await h.Transport.Sent.Task;
        if (reason == "timeout") h.Time.Fire();
        else if (reason == "cancelled") cancel.Cancel();
        else h.Transport.Disconnect();
        Assert.Equal(reason, (await read.WaitAsync(TimeSpan.FromSeconds(5))).Reason);
        Assert.True(h.Transport.SendToken.IsCancellationRequested);
        Assert.Equal(0, h.Subscriptions());
        Assert.Equal(0, h.ConnectionSubscriptions());
    }

    [Fact]
    public async Task Inspect_does_not_change_gui_target_and_rejects_unconnected_or_unnegotiated_before_send()
    {
        var h = new Harness();
        Assert.Equal("disconnected", (await h.Read("inspect")).Reason);
        h.Transport.StateValue = DebugSocketConnectionState.Connected;
        Assert.Equal("unsupported", (await h.Read("inspect")).Reason);
        Assert.False(h.Transport.Sent.Task.IsCompleted);
        h.Ready(); h.Inspector.BeginQuery(77, "selected", null);
        var read = h.Read("inspect");
        Assert.Equal(77, h.Inspector.GetRetainedSnapshot().State.TargetId);
        h.Transport.Detail(new() { TargetId = 42, State = InspectorDetailState.Ready });
        await read;
        Assert.Equal(77, h.Inspector.GetRetainedSnapshot().State.TargetId);
    }

    [Fact]
    public async Task Send_failure_and_invalid_request_are_finite_and_cleanup()
    {
        var h = new Harness(); h.Ready(); h.Transport.ThrowSend = true;
        Assert.Equal("faulted", (await h.Read("inspect")).Reason);
        Assert.Equal(0, h.Subscriptions());
        Assert.Equal("invalid-request", (await h.Service.ReadAsync(new() { Operation = "inspect", NodeId = 42, TimeoutMilliseconds = 60001 })).Reason);
    }

    private sealed class Harness
    {
        public FakeTransport Transport { get; } = new();
        public HierarchyStore Hierarchy { get; } = new();
        public InspectorStore Inspector { get; } = new();
        public ManualTime Time { get; } = new();
        public CapabilityStateStore Capabilities { get; }
        public SessionService Session { get; }
        public CliReadService Service { get; }
        public Harness()
        {
            var logs = new LogStore(10); var telemetry = new TelemetryStore(); var commands = new CommandStore();
            var handshake = new CapabilityHandshakeService();
            Capabilities = new(handshake.LocalSupportedCapabilities);
            Session = new(Transport, new(logs, Hierarchy, Inspector, telemetry, commands, Capabilities),
                new(logs, Hierarchy, Inspector, telemetry, commands, Capabilities, new TelemetrySessionAttributesStore()),
                new(Transport, handshake, Capabilities));
            Service = new(Session, Hierarchy, Capabilities, Time);
        }
        public void Ready()
        {
            Transport.StateValue = DebugSocketConnectionState.Connected;
            Capabilities.ApplyWelcome(new() { SelectedSchemaVersion = 1,
                NegotiatedCapabilities = DebugStudioCapability.InspectorQuery | DebugStudioCapability.InspectorDetail });
        }
        public Task<ControlReadResponseEnvelopeV1> Read(string op) => Service.ReadAsync(new() { Operation = op, NodeId = 42 });
        public int Subscriptions() => Count("InspectorDetailReceived");
        public int ConnectionSubscriptions() => Count("ConnectionStateChanged");
        private int Count(string name) => ((Delegate?)typeof(SessionService).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Session))?.GetInvocationList().Length ?? 0;
    }
    private sealed class ManualTime : TimeProvider
    {
        private TimerCallback? _callback; private object? _state;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { _callback = callback; _state = state; return new TimerStub(); }
        public void Fire() => _callback!(_state);
        private sealed class TimerStub : ITimer
        { public bool Change(TimeSpan dueTime, TimeSpan period) => true; public void Dispose() { } public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }
    private sealed class FakeTransport : ISessionTransport
    {
        public DebugSocketConnectionState StateValue;
        public DebugSocketConnectionState State => StateValue;
        public TaskCompletionSource Sent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool StallSend; public bool ThrowSend;
        public CancellationToken SendToken;
        public InspectorQueryEnvelopeV1? Query;
        public event Action<DebugSocketConnectionSnapshot>? ConnectionStateChanged;
        public event Action<InspectorDetailEnvelopeV1>? InspectorDetailReceived;
#pragma warning disable CS0067
        public event Action<LogEnvelopeV1>? LogReceived;
        public event Action<DebugTelemetryEnvelopeV1>? TelemetryReceived;
        public event Action<DebugSocketServiceStatusEnvelopeV1>? ServiceStatusReceived;
        public event Action<DebugCommandResultEnvelopeV1>? CommandResultReceived;
        public event Action<CapabilityHandshakeWelcomeEnvelopeV1>? CapabilityWelcomeReceived;
        public event Action<HierarchySnapshotEnvelopeV1>? HierarchySnapshotReceived;
        public event Action<HierarchyDeltaEnvelopeV1>? HierarchyDeltaReceived;
#pragma warning restore CS0067
        public void Detail(InspectorDetailEnvelopeV1 d) => InspectorDetailReceived?.Invoke(d);
        public void Disconnect()
        { StateValue = DebugSocketConnectionState.Disconnected; ConnectionStateChanged?.Invoke(new(StateValue, null, "closed", DateTimeOffset.Now)); }
        public Task SendMessageAsync<T>(DebugSocketMessageType type, T payload, string? requestId = null, CancellationToken cancellationToken = default)
        {
            SendToken = cancellationToken; Query = payload as InspectorQueryEnvelopeV1; Sent.TrySetResult();
            if (ThrowSend) throw new InvalidOperationException("send failure");
            return StallSend ? new TaskCompletionSource().Task : Task.CompletedTask;
        }
        public Task ConnectAsync(DebugSocketClientOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendCommandAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
