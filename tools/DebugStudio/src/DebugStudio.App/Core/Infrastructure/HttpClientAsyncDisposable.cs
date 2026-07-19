#nullable enable

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.App.Core.Infrastructure;

/// <summary>
/// app composition が生成して所有する <see cref="HttpClient"/> を
/// app lifetime の <see cref="IAsyncDisposable"/> へ接続する小さな adapter。
///
/// <para>
/// feature service は注入された client を所有・dispose しない。一方 composition が
/// new した client は終了時に必ず破棄する必要があるため、所有境界をここへ明示する。
/// </para>
/// </summary>
public sealed class HttpClientAsyncDisposable : IAsyncDisposable
{
    private HttpClient? _httpClient;

    public HttpClientAsyncDisposable(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// 複数の終了経路から呼ばれても一度だけ dispose する。
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _httpClient, null)?.Dispose();
        return ValueTask.CompletedTask;
    }
}
