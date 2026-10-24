#nullable enable

using System.Threading.Channels;
using DebugStudio.App.Core.Services;
using DebugStudio.Client;

namespace DebugStudio.App.Tests;

public sealed class AutoListenTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ConcurrentStart_uses_one_loop_and_natural_disconnect_relistens()
    {
        var fake = new Session();
        await using var service = fake.Create();
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => service.Start(new DebugSocketClientOptions()))));
        Assert.Equal(1, await fake.Attempts.Reader.ReadAsync().AsTask().WaitAsync(Limit));
        Assert.False(fake.Attempts.Reader.TryRead(out _));
        Assert.False(service.CanStart);
        Assert.True(service.CanStop);

        fake.Terminate(DebugSocketConnectionState.Disconnected);
        Assert.Equal(2, await fake.Attempts.Reader.ReadAsync().AsTask().WaitAsync(Limit));
        await service.StopAsync().WaitAsync(Limit);
        Assert.Equal(0, fake.Subscribers);
        Assert.Equal(1, fake.Disconnects);
        Assert.True(service.CanStart);
        Assert.False(service.CanStop);
        Assert.False(fake.Attempts.Reader.TryRead(out _));
    }

    [Fact]
    public async Task FaultRetry_is_stoppable_and_cancelled_wait_cannot_connect_again()
    {
        var fake = new Session { ConnectError = new InvalidOperationException("bind failed") };
        var retries = Channel.CreateUnbounded<(TimeSpan Delay, TaskCompletionSource Release)>();
        Task Wait(TimeSpan delay, CancellationToken token)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            retries.Writer.TryWrite((delay, release));
            return release.Task.WaitAsync(token);
        }
        await using var service = fake.Create(Wait);
        service.Start(new DebugSocketClientOptions());
        await fake.Attempts.Reader.ReadAsync().AsTask().WaitAsync(Limit);
        var retry = await retries.Reader.ReadAsync().AsTask().WaitAsync(Limit);
        Assert.Equal(TimeSpan.FromSeconds(1), retry.Delay);
        // Session transport remains Faulted here; stopping is governed by loop intent, not socket state.
        Assert.True(service.CanStop);
        Assert.False(service.CanStart);
        await service.StopAsync().WaitAsync(Limit);
        retry.Release.SetResult();
        Assert.False(fake.Attempts.Reader.TryRead(out _));
        Assert.Equal(0, fake.Subscribers);
    }

    [Fact]
    public async Task Retry_intervals_are_bounded_and_reset_after_connect()
    {
        var fake = new Session { ConnectError = new InvalidOperationException("bind failed") };
        var retries = Channel.CreateUnbounded<(TimeSpan Delay, TaskCompletionSource Release)>();
        Task Wait(TimeSpan delay, CancellationToken token)
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            retries.Writer.TryWrite((delay, release));
            return release.Task.WaitAsync(token);
        }
        await using var service = fake.Create(Wait);
        service.Start(new DebugSocketClientOptions());
        var failedAttempts = 0;
        foreach (var expected in new[] { 1, 2, 5, 5 })
        {
            await fake.Attempts.Reader.ReadAsync().AsTask().WaitAsync(Limit);
            var retry = await retries.Reader.ReadAsync().AsTask().WaitAsync(Limit);
            Assert.Equal(TimeSpan.FromSeconds(expected), retry.Delay);
            if (++failedAttempts == 4) fake.ConnectError = null;
            retry.Release.SetResult();
        }
        await fake.Attempts.Reader.ReadAsync().AsTask().WaitAsync(Limit);
        fake.Terminate(DebugSocketConnectionState.Faulted);
        var reset = await retries.Reader.ReadAsync().AsTask().WaitAsync(Limit);
        Assert.Equal(TimeSpan.FromSeconds(1), reset.Delay);
        await service.StopAsync().WaitAsync(Limit);
    }

    [Fact]
    public async Task Stop_cancels_pending_connect_and_dispose_prevents_restart()
    {
        var fake = new Session { BlockConnect = true };
        var service = fake.Create();
        service.Start(new DebugSocketClientOptions());
        await fake.Attempts.Reader.ReadAsync().AsTask().WaitAsync(Limit);
        var first = service.StopAsync();
        service.Start(new DebugSocketClientOptions());
        await first.WaitAsync(Limit);
        await service.DisposeAsync();
        service.Start(new DebugSocketClientOptions());
        Assert.False(service.CanStart);
        Assert.False(fake.Attempts.Reader.TryRead(out _));
        Assert.Equal(0, fake.Subscribers);
    }

    private sealed class Session
    {
        private Action<DebugSocketConnectionSnapshot>? _changed;
        private int _attempt;
        public readonly Channel<int> Attempts = Channel.CreateUnbounded<int>();
        public Exception? ConnectError;
        public bool BlockConnect;
        public int Disconnects;
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;

        public AutoListenService Create(Func<TimeSpan, CancellationToken, Task>? wait = null) => new(
            Connect,
            () => { Disconnects++; return Task.CompletedTask; },
            handler => _changed += handler,
            handler => _changed -= handler,
            wait);

        private Task Connect(DebugSocketClientOptions options, CancellationToken token)
        {
            var error = ConnectError;
            Attempts.Writer.TryWrite(Interlocked.Increment(ref _attempt));
            if (error != null) return Task.FromException(error);
            return BlockConnect ? new TaskCompletionSource().Task.WaitAsync(token) : Task.CompletedTask;
        }

        public void Terminate(DebugSocketConnectionState state)
            => _changed?.Invoke(new DebugSocketConnectionSnapshot(state, null, "test terminal", DateTimeOffset.UtcNow));
    }
}
