#nullable enable

using System.Diagnostics;
using DebugStudio.Client;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// 明示的に有効化された場合だけ、Unity の切断後も同じ設定で待受けを再開する。
/// 再接続loopは所有するが、SessionService 自体の所有・破棄は行わない。
/// UI からの Start/Stop と App lifetime からの最終停止を同じloopへ集約する。
/// </summary>
public sealed class AutoListenService : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Func<DebugSocketClientOptions, CancellationToken, Task> _connect;
    private readonly Func<Task> _disconnect;
    private readonly Action<Action<DebugSocketConnectionSnapshot>> _subscribe;
    private readonly Action<Action<DebugSocketConnectionSnapshot>> _unsubscribe;
    private readonly Func<TimeSpan, CancellationToken, Task> _retryWait;
    private CancellationTokenSource? _cts;
    private Task _loop = Task.CompletedTask;
    private Task? _stop;
    private bool _disposed;

    public AutoListenService(SessionService session, Func<TimeSpan, CancellationToken, Task>? retryWait = null)
        : this(session.ConnectAsync, () => session.DisconnectAsync(),
            handler => session.ConnectionStateChanged += handler,
            handler => session.ConnectionStateChanged -= handler, retryWait) { }

    internal AutoListenService(
        Func<DebugSocketClientOptions, CancellationToken, Task> connect,
        Func<Task> disconnect,
        Action<Action<DebugSocketConnectionSnapshot>> subscribe,
        Action<Action<DebugSocketConnectionSnapshot>> unsubscribe,
        Func<TimeSpan, CancellationToken, Task>? retryWait = null)
    {
        _connect = connect;
        _disconnect = disconnect;
        _subscribe = subscribe;
        _unsubscribe = unsubscribe;
        _retryWait = retryWait ?? ((delay, token) => Task.Delay(delay, token));
    }

    public bool CanStart { get { lock (_gate) return !_disposed && _cts == null; } }
    public bool CanStop { get { lock (_gate) return !_disposed && _cts != null && _stop == null; } }
    public event Action? Changed;

    public void Start(DebugSocketClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (_gate)
        {
            if (_disposed || _cts != null) return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _loop = Task.Run(() => RunAsync(options, token));
        }
        Changed?.Invoke();
    }

    public Task StopAsync()
    {
        Task stop;
        lock (_gate)
        {
            if (_cts == null) return Task.CompletedTask;
            if (_stop != null) return _stop;
            // 先に loop へ停止意図を伝える。Disconnect は loop の終了後に行い、
            // retry 中の Connect と GUI の Stop が別々の接続操作を競合させない。
            _cts.Cancel();
            // _stop を設定してから停止処理を走らせる。Connect が既に同期完了していても、
            // StopAsync の再入呼出しが未設定の _stop を見て二つ目の停止処理を作らない。
            stop = _stop = Task.Run(StopCoreAsync);
        }
        Changed?.Invoke();
        return stop;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate) _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }

    private async Task StopCoreAsync()
    {
        try
        {
            // ConnectAsync が token を無視していても、loop の完了後に必ず transport を閉じる。
            // Dispose は SessionService の所有者ではないため、ここでは session 自体を破棄しない。
            try { await _loop.ConfigureAwait(false); }
            finally { await _disconnect().ConfigureAwait(false); }
        }
        finally
        {
            lock (_gate)
            {
                _cts?.Dispose();
                _cts = null;
                _stop = null;
            }
            Changed?.Invoke();
        }
    }

    private async Task RunAsync(DebugSocketClientOptions options, CancellationToken token)
    {
        var failures = 0;
        while (!token.IsCancellationRequested)
        {
            var terminal = new TaskCompletionSource<DebugSocketConnectionState>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnState(DebugSocketConnectionSnapshot snapshot)
            {
                if (snapshot.State is DebugSocketConnectionState.Disconnected or DebugSocketConnectionState.Faulted)
                    terminal.TrySetResult(snapshot.State);
            }
            var retry = false;
            // Connect より先に購読する。接続直後に Unity が切断しても終端通知を取りこぼさない。
            _subscribe(OnState);
            try
            {
                token.ThrowIfCancellationRequested();
                await _connect(options, token).ConfigureAwait(false);
                failures = 0;
                // Disconnected は通常の Unity 再起動を意味し、直ちに次の accept を始める。
                // Faulted だけを故障扱いにして backoff し、失敗時の接続連打を避ける。
                retry = await terminal.Task.WaitAsync(token).ConfigureAwait(false) == DebugSocketConnectionState.Faulted;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                Trace.TraceError($"Auto-listen failed: {ex}");
                retry = true;
            }
            finally { _unsubscribe(OnState); }

            if (retry)
            {
                var seconds = ++failures switch { 1 => 1, 2 => 2, _ => 5 };
                try { await _retryWait(TimeSpan.FromSeconds(seconds), token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            }
        }
    }
}
