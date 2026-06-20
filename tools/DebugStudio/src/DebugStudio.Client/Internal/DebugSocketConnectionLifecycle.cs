#nullable enable

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Client.Internal;

/// <summary>
/// WebSocket 接続のライフサイクル管理。
/// 接続/切断処理を排他制御し、socket インスタンスの生存を一元管理する。
/// </summary>
internal sealed class DebugSocketConnectionLifecycle : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _sessionCts;
    private bool _disposed;

    public ClientWebSocket? CurrentSocket
    {
        get
        {
            _gate.Wait();
            try
            {
                return _socket;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public bool IsConnected
    {
        get
        {
            _gate.Wait();
            try
            {
                return _socket != null && _socket.State == WebSocketState.Open;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public CancellationToken SessionToken
    {
        get
        {
            _gate.Wait();
            try
            {
                return _sessionCts?.Token ?? CancellationToken.None;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public async Task<ClientWebSocket> ConnectAsync(
        DebugSocketClientOptions options,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(options);

        // connect / disconnect / send の競合中に _socket を半端な状態で見せないため、
        // 接続確立から current socket 公開までを gate 配下で直列化する。
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        ClientWebSocket? socket = null;
        CancellationTokenSource? sessionCts = null;

        try
        {
            if (_socket != null)
            {
                throw new InvalidOperationException("DebugStudio session is already connected.");
            }

            socket = new ClientWebSocket();
            socket.Options.KeepAliveInterval = options.KeepAliveInterval;

            if (!string.IsNullOrWhiteSpace(options.OriginHeader))
            {
                socket.Options.SetRequestHeader("Origin", options.OriginHeader);
            }

            // sessionCts は、この接続インスタンスに紐づく受信ループ全体を止めるための token source。
            sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            await socket.ConnectAsync(options.ServerUri, sessionCts.Token).ConfigureAwait(false);

            // 接続成功後にだけ current socket を差し替える。
            // これより前に _socket を公開すると、未接続 socket を他経路が掴む恐れがある。
            _socket = socket;
            _sessionCts = sessionCts;

            var result = socket;
            socket = null;
            sessionCts = null;

            return result;
        }
        catch
        {
            socket?.Dispose();
            sessionCts?.Dispose();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DisconnectResult> DisconnectAsync(CancellationToken cancellationToken)
    {
        ClientWebSocket? socket;
        CancellationTokenSource? sessionCts;

        // まず current 参照を外し、以後の send が古い socket を掴めないようにする。
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            socket = _socket;
            sessionCts = _sessionCts;

            _socket = null;
            _sessionCts = null;

            if (socket == null)
            {
                return new DisconnectResult(null, null);
            }
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            // receive loop に先に終了要求を出してから close handshake へ進む。
            sessionCts?.Cancel();

            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    // close 自体は best effort。ここが失敗しても disconnect 全体は完了させる。
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client-disconnect", cancellationToken)
                        .ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }
        finally
        {
            sessionCts?.Dispose();
        }

        return new DisconnectResult(socket, sessionCts);
    }

    public async Task<bool> TryAcquireGateAsync(CancellationToken cancellationToken)
    {
        // dispose 済みなら gate を取らせず、上流に「もう使えない」ことだけ返す。
        if (_disposed)
        {
            return false;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public void ReleaseGate()
    {
        _gate.Release();
    }

    public bool IsSameSocket(ClientWebSocket socket)
    {
        return ReferenceEquals(_socket, socket);
    }

    /// <summary>
    /// lifecycle gate を呼び出し側が保持している前提で、現在 socket を読む。
    /// 再度 gate を取りに行くと self-deadlock するため、send 経路ではこちらを使う。
    /// </summary>
    public ClientWebSocket? GetCurrentSocketUnsafe()
    {
        return _socket;
    }

    public void ClearSocketIfSame(ClientWebSocket socket)
    {
        if (ReferenceEquals(_socket, socket))
        {
            // 現役 socket を落とす時だけ session CTS も同時に破棄する。
            // すでに別 socket へ差し替わっている場合は、現役セッションを壊してはいけない。
            _socket = null;
            _sessionCts?.Dispose();
            _sessionCts = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        _gate.Dispose();
    }
}

internal readonly record struct DisconnectResult(ClientWebSocket? Socket, CancellationTokenSource? Cts);
