#nullable enable

using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.DebugSocket;
using UnityEngine;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// listener / outbound connect の transport lifecycle を担う内部 host。
    /// accept、upgrade、再接続、session activation の orchestration を service から分離する。
    /// </summary>
    internal sealed class DebugSocketTransportHost
    {
        private static readonly TimeSpan OutboundReconnectDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan OutboundKeepAliveInterval = TimeSpan.FromSeconds(20);

        private readonly DebugSocketOptions _options;
        private readonly IDebugSocketTransportHostCallbacks _callbacks;

        private HttpListener? _listener;
        private UniTask? _transportLoopTask;

        public DebugSocketTransportHost(DebugSocketOptions options, IDebugSocketTransportHostCallbacks callbacks)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        }

        public bool HasListener => _listener != null;

        public void StartListener(CancellationToken cancellationToken)
        {
            Debug.Log($"[DebugSocket] Starting listener on {_options.ListenerPrefix}");
            _listener = new HttpListener();
            _listener.Prefixes.Add(_options.ListenerPrefix);
            _listener.Start();
            _transportLoopTask = AcceptLoopAsync(_listener, cancellationToken);
            Debug.Log($"[DebugSocket] Listener started on {_options.ListenerPrefix}");
        }

        public void StartOutbound(CancellationToken cancellationToken)
        {
            var connectUri = _options.ConnectUri
                ?? throw new InvalidOperationException("debugSocket:connectUri is required while debugSocket:mode=connect.");

            Debug.Log($"[DebugSocket] Starting outbound client transport. endpoint={connectUri}");
            _transportLoopTask = ConnectLoopAsync(connectUri, cancellationToken);
        }

        public void AbortListenerOnStartFailure()
        {
            try
            {
                _listener?.Close();
            }
            catch
            {
            }

            _listener = null;
            _transportLoopTask = null;
        }

        public void StopListener()
        {
            if (_listener == null)
            {
                return;
            }

            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch
            {
            }
            finally
            {
                _listener = null;
            }
        }

        public async UniTask WaitForTransportLoopAsync()
        {
            if (_transportLoopTask.HasValue)
            {
                await _transportLoopTask.Value.SuppressCancellationThrow();
                _transportLoopTask = null;
            }
        }

        /// <summary>
        /// listener で upgrade 要求を受け続けるループ。
        /// ここでは request を受けるだけに留め、重い処理は個別メソッドへ渡す。
        /// </summary>
        private async UniTask AcceptLoopAsync(HttpListener listener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().AsUniTask();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException) when (!listener.IsListening)
                {
                    break;
                }

                // accept loop 全体を止めないため、各 request は個別に fire-and-forget で処理する。
                ProcessContextAsync(context, cancellationToken).Forget();
            }
        }

        private async UniTask ConnectLoopAsync(Uri connectUri, CancellationToken cancellationToken)
        {
            // DebugStudio 未起動時の 2 秒周期リトライで Console が溢れないよう、
            // 同一の「未接続ストリーク」では初回失敗だけをログに出す。
            var logNextConnectFailure = true;

            while (!cancellationToken.IsCancellationRequested)
            {
                ClientWebSocket? socket = null;
                DebugSocketClientSession? session = null;

                try
                {
                    socket = new ClientWebSocket();
                    socket.Options.KeepAliveInterval = OutboundKeepAliveInterval;

                    if (logNextConnectFailure)
                    {
                        Debug.Log($"[DebugSocket] Connecting to DebugStudio server. endpoint={connectUri}");
                    }

                    await socket.ConnectAsync(connectUri, cancellationToken).AsUniTask();

                    session = await _callbacks.ActivateSessionAsync(
                        socket,
                        cancellationToken,
                        connectedMessage: "DebugSocket connected to DebugStudio server.");
                    socket = null;
                    _callbacks.SetLastStartError(null);
                    Debug.Log($"[DebugSocket] Connected to DebugStudio server. endpoint={connectUri}");

                    // outbound mode では transport loop 自体は生かしたまま、
                    // その時点の session 完了だけを待って再接続判断へ戻る。
                    await session.Completion;
                }
                catch (OperationCanceledException)
                {
                    socket?.Dispose();
                    break;
                }
                catch (WebSocketException ex)
                {
                    socket?.Dispose();
                    _callbacks.SetLastStartError(ex.Message);
                    if (logNextConnectFailure)
                    {
                        Debug.LogWarning(
                            $"[DebugSocket] Failed to connect to DebugStudio server. endpoint={connectUri}, detail={ex.Message}");
                        logNextConnectFailure = false;
                    }
                }
                catch (Exception ex)
                {
                    socket?.Dispose();
                    _callbacks.SetLastStartError(ex.Message);
                    if (logNextConnectFailure)
                    {
                        Debug.LogError($"[DebugSocket] Outbound transport loop failed. endpoint={connectUri}: {ex}");
                        logNextConnectFailure = false;
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (session != null)
                {
                    Debug.LogWarning($"[DebugSocket] Connection to DebugStudio server ended. Reconnecting. endpoint={connectUri}");
                    logNextConnectFailure = true;
                }

                // 接続失敗や切断はセッション単位の揺らぎとして扱い、
                // service 全体は落とさず短い待機後に次の connect を試みる。
                await global::System.Threading.Tasks.Task
                    .Delay((int)OutboundReconnectDelay.TotalMilliseconds, cancellationToken)
                    .AsUniTask()
                    .SuppressCancellationThrow();
            }
        }

        /// <summary>
        /// 単一の upgrade request を処理し、必要なら新しいクライアントセッションを作る。
        /// </summary>
        private async UniTaskVoid ProcessContextAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            try
            {
                bool isWebSocketRequest = context.Request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                                          context.Request.Headers["Upgrade"]?.Equals("websocket", StringComparison.OrdinalIgnoreCase) == true
                                          && context.Request.Headers["Sec-WebSocket-Version"] != null
                                          && context.Request.Headers["Sec-WebSocket-Key"] != null;
                if (!isWebSocketRequest && context.Request.IsWebSocketRequest == false)
                {
                    Debug.LogWarning($"[DebugSocket] Received non-WebSocket request from {context.Request.RemoteEndPoint}. Only WebSocket requests are accepted.");
                    Debug.LogWarning($"[DebugSocket] Request details: Method={context.Request.HttpMethod}, URL={context.Request.Url}, RawURL={context.Request.RawUrl}, Host={context.Request.UserHostName}, Connection={context.Request.Headers["Connection"]}, Upgrade={context.Request.Headers["Upgrade"]}, Sec-WebSocket-Version={context.Request.Headers["Sec-WebSocket-Version"]}, Origin={context.Request.Headers["Origin"]}, UserAgent={context.Request.UserAgent}, Headers={context.Request.Headers}");
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    return;
                }

                if (!string.Equals(context.Request.Url?.AbsolutePath, _options.Path, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null)
                    .AsUniTask();

                // upgrade 完了直後にサービス停止が入っていた場合は、
                // これ以上 session 化せずその場で socket を閉じる。
                // ここで ActivateSessionAsync まで進めると、Start されない zombie session を current に載せる危険がある。
                if (cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        webSocketContext.WebSocket.Abort();
                        webSocketContext.WebSocket.Dispose();
                    }
                    catch
                    {
                    }

                    return;
                }

                await _callbacks.ActivateSessionAsync(
                    webSocketContext.WebSocket,
                    cancellationToken,
                    connectedMessage: "DebugSocket client connected.");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                // handshake 失敗は request 単位の問題なので、listener 全体は落とさない。
                try
                {
                    Debug.LogException(ex);
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch
                {
                }
            }
        }
    }
}
