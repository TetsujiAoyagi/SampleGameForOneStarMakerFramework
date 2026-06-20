#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DebugStudio.App.Core.Infrastructure;

public sealed class OrderedAsyncDisposable : IAsyncDisposable
{
    private readonly IReadOnlyList<IAsyncDisposable> _disposables;

    public OrderedAsyncDisposable(params IAsyncDisposable[] disposables)
    {
        _disposables = disposables ?? Array.Empty<IAsyncDisposable>();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            if (disposable != null)
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
