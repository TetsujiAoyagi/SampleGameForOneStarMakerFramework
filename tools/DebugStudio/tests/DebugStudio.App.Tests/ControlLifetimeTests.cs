#nullable enable

using DebugStudio.App.Core.Infrastructure;

namespace DebugStudio.App.Tests;

public sealed class ControlLifetimeTests
{
    [Fact]
    public async Task Ordered_cleanup_attempts_every_member_once_and_shares_failure()
    {
        var order = new List<string>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifetime = new OrderedAsyncDisposable(
            new Cleanup(async () => { order.Add("auto-listen"); await release.Task; }),
            new Cleanup(() => { order.Add("control"); throw new InvalidOperationException("control cleanup"); }),
            new Cleanup(() => { order.Add("persistence"); return Task.CompletedTask; }),
            new Cleanup(() => { order.Add("vm"); return Task.CompletedTask; }));
        var first = lifetime.DisposeAsync().AsTask();
        var second = lifetime.DisposeAsync().AsTask();
        Assert.Same(first, second);
        release.SetResult();
        await Assert.ThrowsAsync<AggregateException>(() => first);
        await Assert.ThrowsAsync<AggregateException>(() => lifetime.DisposeAsync().AsTask());
        Assert.Equal(new[] { "auto-listen", "control", "persistence", "vm" }, order);
    }

    private sealed class Cleanup(Func<Task> cleanup) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new(cleanup());
    }
}
