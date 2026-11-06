#nullable enable

using System.Net.WebSockets;
using DebugStudio.Client;

namespace DebugStudio.Cli.Tests;

public sealed class ControlClientCloseTests
{
    [Fact]
    public async Task Client_close_deadline_aborts_an_unresponsive_peer()
    {
        var time = new ManualTime();
        using var socket = new StalledCloseSocket();
        var close = ControlClientClose.TryCloseAsync(socket, time);
        await socket.Entered.Task;
        time.Fire();
        await close.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(socket.Aborted);
        Assert.True(socket.CloseToken.IsCancellationRequested);
    }

    private sealed class ManualTime : TimeProvider
    {
        private TimerCallback? _callback;
        private object? _state;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { _callback = callback; _state = state; return new TimerStub(); }
        public void Fire() => _callback!(_state);
        private sealed class TimerStub : ITimer
        { public bool Change(TimeSpan dueTime, TimeSpan period) => true; public void Dispose() { } public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }
    private sealed class StalledCloseSocket : WebSocket
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Aborted { get; private set; }
        public CancellationToken CloseToken { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => Aborted ? WebSocketState.Aborted : WebSocketState.Open;
        public override string? SubProtocol => null;
        public override void Abort() => Aborted = true;
        public override Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken cancellationToken)
        { CloseToken = cancellationToken; Entered.TrySetResult(); return new TaskCompletionSource().Task; }
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken token) => throw new NotSupportedException();
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token) => throw new NotSupportedException();
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool endOfMessage, CancellationToken token) => throw new NotSupportedException();
        public override void Dispose() { }
    }
}
