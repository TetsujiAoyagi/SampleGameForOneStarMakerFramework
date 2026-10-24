#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DebugStudio.App.Core.Infrastructure;

public sealed class OrderedAsyncDisposable : IAsyncDisposable
{
    private readonly IReadOnlyList<IAsyncDisposable> _disposables;
    private readonly object _gate = new();
    private Task? _disposeTask;

    public OrderedAsyncDisposable(params IAsyncDisposable[] disposables)
    {
        _disposables = disposables ?? Array.Empty<IAsyncDisposable>();
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate) return new ValueTask(_disposeTask ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        List<Exception>? errors = null;
        foreach (var disposable in _disposables)
        {
            if (disposable != null)
            {
                try { await disposable.DisposeAsync().ConfigureAwait(false); }
                catch (Exception ex)
                {
                    Trace.TraceError($"Disposing {disposable.GetType().Name} failed: {ex}");
                    (errors ??= new List<Exception>()).Add(ex);
                }
            }
        }
        if (errors != null) throw new AggregateException(errors);
    }
}
