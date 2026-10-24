#nullable enable

using System.Net.WebSockets;
using System.Threading.Channels;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Tests;

public sealed class CliControlLifetimeTests
{
    [Theory]
    [InlineData("frame", 15)]
    [InlineData("send", 3)]
    [InlineData("close", 3)]
    public async Task Stalled_peer_is_aborted_at_deadline_and_next_read_returns(string stage, int seconds)
    {
        await using var harness = new Harness();
        using var socket = new ScriptedSocket { StallSend = stage == "send", StallClose = stage == "close" };
        if (stage == "frame")
        {
            socket.Frames.Enqueue((new byte[] { 1 }, false, WebSocketMessageType.Binary));
            socket.Frames.Enqueue((new byte[] { 2 }, false, WebSocketMessageType.Binary));
        }
        else EnqueueRead(socket);
        var handling = harness.Control.HandleControlConnectionAsync(socket, CancellationToken.None);
        var deadline = await harness.Deadlines.Reader.ReadAsync();
        if (stage == "send") deadline = await harness.Deadlines.Reader.ReadAsync();
        if (stage == "close")
        {
            await harness.Deadlines.Reader.ReadAsync(); // send
            await harness.Deadlines.Reader.ReadAsync(); // close frame
            deadline = await harness.Deadlines.Reader.ReadAsync(); // close handshake
        }
        await (stage switch { "frame" => socket.BlockedReceive.Task, "send" => socket.Sending.Task, _ => socket.Closing.Task });
        Assert.Equal(TimeSpan.FromSeconds(seconds), deadline.Timeout);
        Assert.False(harness.Deadlines.Reader.TryRead(out _)); // Fragments did not renew the frame deadline.
        deadline.Cancellation.Cancel();
        if (stage == "close") await handling;
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handling);
        Assert.True(socket.Aborted);
        Assert.True(socket.Disposed);
        if (stage == "close") Assert.Single(socket.Sent); // Close failure leaves the determined result intact.

        using var next = new ScriptedSocket();
        EnqueueRead(next);
        await harness.Control.HandleControlConnectionAsync(next, CancellationToken.None);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(Assert.Single(next.Sent), out var envelope));
        Assert.True(DebugSocketProtocol.TryDeserializePayload<ControlReadResponseEnvelopeV1>(envelope!, out var response));
        Assert.Equal("no-snapshot", response!.Reason);
        Assert.True(next.Disposed);
    }

    [Fact]
    public async Task Shutdown_cancels_receive_and_disposes_socket()
    {
        await using var harness = new Harness();
        using var socket = new ScriptedSocket();
        using var shutdown = new CancellationTokenSource();
        var handling = harness.Control.HandleControlConnectionAsync(socket, shutdown.Token);
        await socket.BlockedReceive.Task;
        shutdown.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handling);
        Assert.True(socket.Aborted);
        Assert.True(socket.Disposed);
    }

    private static void EnqueueRead(ScriptedSocket socket)
    {
        var frame = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.ControlReadRequest,
            new ControlReadRequestEnvelopeV1 { RequestId = "read", Operation = "hierarchy" }, "read");
        socket.Frames.Enqueue((frame, true, WebSocketMessageType.Binary));
        socket.Frames.Enqueue((Array.Empty<byte>(), true, WebSocketMessageType.Close));
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SessionService _session;
        public Channel<(TimeSpan Timeout, CancellationTokenSource Cancellation)> Deadlines { get; } =
            Channel.CreateUnbounded<(TimeSpan, CancellationTokenSource)>();
        public DebugStudioCliControlService Control { get; }

        public Harness()
        {
            var log = new LogStore(10);
            var hierarchy = new HierarchyStore();
            var inspector = new InspectorStore();
            var telemetry = new TelemetryStore();
            var commands = new CommandStore();
            var handshake = new CapabilityHandshakeService();
            var capabilities = new CapabilityStateStore(handshake.LocalSupportedCapabilities);
            var transport = new DebugStudioServerSessionTransport();
            _session = new SessionService(transport,
                new SessionResetPolicy(log, hierarchy, inspector, telemetry, commands, capabilities),
                new SessionMessageRouter(log, hierarchy, inspector, telemetry, commands, capabilities,
                    new TelemetrySessionAttributesStore()),
                new SessionCapabilityCoordinator(transport, handshake, capabilities));
            Control = new DebugStudioCliControlService(_session,
                new CommandService(_session, capabilities, commands), null,
                new CliReadService(_session, hierarchy, capabilities), (timeout, token) =>
                {
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    Deadlines.Writer.TryWrite((timeout, cts));
                    return cts;
                });
        }

        public async ValueTask DisposeAsync()
        {
            await Control.DisposeAsync();
            await _session.DisposeAsync();
        }
    }

    private sealed class ScriptedSocket : WebSocket
    {
        private WebSocketState _state = WebSocketState.Open;
        public Queue<(byte[] Data, bool End, WebSocketMessageType Type)> Frames { get; } = new();
        public List<byte[]> Sent { get; } = new();
        public TaskCompletionSource BlockedReceive { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Sending { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Closing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool StallSend { get; init; }
        public bool StallClose { get; init; }
        public bool Aborted { get; private set; }
        public bool Disposed { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;
        public override void Abort() { Aborted = true; _state = WebSocketState.Aborted; }
        public override void Dispose() { Disposed = true; }
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken token) =>
            CloseAsync(status, description, token);
        public override async Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken token)
        {
            Closing.TrySetResult();
            if (StallClose) await new TaskCompletionSource().Task.WaitAsync(token);
            _state = WebSocketState.Closed;
        }
        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token)
        {
            if (Frames.TryDequeue(out var frame))
            {
                frame.Data.CopyTo(buffer.AsSpan());
                return new WebSocketReceiveResult(frame.Data.Length, frame.Type, frame.End);
            }
            BlockedReceive.TrySetResult();
            return await new TaskCompletionSource<WebSocketReceiveResult>().Task.WaitAsync(token);
        }
        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool endOfMessage,
            CancellationToken token)
        {
            Sending.TrySetResult();
            if (StallSend) await new TaskCompletionSource().Task.WaitAsync(token);
            Sent.Add(buffer.ToArray());
        }
    }
}
