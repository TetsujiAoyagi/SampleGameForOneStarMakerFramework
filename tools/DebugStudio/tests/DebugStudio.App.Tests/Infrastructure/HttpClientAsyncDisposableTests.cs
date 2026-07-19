#nullable enable

using System.Net.Http;
using DebugStudio.App.Core.Infrastructure;

namespace DebugStudio.App.Tests.Infrastructure;

/// <summary>
/// composition 所有 HttpClient の終了処理が複数経路から呼ばれても安全であることを固定する。
/// </summary>
public sealed class HttpClientAsyncDisposableTests
{
    [Fact]
    public async Task DisposeAsync_二重呼び出しでもHttpClientを一度だけ安全に破棄する()
    {
        var client = new HttpClient();
        var lifetime = new HttpClientAsyncDisposable(client);

        await lifetime.DisposeAsync();
        await lifetime.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetAsync("http://localhost/"));
    }
}
