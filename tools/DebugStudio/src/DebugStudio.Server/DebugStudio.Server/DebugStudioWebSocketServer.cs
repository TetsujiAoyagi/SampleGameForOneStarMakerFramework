#nullable enable

using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Server;

/// <summary>
/// DebugStudio が Unity からの接続を受け付ける single-session WebSocket server。
///
/// v1 の設計判断:
/// - 単一クライアントだけを扱う
/// - 新しい接続を受けたら古い接続を破棄して差し替える
/// - listen 開始と accept 待機を分離し、ライフサイクルを追いやすくする
/// </summary>
public sealed class DebugStudioWebSocketServer : IDisposable
{
    private readonly object _gate = new();
    private readonly DebugStudioServerOptions _options;
    private readonly Func<TimeSpan, CancellationTokenSource> _timeoutFactory;

    private HttpListener? _listener;
    private HttpListenerWebSocketContext? _currentContext;
    private WebSocket? _currentWebSocket;
    private bool _disposed;

    public DebugStudioWebSocketServer(DebugStudioServerOptions options)
        : this(options, timeout => new CancellationTokenSource(timeout)) { }

    internal DebugStudioWebSocketServer(DebugStudioServerOptions options, Func<TimeSpan, CancellationTokenSource> timeoutFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeoutFactory = timeoutFactory;
    }

    /// <summary>
    /// 現在の transport 状態。
    /// </summary>
    public DebugStudioServerTransportState State { get; private set; } = DebugStudioServerTransportState.Idle;

    /// <summary>
    /// HttpListener が待受中か。
    /// </summary>
    public bool IsListening => _listener?.IsListening == true;

    /// <summary>
    /// 現在アクティブな WebSocket。
    /// </summary>
    public WebSocket? CurrentSocket
    {
        get
        {
            lock (_gate)
            {
                return _currentWebSocket;
            }
        }
    }

    /// <summary>
    /// 現在の設定から導出される listener prefix。
    /// </summary>
    public string ListenerPrefix => _options.GetListenerPrefix();

    /// <summary>
    /// 待受を開始する。
    /// accept 自体は別メソッドへ分離し、listen 開始失敗と接続タイムアウトを区別しやすくする。
    /// </summary>
    public void StartListening()
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            if (_listener?.IsListening == true)
            {
                return;
            }

            var listener = new HttpListener();
            listener.Prefixes.Add(ListenerPrefix);
            listener.Start();

            _listener = listener;
            State = DebugStudioServerTransportState.Listening;
        }
    }

    /// <summary>
    /// WebSocket 接続を 1 件受け付ける。
    ///
    /// non-WebSocket request は 400 を返して待機継続し、
    /// timeout / cancel / socket fault を呼び出し元が区別できるようにする。
    /// </summary>
    public Task<HttpListenerWebSocketContext> AcceptWebSocketAsync(CancellationToken cancellationToken = default)
        => AcceptWebSocketAsync(cancellationToken, continueOnIdleTimeout: false);

    public async Task<HttpListenerWebSocketContext> AcceptWebSocketAsync(CancellationToken cancellationToken, bool continueOnIdleTimeout)
    {
        ThrowIfDisposed();

        HttpListener listener;
        lock (_gate)
        {
            listener = _listener ?? throw new InvalidOperationException("Server is not listening. Call StartListening() first.");
        }

        // The default Connect attempt owns one deadline, including ignored HTTP requests.
        // Opt-in listening uses renewable idle waits below and a separate handshake deadline.
        using var timeoutCts = _timeoutFactory(TimeSpan.FromSeconds(_options.AcceptTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HttpListenerContext httpContext;
            try
            {
                // Keep the same GetContext task after an idle deadline. A fresh task would leave
                // the old accept pending and could silently consume the next Unity connection.
                var pendingContext = listener.GetContextAsync();
                if (continueOnIdleTimeout)
                {
                    while (!pendingContext.IsCompleted)
                    {
                        using var idleTimeout = _timeoutFactory(TimeSpan.FromSeconds(_options.AcceptTimeoutSeconds));
                        using var idleLinked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, idleTimeout.Token);
                        try { await pendingContext.WaitAsync(idleLinked.Token).ConfigureAwait(false); }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && idleTimeout.IsCancellationRequested) { }
                    }
                }
                httpContext = await pendingContext.WaitAsync(continueOnIdleTimeout ? cancellationToken : linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
            {
                State = DebugStudioServerTransportState.Listening;
                throw new TimeoutException(
                    $"Accepting a WebSocket connection timed out after {_options.AcceptTimeoutSeconds} second(s).",
                    ex);
            }
            catch (Exception)
            {
                State = DebugStudioServerTransportState.Faulted;
                throw;
            }

            if (!httpContext.Request.IsWebSocketRequest)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                httpContext.Response.Close();
                continue;
            }

            HttpListenerWebSocketContext webSocketContext;
            using var handshakeTimeout = continueOnIdleTimeout ? _timeoutFactory(TimeSpan.FromSeconds(_options.AcceptTimeoutSeconds)) : null;
            using var handshakeLinked = handshakeTimeout == null ? null : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, handshakeTimeout.Token);
            try
            {
                // Idle waiting can span many deadlines. The handshake receives its own finite
                // deadline and its timeout continues to be a fault in both startup modes.
                webSocketContext = await httpContext.AcceptWebSocketAsync(subProtocol: null)
                    .WaitAsync(handshakeLinked?.Token ?? linkedCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && (handshakeTimeout ?? timeoutCts).IsCancellationRequested)
            {
                State = DebugStudioServerTransportState.Listening;
                throw new TimeoutException(
                    $"Completing the WebSocket handshake timed out after {_options.AcceptTimeoutSeconds} second(s).",
                    ex);
            }
            catch (Exception)
            {
                State = DebugStudioServerTransportState.Faulted;
                throw;
            }

            lock (_gate)
            {
                ReplaceCurrentSessionLocked(webSocketContext);
                State = DebugStudioServerTransportState.Connected;
            }

            return webSocketContext;
        }
    }

    /// <summary>
    /// 現在の接続と listener を停止する。
    /// 複数回呼ばれても安全な冪等 API とする。
    /// </summary>
    public void Stop()
    {
        StopInternal(markDisposed: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopInternal(markDisposed: true);
        _disposed = true;
        State = DebugStudioServerTransportState.Disposed;
        GC.SuppressFinalize(this);
    }

    private void StopInternal(bool markDisposed)
    {
        lock (_gate)
        {
            DisposeCurrentSessionLocked();

            if (_listener != null)
            {
                try
                {
                    if (_listener.IsListening)
                    {
                        _listener.Stop();
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    try
                    {
                        _listener.Close();
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    _listener = null;
                }
            }

            if (!markDisposed)
            {
                State = DebugStudioServerTransportState.Idle;
            }
        }
    }

    private void ReplaceCurrentSessionLocked(HttpListenerWebSocketContext nextContext)
    {
        DisposeCurrentSessionLocked();
        _currentContext = nextContext;
        _currentWebSocket = nextContext.WebSocket;
    }

    private void DisposeCurrentSessionLocked()
    {
        try
        {
            _currentWebSocket?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _currentWebSocket = null;
            _currentContext = null;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
