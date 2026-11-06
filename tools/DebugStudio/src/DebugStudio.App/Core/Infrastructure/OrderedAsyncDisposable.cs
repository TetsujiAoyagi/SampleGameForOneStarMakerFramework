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
        // 複数の Close 経路が同時に来ても同じ Task を共有し、所有物を二重解放しない。
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
                    // 先行する一つの解放失敗で残りの購読や transport を残さない。
                    // 全件を順序どおり試した後にまとめて通知する。
                    Trace.TraceError($"Disposing {disposable.GetType().Name} failed: {ex}");
                    (errors ??= new List<Exception>()).Add(ex);
                }
            }
        }
        if (errors != null) throw new AggregateException(errors);
    }
}
