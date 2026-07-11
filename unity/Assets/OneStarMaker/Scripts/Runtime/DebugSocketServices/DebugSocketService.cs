#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Telemetry;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// Unity 側で DebugSocket transport を維持する長寿命サービス。
    ///
    /// <para>
    /// v1 の設計判断として、
    /// </para>
    /// <list type="bullet">
    /// <item><description>単一クライアントのみを扱う</description></item>
    /// <item><description>新接続が来たら旧接続を置き換える</description></item>
    /// <item><description>送信は必ず queue 経由にし、WebSocket への同時 Send を避ける</description></item>
    /// </list>
    /// <para>
    /// を固定している。
    /// </para>
    /// </summary>
    public sealed class DebugSocketService : IDisposable
    {
        public readonly struct RuntimeDiagnosticsSnapshot
        {
            public RuntimeDiagnosticsSnapshot(
                string transportMode,
                string configuredEndpoint,
                string listenerPrefix,
                bool autoStart,
                bool isRunning,
                bool hasActiveSession,
                string? sessionId,
                int pendingQueueLength,
                int maxQueueLength,
                long droppedBeforeSessionCount,
                long droppedQueueOverflowCount,
                string? lastStartError)
            {
                TransportMode = transportMode;
                ConfiguredEndpoint = configuredEndpoint;
                ListenerPrefix = listenerPrefix;
                AutoStart = autoStart;
                IsRunning = isRunning;
                HasActiveSession = hasActiveSession;
                SessionId = sessionId;
                PendingQueueLength = pendingQueueLength;
                MaxQueueLength = maxQueueLength;
                DroppedBeforeSessionCount = droppedBeforeSessionCount;
                DroppedQueueOverflowCount = droppedQueueOverflowCount;
                LastStartError = lastStartError;
            }

            public string TransportMode { get; }
            public string ConfiguredEndpoint { get; }
            public string ListenerPrefix { get; }
            public bool AutoStart { get; }
            public bool IsRunning { get; }
            public bool HasActiveSession { get; }
            public string? SessionId { get; }
            public int PendingQueueLength { get; }
            public int MaxQueueLength { get; }
            public long DroppedBeforeSessionCount { get; }
            public long DroppedQueueOverflowCount { get; }
            public string? LastStartError { get; }
        }

        private const int CurrentSchemaVersion = 1;
        private const int MaxInboundMessageBytes = 1024 * 1024;
        private const string MainThreadContextUnavailableMessage = "Main thread synchronization context is not available.";
        private static readonly TimeSpan WebSocketCloseTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan OutboundReconnectDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan OutboundKeepAliveInterval = TimeSpan.FromSeconds(20);
        private static readonly DebugStudioCapability ServerCapabilities =
            DebugStudioCapability.CapabilityNegotiation |
            DebugStudioCapability.LogStream |
            DebugStudioCapability.TelemetryStream |
            DebugStudioCapability.ServiceStatusStream |
            DebugStudioCapability.DebugCommand |
            DebugStudioCapability.CommandResult |
            DebugStudioCapability.HierarchySnapshot |
            DebugStudioCapability.HierarchyDelta |
            DebugStudioCapability.InspectorQuery |
            DebugStudioCapability.InspectorDetail;

        private static readonly int[] SupportedMessageTypes =
        {
            (int)DebugSocketMessageType.Log,
            (int)DebugSocketMessageType.Telemetry,
            (int)DebugSocketMessageType.ServiceStatus,
            (int)DebugSocketMessageType.DebugCommand,
            (int)DebugSocketMessageType.CommandResult,
            (int)DebugSocketMessageType.CapabilityHello,
            (int)DebugSocketMessageType.CapabilityWelcome,
            (int)DebugSocketMessageType.HierarchySnapshot,
            (int)DebugSocketMessageType.HierarchyDelta,
            (int)DebugSocketMessageType.InspectorQuery,
            (int)DebugSocketMessageType.InspectorDetail,
        };

        private readonly object _gate = new();
        private readonly IDebugCommandDispatcher _dispatcher;
        private readonly DebugSocketRealtimeStream _realtimeStream;
        private readonly Dictionary<long, HierarchyNodeDtoV1> _publishedHierarchyNodes = new();
        // Unity の object identity をそのまま wire に出さず、
        // service 内だけで扱う stable token へ変換するためのキャッシュ群。
        //
        // - forward: Unity 内部キー -> token
        // - reverse: token -> Unity object / Unity 内部キー
        //
        // を 3 本で持つ。
        // こうしておくと、hierarchy 送信時は token を安定再利用でき、
        // inspector query 側は token から O(1) で GameObject を引き直せる。
        private readonly Dictionary<ulong, long> _runtimeIdentityToNodeIds = new();
        private readonly Dictionary<long, ulong> _nodeIdToRuntimeIdentities = new();
        private readonly Dictionary<long, GameObject> _nodeIdToGameObjects = new();
        private readonly List<GameObject> _rootGameObjectBuffer = new(32);
        private readonly SynchronizationContext? _mainThreadContext;

        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private UniTask? _transportLoopTask;
        private ClientSession? _currentSession;
        private long _hierarchyRevision;
        private long _publishedHierarchyRevision;
        private long _inspectorRevision;
        private long _droppedBeforeSessionCount;
        private long _droppedQueueOverflowCount;
        // token は service の寿命中は再利用しない。
        // 旧セッション由来の遅延 query が後から到着しても、
        // 新しいオブジェクトへ偶然 alias しないことを優先する。
        private long _nextRuntimeNodeId = 1;
        private string? _lastStartError;
        private bool _disposed;

        public DebugSocketService(DebugSocketOptions options, IDebugCommandDispatcher dispatcher)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _mainThreadContext = SynchronizationContext.Current;

            // 受信スレッドから直接 Unity API を触らないため、必ず main-thread decorator を噛ませる。
            _dispatcher = new MainThreadDebugCommandDispatcher(
                dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)),
                _mainThreadContext);
            _realtimeStream = new DebugSocketRealtimeStream(this);

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        /// <summary>起動設定。</summary>
        public DebugSocketOptions Options { get; }

        /// <summary>
        /// logger 側へ渡す realtime stream。
        /// 未接続時のメッセージは drop されるが、呼び出し側は接続状態を意識しなくてよい。
        /// </summary>
        public Stream RealtimeStream => _realtimeStream;

        /// <summary>現在選択されている transport の管理ループが起動中か。</summary>
        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        /// <summary>
        /// runtime validation / 現場診断用の軽量 snapshot。
        /// queue 圧迫や未接続時 drop の累積を外から観測できるようにする。
        /// </summary>
        public RuntimeDiagnosticsSnapshot GetRuntimeDiagnosticsSnapshot()
        {
            ClientSession? session;
            lock (_gate)
            {
                session = _currentSession;
            }

            return new RuntimeDiagnosticsSnapshot(
                transportMode: Options.TransportMode.ToString().ToLowerInvariant(),
                configuredEndpoint: Options.EndpointDisplayName,
                listenerPrefix: Options.ListenerPrefix,
                autoStart: Options.AutoStart,
                isRunning: IsRunning,
                hasActiveSession: session != null,
                sessionId: session?.SessionId,
                pendingQueueLength: session?.PendingQueueLength ?? 0,
                maxQueueLength: Options.MaxQueueLength,
                droppedBeforeSessionCount: Interlocked.Read(ref _droppedBeforeSessionCount),
                droppedQueueOverflowCount: Interlocked.Read(ref _droppedQueueOverflowCount),
                lastStartError: _lastStartError);
        }

        /// <summary>
        /// 選択されている DebugSocket transport を起動する。
        /// listen でも connect でも、実処理ループ自体はバックグラウンドで回り続ける。
        /// </summary>
        public UniTask StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (!Options.Enabled)
            {
                Debug.LogWarning("[DebugSocket] StartAsync was called while options.Enabled=false.");
                return UniTask.CompletedTask;
            }

            if (!Options.AutoStart)
            {
                Debug.LogWarning($"[DebugSocket] AutoStart is disabled. Transport start skipped. endpoint={Options.EndpointDisplayName}");
                return UniTask.CompletedTask;
            }

            if (IsRunning)
            {
                return UniTask.CompletedTask;
            }

            try
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                switch (Options.TransportMode)
                {
                    case DebugSocketTransportMode.Listen:
                        StartListenerTransport(_cts.Token);
                        break;

                    case DebugSocketTransportMode.Connect:
                        StartOutboundTransport(_cts.Token);
                        break;

                    default:
                        throw new NotSupportedException(
                            $"DebugSocket transport mode '{Options.TransportMode}' is not supported. endpoint={Options.EndpointDisplayName}");
                }

                _lastStartError = null;
            }
            catch (Exception ex)
            {
                _lastStartError = ex.Message;
                Debug.LogError($"[DebugSocket] Failed to start transport. mode={Options.TransportMode}, endpoint={Options.EndpointDisplayName}: {ex}");

                try
                {
                    _listener?.Close();
                }
                catch
                {
                }

                _listener = null;
                _cts?.Dispose();
                _cts = null;
                _transportLoopTask = null;
                throw;
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 現在の transport loop とセッションを停止する。
        /// PlayMode 終了やアプリ終了時に安全に後始末できるよう、複数回呼んでもよい。
        /// </summary>
        public async UniTask StopAsync()
        {
            if (_listener == null && _cts == null)
            {
                return;
            }

            // まず loop 側へ停止を通知する。
            _cts?.Cancel();

            // 現在のクライアントセッションを閉じる。
            ClientSession? sessionToClose;
            lock (_gate)
            {
                sessionToClose = _currentSession;
                _currentSession = null;
                ResetPublishedHierarchyUnsafe();
            }

            // GetContextAsync を抜けさせるため listener 自体も止める。
            if (_listener != null)
            {
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

            if (sessionToClose != null)
            {
                await sessionToClose.CloseAsync("service-stopping", CancellationToken.None);
            }

            // transport loop の終了も待ってから token source を片付ける。
            if (_transportLoopTask.HasValue)
            {
                await _transportLoopTask.Value.SuppressCancellationThrow();
                _transportLoopTask = null;
            }

            _cts?.Dispose();
            _cts = null;
        }

        private void StartListenerTransport(CancellationToken cancellationToken)
        {
            Debug.Log($"[DebugSocket] Starting listener on {Options.ListenerPrefix}");
            _listener = new HttpListener();
            _listener.Prefixes.Add(Options.ListenerPrefix);
            _listener.Start();
            _transportLoopTask = AcceptLoopAsync(_listener, cancellationToken);
            Debug.Log($"[DebugSocket] Listener started on {Options.ListenerPrefix}");
        }

        private void StartOutboundTransport(CancellationToken cancellationToken)
        {
            var connectUri = Options.ConnectUri
                ?? throw new InvalidOperationException("debugSocket:connectUri is required while debugSocket:mode=connect.");

            Debug.Log($"[DebugSocket] Starting outbound client transport. endpoint={connectUri}");
            _transportLoopTask = ConnectLoopAsync(connectUri, cancellationToken);
        }


        /// <summary>
        /// logging infrastructure から届いた realtime log frame を現在セッションへ積む。
        /// ログ無効または未接続時は破棄する。
        /// </summary>
        internal void EnqueueRealtimeLogFrame(byte[] framedMessageBuffer, int count)
        {
            // realtime log は毎フレーム級で流れる可能性があるため、
            // stream 側で借りた pooled buffer をそのままキューへ受け渡す。
            // ここで drop する場合も、必ず pool へ返して所有権を閉じる。
            if (!Options.SendLogs || count <= 0)
            {
                ArrayPool<byte>.Shared.Return(framedMessageBuffer);
                return;
            }

            EnqueueOutgoingMessage(OutgoingFrame.CreatePooled(framedMessageBuffer, count));
        }

        /// <summary>
        /// テレメトリレコードを protocol envelope 化して現在セッションへ流す。
        /// </summary>
        public void EnqueueTelemetry(in TelemetryRecord record)
        {
            if (!Options.SendTelemetry)
            {
                return;
            }

            EnqueueOutgoingMessage(DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.Telemetry,
                DebugTelemetryEnvelopeV1.FromRecord(record)));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            StopAsync().Forget();
            _realtimeStream.Dispose();
        }

        /// <summary>
        /// scene load/unload だけでは拾えない hierarchy 変化に対する明示通知口。
        ///
        /// <para>
        /// 生成/破棄/親子付け替えを独自システム側で把握できる箇所があれば、
        /// ここを呼ぶことで現在セッションへ snapshot / delta の再送を要求できる。
        /// </para>
        /// </summary>
        public void NotifyHierarchyChanged()
        {
            // hierarchy capture は SceneManager / GameObject / Transform を触るため main thread 必須。
            // context が無いままバックグラウンドで進めると Unity API 制約を破るので、
            // 明示的に何もしない方を選ぶ。
            if (_mainThreadContext == null)
            {
                Debug.LogWarning($"[DebugSocket] NotifyHierarchyChanged skipped. {MainThreadContextUnavailableMessage}");
                return;
            }

            if (SynchronizationContext.Current == _mainThreadContext)
            {
                PublishHierarchyUpdateIfPossible();
                return;
            }

            _mainThreadContext.Post(
                static state => ((DebugSocketService)state!).PublishHierarchyUpdateIfPossible(),
                this);
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
                ClientSession? session = null;

                try
                {
                    socket = new ClientWebSocket();
                    socket.Options.KeepAliveInterval = OutboundKeepAliveInterval;

                    if (logNextConnectFailure)
                    {
                        Debug.Log($"[DebugSocket] Connecting to DebugStudio server. endpoint={connectUri}");
                    }

                    await socket.ConnectAsync(connectUri, cancellationToken).AsUniTask();

                    session = await ActivateSessionAsync(
                        socket,
                        cancellationToken,
                        connectedMessage: "DebugSocket connected to DebugStudio server.");
                    socket = null;
                    _lastStartError = null;
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
                    _lastStartError = ex.Message;
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
                    _lastStartError = ex.Message;
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

                if (!string.Equals(context.Request.Url?.AbsolutePath, Options.Path, StringComparison.OrdinalIgnoreCase))
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

                await ActivateSessionAsync(
                    webSocketContext.WebSocket,
                    cancellationToken,
                    connectedMessage: "DebugSocket client connected.");
            }
            catch (OperationCanceledException)
            {
            }
            catch(Exception ex)
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

        /// <summary>
        /// サービス状態通知を framed binary にする。
        /// ログやテレメトリに乗せたくない管理イベントはこの envelope で返す。
        /// </summary>
        private byte[] CreateServiceStatus(string status, string message)
        {
            return DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.ServiceStatus,
                new DebugSocketServiceStatusEnvelopeV1
                {
                    Status = status,
                    Message = message,
                    TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                });
        }

        /// <summary>
        /// hello を受けた後に返す capability welcome。
        /// schema 互換が成立した場合だけ、実際に送受信できる capability 群を negotiated として返す。
        /// </summary>
        private byte[] CreateCapabilityWelcomeFrame(string sessionId, CapabilityHandshakeHelloEnvelopeV1 hello)
        {
            var runtimeAvailableCapabilities = GetRuntimeAvailableCapabilities();
            var selectedSchemaVersion =
                hello.MinSchemaVersion <= CurrentSchemaVersion && CurrentSchemaVersion <= hello.MaxSchemaVersion
                    ? CurrentSchemaVersion
                    : 0;
            var negotiatedCapabilities = selectedSchemaVersion > 0
                ? runtimeAvailableCapabilities & hello.SupportedCapabilities
                : DebugStudioCapability.None;
            var statusMessage = selectedSchemaVersion > 0
                ? $"Capability negotiation completed with {hello.ClientName}."
                : $"Schema negotiation failed. Client supports {hello.MinSchemaVersion}-{hello.MaxSchemaVersion}, server is {CurrentSchemaVersion}.";
            return DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.CapabilityWelcome,
                new CapabilityHandshakeWelcomeEnvelopeV1
                {
                    SessionId = sessionId,
                    ServerName = string.IsNullOrWhiteSpace(Application.productName) ? "Unity Player" : Application.productName,
                    SelectedSchemaVersion = selectedSchemaVersion,
                    ServerCapabilities = runtimeAvailableCapabilities,
                    NegotiatedCapabilities = negotiatedCapabilities,
                    SupportedMessageTypes = SupportedMessageTypes,
                    TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    StatusMessage = statusMessage,
                });
        }

        private DebugStudioCapability GetRuntimeAvailableCapabilities()
        {
            if (_mainThreadContext != null)
            {
                return ServerCapabilities;
            }

            // main thread context が無いと hierarchy / inspector の実処理は安全に実行できない。
            // handshake だけ「使える」と広告すると viewer 側が永続的に失敗ボタンを押せてしまうため、
            // 交渉段階から capability を落としておく。
            return ServerCapabilities &
                   ~(
                       DebugStudioCapability.HierarchySnapshot |
                       DebugStudioCapability.HierarchyDelta |
                       DebugStudioCapability.InspectorQuery |
                       DebugStudioCapability.InspectorDetail);
        }

        /// <summary>
        /// 現在のセッションへメッセージを積む。
        /// セッションがなければ drop する。v1 では未接続時のバッファ保持はしない。
        /// </summary>
        private void EnqueueOutgoingMessage(byte[] framedMessage)
        {
            EnqueueOutgoingMessage(OutgoingFrame.CreateOwned(framedMessage));
        }

        /// <summary>
        /// 現在のセッションへ framed message を積む。
        ///
        /// <para>
        /// pooled buffer の場合、未接続や drop 時でも release を忘れないことが重要。
        /// 送信経路のどこで ownership が終わるかをこのメソッドから先で一貫させる。
        /// </para>
        /// </summary>
        private void EnqueueOutgoingMessage(in OutgoingFrame framedMessage)
        {
            ClientSession? session;
            lock (_gate)
            {
                session = _currentSession;
            }

            if (session == null)
            {
                Interlocked.Increment(ref _droppedBeforeSessionCount);
                framedMessage.Release();
                return;
            }

            session.Enqueue(framedMessage);
        }

        private void EnqueueRuntimeDiagnosticsIfNeeded(ClientSession session)
        {
            var snapshot = GetRuntimeDiagnosticsSnapshot();
            if (snapshot.DroppedBeforeSessionCount == 0 && snapshot.DroppedQueueOverflowCount == 0)
            {
                return;
            }

            session.Enqueue(CreateServiceStatus(
                "runtime-diagnostics",
                $"queue={snapshot.PendingQueueLength}/{snapshot.MaxQueueLength}, dropped.disconnected={snapshot.DroppedBeforeSessionCount}, dropped.queueOverflow={snapshot.DroppedQueueOverflowCount}."));
        }

        private void RecordQueueOverflowDrops(int droppedCount)
        {
            if (droppedCount <= 0)
            {
                return;
            }

            Interlocked.Add(ref _droppedQueueOverflowCount, droppedCount);
        }

        /// <summary>
        /// 受信した binary message を protocol として解釈し、必要なら command result を返す。
        /// <see cref="ReadOnlyMemory{T}"/> を受けることで、受信 loop 側の余計な byte[] 複製を避ける。
        /// </summary>
        private async UniTask HandleInboundMessageAsync(ClientSession session, ReadOnlyMemory<byte> framedMessage, CancellationToken cancellationToken)
        {
            if (!DebugSocketProtocol.TryDeserializeEnvelope(framedMessage, out var envelope) || envelope == null)
            {
                session.Enqueue(CreateServiceStatus("protocol-error", "Received invalid framed message."));
                return;
            }

            // 新しい client が current へ切り替わった後に旧 session から遅れて届いた inbound は、
            // もう現在の hierarchy/token 状態へ影響させない。
            // ここを通してしまうと、replaced session が後から snapshot を再発行して
            // 新 session 用に初期化した token cache を汚染できてしまう。
            if (!IsCurrentSession(session))
            {
                return;
            }

            switch ((DebugSocketMessageType)envelope.MessageType)
            {
                case DebugSocketMessageType.CapabilityHello:
                    if (!DebugSocketProtocol.TryDeserializePayload<CapabilityHandshakeHelloEnvelopeV1>(envelope, out var hello) ||
                        hello == null)
                    {
                        session.Enqueue(CreateServiceStatus("protocol-error", "Failed to decode capability hello payload."));
                        return;
                    }

                    var schemaCompatible =
                        hello.MinSchemaVersion <= CurrentSchemaVersion &&
                        CurrentSchemaVersion <= hello.MaxSchemaVersion;
                    var runtimeAvailableCapabilities = GetRuntimeAvailableCapabilities();
                    session.HasCompletedCapabilityHello = true;
                    session.NegotiatedCapabilities = schemaCompatible
                        ? runtimeAvailableCapabilities & hello.SupportedCapabilities
                        : DebugStudioCapability.None;
                    session.Enqueue(CreateCapabilityWelcomeFrame(session.SessionId, hello));
                    if (!schemaCompatible)
                    {
                        await session.CloseFromReceiveLoopAsync("schema-mismatch");
                        return;
                    }

                    if ((session.NegotiatedCapabilities & DebugStudioCapability.HierarchySnapshot) != 0)
                    {
                        if (_mainThreadContext == null)
                        {
                            session.Enqueue(CreateServiceStatus("main-thread-unavailable", MainThreadContextUnavailableMessage));
                            return;
                        }

                        await SwitchToMainThreadAsync(cancellationToken);
                        if (!IsCurrentSession(session))
                        {
                            return;
                        }

                        session.Enqueue(CreateHierarchySnapshotFrame());
                    }
                    return;

                case DebugSocketMessageType.DebugCommand:
                    if (!DebugSocketProtocol.TryDeserializePayload<DebugCommandEnvelopeV1>(envelope, out var command) || command == null)
                    {
                        session.Enqueue(CreateServiceStatus("protocol-error", "Failed to decode debug command payload."));
                        return;
                    }

                    // live validation を「アプリ固有コマンド未実装」で止めないため、
                    // サービス自身の built-in command だけはここで先に処理する。
                    // これにより dispatcher override がまだ無いアプリでも
                    // command correlation / runtime diagnostics の最小 slice を通せる。
                    if (TryDispatchBuiltInCommand(command, out var builtInResult))
                    {
                        builtInResult.RequestId = command.RequestId;
                        session.Enqueue(DebugSocketProtocol.SerializeMessage(
                            DebugSocketMessageType.CommandResult,
                            builtInResult,
                            command.RequestId));
                        return;
                    }

                    if (!session.HasCompletedCapabilityHello ||
                        (session.NegotiatedCapabilities & DebugStudioCapability.DebugCommand) == 0)
                    {
                        session.Enqueue(CreateServiceStatus("protocol-error", "Debug command capability is not negotiated."));
                        return;
                    }

                    try
                    {
                        var result = await _dispatcher.DispatchAsync(command, cancellationToken);
                        result.RequestId = command.RequestId;

                        session.Enqueue(DebugSocketProtocol.SerializeMessage(
                            DebugSocketMessageType.CommandResult,
                            result,
                            command.RequestId));
                    }
                    catch (Exception ex)
                    {
                        // 1 コマンドの失敗でセッション全体を落とすと、viewer 側は再接続まで必要になる。
                        // ここでは command result として失敗を返し、transport 自体は継続させる。
                        session.Enqueue(DebugSocketProtocol.SerializeMessage(
                            DebugSocketMessageType.CommandResult,
                            new DebugCommandResultEnvelopeV1
                            {
                                RequestId = command.RequestId,
                                Success = false,
                                Message = ex.Message,
                            },
                            command.RequestId));
                    }
                    return;

                case DebugSocketMessageType.InspectorQuery:
                    if (!DebugSocketProtocol.TryDeserializePayload<InspectorQueryEnvelopeV1>(envelope, out var inspectorQuery) ||
                        inspectorQuery == null)
                    {
                        session.Enqueue(CreateServiceStatus("protocol-error", "Failed to decode inspector query payload."));
                        return;
                    }

                    if (!session.HasCompletedCapabilityHello ||
                        (session.NegotiatedCapabilities & DebugStudioCapability.InspectorQuery) == 0 ||
                        (session.NegotiatedCapabilities & DebugStudioCapability.InspectorDetail) == 0)
                    {
                        session.Enqueue(CreateInspectorFaultFrame(
                            inspectorQuery,
                            envelope.RequestId,
                            "Inspector query/detail capability is not negotiated."));
                        return;
                    }

                    if (_mainThreadContext == null)
                    {
                        session.Enqueue(CreateInspectorMainThreadUnavailableFrame(inspectorQuery, envelope.RequestId));
                        return;
                    }

                    await SwitchToMainThreadAsync(cancellationToken);
                    if (!IsCurrentSession(session))
                    {
                        return;
                    }

                    try
                    {
                        session.Enqueue(CreateInspectorDetailFrame(inspectorQuery, envelope.RequestId));
                    }
                    catch (Exception ex)
                    {
                        // inspector detail 生成中の例外も、query 単位の fault として返す。
                        // セッション close まで波及させるより、viewer 側に失敗理由を見せた方が運用しやすい。
                        session.Enqueue(CreateInspectorFaultFrame(inspectorQuery, envelope.RequestId, ex.Message));
                    }
                    return;

                default:
                    session.Enqueue(CreateServiceStatus(
                        "protocol-error",
                        $"Unsupported inbound message type: {(DebugSocketMessageType)envelope.MessageType}."));
                    return;
            }
        }

        private bool TryDispatchBuiltInCommand(
            DebugCommandEnvelopeV1 command,
            out DebugCommandResultEnvelopeV1 result)
        {
            if (string.Equals(command.CommandType, "ping", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(command.CommandType, "debugsocket.ping", StringComparison.OrdinalIgnoreCase))
            {
                result = new DebugCommandResultEnvelopeV1
                {
                    Success = true,
                    Message = "DebugSocket ping succeeded.",
                    PayloadJson = string.Format(
                        CultureInfo.InvariantCulture,
                        "{{\"service\":\"debugsocket\",\"status\":\"ok\",\"timestampUnixTimeMilliseconds\":{0}}}",
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                };
                return true;
            }

            if (string.Equals(command.CommandType, "runtime-diagnostics", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(command.CommandType, "debugsocket.runtime-diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                var snapshot = GetRuntimeDiagnosticsSnapshot();
                var sessionIdJson = snapshot.SessionId == null
                    ? "null"
                    : $"\"{EscapeJsonString(snapshot.SessionId)}\"";
                var lastStartErrorJson = snapshot.LastStartError == null
                    ? "null"
                    : $"\"{EscapeJsonString(snapshot.LastStartError)}\"";

                result = new DebugCommandResultEnvelopeV1
                {
                    Success = true,
                    Message = "Runtime diagnostics snapshot captured.",
                    PayloadJson = string.Format(
                        CultureInfo.InvariantCulture,
                        "{{\"transportMode\":\"{0}\",\"configuredEndpoint\":\"{1}\",\"listenerPrefix\":\"{2}\",\"autoStart\":{3},\"isRunning\":{4},\"hasActiveSession\":{5},\"sessionId\":{6},\"pendingQueueLength\":{7},\"maxQueueLength\":{8},\"droppedBeforeSessionCount\":{9},\"droppedQueueOverflowCount\":{10},\"lastStartError\":{11}}}",
                        EscapeJsonString(snapshot.TransportMode),
                        EscapeJsonString(snapshot.ConfiguredEndpoint),
                        EscapeJsonString(snapshot.ListenerPrefix),
                        snapshot.AutoStart ? "true" : "false",
                        snapshot.IsRunning ? "true" : "false",
                        snapshot.HasActiveSession ? "true" : "false",
                        sessionIdJson,
                        snapshot.PendingQueueLength,
                        snapshot.MaxQueueLength,
                        snapshot.DroppedBeforeSessionCount,
                        snapshot.DroppedQueueOverflowCount,
                        lastStartErrorJson),
                };
                return true;
            }

            result = new DebugCommandResultEnvelopeV1();
            return false;
        }

        private bool IsCurrentSession(ClientSession session)
        {
            lock (_gate)
            {
                return ReferenceEquals(_currentSession, session);
            }
        }

        /// <summary>
        /// セッション終了時に current session 参照を掃除する。
        /// 自分が current でない場合は、すでに新接続へ切り替わっているので何もしない。
        /// </summary>
        private void OnSessionClosed(ClientSession session)
        {
            lock (_gate)
            {
                if (ReferenceEquals(_currentSession, session))
                {
                    _currentSession = null;
                    ResetPublishedHierarchyUnsafe();
                }
            }
        }

        private async UniTask<ClientSession> ActivateSessionAsync(
            WebSocket socket,
            CancellationToken cancellationToken,
            string connectedMessage)
        {
            var session = new ClientSession(this, socket, Options.MaxQueueLength, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                await session.CloseAsync("activation-cancelled", CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();
            }

            ClientSession? previousSession;
            lock (_gate)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    previousSession = null;
                    _ = session.CloseAsync("activation-cancelled", CancellationToken.None);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                previousSession = _currentSession;
                // session が切り替わる瞬間に hierarchy 正本と token cache を明示リセットする。
                // OnSessionClosed(previous) だけに頼ると、
                // 「current を新 session に差し替えた後で旧 session が閉じる」経路で reset 漏れが起きる。
                // counter 自体は戻さず、旧 session の遅延 query が新 object へ alias しないことを優先する。
                ResetPublishedHierarchyUnsafe();
                _currentSession = session;
            }

            // 新セッションを先に起動してから、旧セッションを閉じる。
            // こうすると viewer 側の再接続が連続しても、空白時間を最小化できる。
            session.Start();
            session.Enqueue(CreateServiceStatus("connected", connectedMessage));
            EnqueueRuntimeDiagnosticsIfNeeded(session);

            if (previousSession != null)
            {
                await previousSession.CloseAsync("replaced-by-new-client", CancellationToken.None);
            }

            return session;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DebugSocketService));
            }
        }

        private static string EscapeJsonString(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private async UniTask SwitchToMainThreadAsync(CancellationToken cancellationToken)
        {
            await new SwitchToSynchronizationContextAwaitable(_mainThreadContext, cancellationToken);
        }

        /// <summary>
        /// 1 GameObject subtree を preorder で平坦化して snapshot node 配列へ積む。
        /// DebugStudio 側は `ParentId + Depth + TraversalIndex` を使って tree を再構築する。
        /// </summary>
        private sealed class HierarchyCaptureResult
        {
            public HierarchyCaptureResult(List<HierarchyNodeDtoV1> nodes, HashSet<long> seenNodeIds)
            {
                Nodes = nodes;
                SeenNodeIds = seenNodeIds;
            }

            public List<HierarchyNodeDtoV1> Nodes { get; }

            public HashSet<long> SeenNodeIds { get; }
        }

        private void AppendHierarchyNodeRecursiveUnsafe(
            Scene scene,
            Transform transform,
            long parentId,
            int depth,
            ref int traversalIndex,
            List<HierarchyNodeDtoV1> nodes,
            HashSet<long> seenNodeIds)
        {
            var gameObject = transform.gameObject;
            var nodeId = CreateRuntimeNodeIdUnsafe(gameObject);
            seenNodeIds.Add(nodeId);
            var flags = HierarchyNodeFlags.None;

            if (gameObject.activeSelf)
            {
                flags |= HierarchyNodeFlags.ActiveSelf;
            }

            if (gameObject.activeInHierarchy)
            {
                flags |= HierarchyNodeFlags.ActiveInHierarchy;
            }

            if (transform.parent == null)
            {
                flags |= HierarchyNodeFlags.SceneRoot;
            }

            if (transform.childCount > 0)
            {
                flags |= HierarchyNodeFlags.HasChildren;
            }

            if (string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal))
            {
                flags |= HierarchyNodeFlags.DontDestroyOnLoad;
            }

            nodes.Add(new HierarchyNodeDtoV1
            {
                NodeId = nodeId,
                ParentId = parentId,
                TypeId = 1,
                Flags = flags,
                Depth = depth,
                SiblingIndex = transform.GetSiblingIndex(),
                ChildCount = transform.childCount,
                TraversalIndex = traversalIndex++,
                Name = gameObject.name,
                TypeName = nameof(GameObject),
            });

            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                AppendHierarchyNodeRecursiveUnsafe(
                    scene,
                    transform.GetChild(childIndex),
                    nodeId,
                    depth + 1,
                    ref traversalIndex,
                    nodes,
                    seenNodeIds);
            }
        }

        private void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            PublishHierarchyUpdateIfPossible();
        }

        private void OnSceneUnloaded(Scene _)
        {
            PublishHierarchyUpdateIfPossible();
        }

        private void OnActiveSceneChanged(Scene _, Scene __)
        {
            PublishHierarchyUpdateIfPossible();
        }

        /// <summary>
        /// 現在接続中のクライアントが hierarchy を購読していれば、
        /// 直近に送った正本との差分または全量 snapshot を再送する。
        /// </summary>
        private void PublishHierarchyUpdateIfPossible()
        {
            ClientSession? session;
            byte[]? framedMessage = null;
            lock (_gate)
            {
                session = _currentSession;
                if (session == null || (session.NegotiatedCapabilities & DebugStudioCapability.HierarchySnapshot) == 0)
                {
                    return;
                }

                // hierarchy capture / publish state / token pruning を同じ排他境界へ揃える。
                // これにより、capture の途中で別スレッドがセッション差し替えや state reset を行っても、
                // 「half old / half new」な token 空間にならないようにする。
                var captureResult = CaptureHierarchyNodesUnsafe();
                if ((session.NegotiatedCapabilities & DebugStudioCapability.HierarchyDelta) != 0 &&
                    TryCreateHierarchyDeltaFrameUnsafe(captureResult, out framedMessage))
                {
                    // delta が作れた場合はそれを優先送信する。
                }
                else if ((session.NegotiatedCapabilities & DebugStudioCapability.HierarchyDelta) != 0 &&
                    HasPublishedHierarchyStateUnsafe())
                {
                    // 既存正本があり、差分も発生しなかった場合は何も送らない。
                    return;
                }
                else
                {
                    framedMessage = CreateHierarchySnapshotFrameUnsafe(captureResult);
                }
            }

            session.Enqueue(framedMessage!);
        }

        /// <summary>
        /// 現在ロード済み scene から hierarchy node 一覧を取得する。
        /// snapshot と delta の両方で同じ正規化結果を使うため、列挙処理を 1 箇所へ寄せる。
        /// </summary>
        private HierarchyCaptureResult CaptureHierarchyNodesUnsafe()
        {
            var sceneCount = SceneManager.sceneCount;
            var nodes = new List<HierarchyNodeDtoV1>(sceneCount * 16);
            var seenNodeIds = new HashSet<long>();
            var traversalIndex = 0;

            for (var sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                var roots = GetRootGameObjectsNonAlloc(scene);
                for (var rootIndex = 0; rootIndex < roots.Count; rootIndex++)
                {
                    AppendHierarchyNodeRecursiveUnsafe(
                        scene,
                        roots[rootIndex].transform,
                        parentId: 0,
                        depth: 0,
                        ref traversalIndex,
                        nodes,
                        seenNodeIds);
                }
            }

            return new HierarchyCaptureResult(nodes, seenNodeIds);
        }

        private byte[] CreateHierarchySnapshotFrame()
        {
            lock (_gate)
            {
                return CreateHierarchySnapshotFrameUnsafe(CaptureHierarchyNodesUnsafe());
            }
        }

        private byte[] CreateHierarchySnapshotFrameUnsafe(HierarchyCaptureResult captureResult)
        {
            var revision = Interlocked.Increment(ref _hierarchyRevision);
            ReplacePublishedHierarchyUnsafe(captureResult.Nodes, revision);
            PruneRuntimeNodeMappingsUnsafe(captureResult.SeenNodeIds);

            return DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.HierarchySnapshot,
                new HierarchySnapshotEnvelopeV1
                {
                    Revision = revision,
                    CapturedAtUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ScopeName = "Loaded Scenes",
                    Nodes = captureResult.Nodes.ToArray(),
                });
        }

        private bool TryCreateHierarchyDeltaFrameUnsafe(HierarchyCaptureResult captureResult, out byte[]? framedMessage)
        {
            var nodes = captureResult.Nodes;
            var currentNodes = new Dictionary<long, HierarchyNodeDtoV1>(nodes.Count);
            for (var index = 0; index < nodes.Count; index++)
            {
                currentNodes[nodes[index].NodeId] = nodes[index];
            }

            List<HierarchyNodeChangeDtoV1>? changes = null;
            long baseRevision;
            long revision;

            if (_publishedHierarchyRevision == 0 || _publishedHierarchyNodes.Count == 0)
            {
                framedMessage = Array.Empty<byte>();
                return false;
            }

            foreach (var published in _publishedHierarchyNodes)
            {
                if (!currentNodes.ContainsKey(published.Key))
                {
                    changes ??= new List<HierarchyNodeChangeDtoV1>();
                    changes.Add(new HierarchyNodeChangeDtoV1
                    {
                        ChangeKind = HierarchyChangeKind.Remove,
                        NodeId = published.Key,
                    });
                }
            }

            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (!_publishedHierarchyNodes.TryGetValue(node.NodeId, out var publishedNode) ||
                    !HierarchyNodeEquals(publishedNode, node))
                {
                    changes ??= new List<HierarchyNodeChangeDtoV1>();
                    changes.Add(CreateHierarchyNodeChange(node, HierarchyChangeKind.Upsert));
                }
            }

            if (changes == null || changes.Count == 0)
            {
                // full capture が成功したが差分が無かったケースでも、
                // token cache の stale entry はここで掃除しておく。
                PruneRuntimeNodeMappingsUnsafe(captureResult.SeenNodeIds);
                framedMessage = Array.Empty<byte>();
                return false;
            }

            baseRevision = _publishedHierarchyRevision;
            revision = Interlocked.Increment(ref _hierarchyRevision);
            ReplacePublishedHierarchyUnsafe(nodes, revision);
            PruneRuntimeNodeMappingsUnsafe(captureResult.SeenNodeIds);

            framedMessage = DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.HierarchyDelta,
                new HierarchyDeltaEnvelopeV1
                {
                    BaseRevision = baseRevision,
                    Revision = revision,
                    CapturedAtUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ScopeName = "Loaded Scenes",
                    Changes = changes.ToArray(),
                });
            return true;
        }

        private static HierarchyNodeChangeDtoV1 CreateHierarchyNodeChange(
            HierarchyNodeDtoV1 node,
            HierarchyChangeKind changeKind)
        {
            return new HierarchyNodeChangeDtoV1
            {
                ChangeKind = changeKind,
                NodeId = node.NodeId,
                ParentId = node.ParentId,
                TypeId = node.TypeId,
                Flags = node.Flags,
                Depth = node.Depth,
                SiblingIndex = node.SiblingIndex,
                ChildCount = node.ChildCount,
                TraversalIndex = node.TraversalIndex,
                Name = node.Name,
                TypeName = node.TypeName,
            };
        }

        private static bool HierarchyNodeEquals(HierarchyNodeDtoV1 left, HierarchyNodeDtoV1 right)
        {
            return left.NodeId == right.NodeId &&
                   left.ParentId == right.ParentId &&
                   left.TypeId == right.TypeId &&
                   left.Flags == right.Flags &&
                   left.Depth == right.Depth &&
                   left.SiblingIndex == right.SiblingIndex &&
                   left.ChildCount == right.ChildCount &&
                   left.TraversalIndex == right.TraversalIndex &&
                   string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
                   string.Equals(left.TypeName, right.TypeName, StringComparison.Ordinal);
        }

        private void ReplacePublishedHierarchyUnsafe(IReadOnlyList<HierarchyNodeDtoV1> nodes, long revision)
        {
            _publishedHierarchyNodes.Clear();
            for (var index = 0; index < nodes.Count; index++)
            {
                _publishedHierarchyNodes[nodes[index].NodeId] = nodes[index];
            }

            _publishedHierarchyRevision = revision;
        }

        private void ResetPublishedHierarchyUnsafe()
        {
            // cache 自体はセッション切替で消すが、
            // token counter までは戻さない。
            // 旧セッションから遅れて届いた TargetId が
            // 新セッションの別オブジェクトへ衝突しない方を優先する。
            _publishedHierarchyNodes.Clear();
            _publishedHierarchyRevision = 0;
            _runtimeIdentityToNodeIds.Clear();
            _nodeIdToRuntimeIdentities.Clear();
            _nodeIdToGameObjects.Clear();
        }

        private bool HasPublishedHierarchyStateUnsafe()
        {
            return _publishedHierarchyRevision != 0 && _publishedHierarchyNodes.Count != 0;
        }

        private byte[] CreateInspectorDetailFrame(InspectorQueryEnvelopeV1 query, string? requestId)
        {
            if (query.TargetId <= 0)
            {
                return DebugSocketProtocol.SerializeMessage(
                    DebugSocketMessageType.InspectorDetail,
                    new InspectorDetailEnvelopeV1
                    {
                        Revision = 0,
                        CapturedAtUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        TargetId = query.TargetId,
                        TargetName = "Unknown node",
                        TargetTypeId = 0,
                        TargetTypeName = null,
                        State = InspectorDetailState.NotFound,
                        Message = "Inspector target id was invalid.",
                        Sections = Array.Empty<InspectorSectionDtoV1>(),
                    },
                    requestId);
            }

            if (!TryFindGameObjectByNodeId(query.TargetId, out var scene, out var gameObject) || gameObject == null)
            {
                return DebugSocketProtocol.SerializeMessage(
                    DebugSocketMessageType.InspectorDetail,
                    new InspectorDetailEnvelopeV1
                    {
                        Revision = 0,
                        CapturedAtUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        TargetId = query.TargetId,
                        TargetName = $"Node {query.TargetId}",
                        TargetTypeId = 1,
                        TargetTypeName = nameof(GameObject),
                        State = InspectorDetailState.NotFound,
                        Message = "Hierarchy target was not found in loaded scenes.",
                        Sections = Array.Empty<InspectorSectionDtoV1>(),
                    },
                    requestId);
            }

            var revision = Interlocked.Increment(ref _inspectorRevision);
            var sections = BuildInspectorSections(query.TargetId, gameObject, scene, query.QueryFlags);
            return DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.InspectorDetail,
                new InspectorDetailEnvelopeV1
                {
                    Revision = revision,
                    CapturedAtUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    TargetId = query.TargetId,
                    TargetName = gameObject.name,
                    TargetTypeId = 1,
                    TargetTypeName = nameof(GameObject),
                    State = InspectorDetailState.Ready,
                    Message = "Inspector detail captured.",
                    Sections = sections,
                },
                requestId);
        }

        private byte[] CreateInspectorMainThreadUnavailableFrame(InspectorQueryEnvelopeV1 query, string? requestId)
        {
            return CreateInspectorFaultFrame(query, requestId, MainThreadContextUnavailableMessage);
        }

        private byte[] CreateInspectorFaultFrame(InspectorQueryEnvelopeV1 query, string? requestId, string message)
        {
            return DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.InspectorDetail,
                new InspectorDetailEnvelopeV1
                {
                    Revision = 0,
                    CapturedAtUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    TargetId = query.TargetId,
                    TargetName = $"Node {query.TargetId}",
                    TargetTypeId = 1,
                    TargetTypeName = nameof(GameObject),
                    State = InspectorDetailState.Faulted,
                    Message = message,
                    Sections = Array.Empty<InspectorSectionDtoV1>(),
                },
                requestId);
        }

        private static InspectorSectionDtoV1[] BuildInspectorSections(
            long targetId,
            GameObject gameObject,
            Scene scene,
            InspectorQueryFlags queryFlags)
        {
            var sections = new List<InspectorSectionDtoV1>();
            var sectionId = 1;
            var propertyId = 1;
            var includeMetadata = (queryFlags & InspectorQueryFlags.IncludeMetadata) != 0;
            var includeComponents = (queryFlags & InspectorQueryFlags.IncludeComponents) != 0;
            var includeProperties = (queryFlags & InspectorQueryFlags.IncludeProperties) != 0;
            var includeRawValues = (queryFlags & InspectorQueryFlags.IncludeRawValues) != 0;

            sections.Add(new InspectorSectionDtoV1
            {
                SectionId = sectionId++,
                Kind = InspectorSectionKind.Header,
                TypeId = 1,
                DisplayName = "GameObject",
                TypeName = nameof(GameObject),
                Properties = BuildGameObjectProperties(targetId, gameObject, scene, includeMetadata, includeRawValues, ref propertyId),
            });

            if (includeProperties)
            {
                sections.Add(new InspectorSectionDtoV1
                {
                    SectionId = sectionId++,
                    Kind = InspectorSectionKind.Component,
                    TypeId = 2,
                    DisplayName = "Transform",
                    TypeName = nameof(Transform),
                    Properties = BuildTransformProperties(gameObject.transform, includeRawValues, ref propertyId),
                });
            }

            if (includeComponents)
            {
                var components = gameObject.GetComponents<Component>();
                for (var index = 0; index < components.Length; index++)
                {
                    var component = components[index];
                    if (component == null || component is Transform)
                    {
                        continue;
                    }

                    sections.Add(new InspectorSectionDtoV1
                    {
                        SectionId = sectionId++,
                        Kind = InspectorSectionKind.Component,
                        TypeId = 3,
                        DisplayName = component.GetType().Name,
                        TypeName = component.GetType().FullName,
                        Properties = BuildComponentProperties(component, includeProperties, includeRawValues, ref propertyId),
                    });
                }
            }

            return sections.ToArray();
        }

        private static InspectorPropertyDtoV1[] BuildGameObjectProperties(
            long targetId,
            GameObject gameObject,
            Scene scene,
            bool includeMetadata,
            bool includeRawValues,
            ref int propertyId)
        {
            var properties = new List<InspectorPropertyDtoV1>
            {
                CreateInspectorProperty(ref propertyId, "Name", gameObject.name, ValueTypeId.Utf16String, path: "GameObject.Name"),
                CreateInspectorProperty(ref propertyId, "ActiveSelf", gameObject.activeSelf, ValueTypeId.Boolean, path: "GameObject.ActiveSelf"),
                CreateInspectorProperty(ref propertyId, "ActiveInHierarchy", gameObject.activeInHierarchy, ValueTypeId.Boolean, path: "GameObject.ActiveInHierarchy"),
                CreateInspectorProperty(ref propertyId, "ChildCount", gameObject.transform.childCount, ValueTypeId.Int32, path: "GameObject.ChildCount"),
                CreateInspectorProperty(ref propertyId, "SiblingIndex", gameObject.transform.GetSiblingIndex(), ValueTypeId.Int32, path: "GameObject.SiblingIndex"),
            };

            if (includeMetadata)
            {
                // 以前は Unity 内部の InstanceId / SceneHandle をそのまま露出していたが、
                // Unity 6.5 以降は API 変更の影響を強く受けるうえ、
                // viewer 側が本当に必要としているのは「このノードを安定して識別できること」だけだった。
                // そこで inspector metadata には service-local token を文字列で載せ、
                // 生の engine identity は wire へ出さないようにする。
                properties.Add(CreateInspectorProperty(ref propertyId, "Scene", scene.name, ValueTypeId.Utf16String, path: "GameObject.Scene"));
                properties.Add(CreateInspectorProperty(ref propertyId, "Tag", gameObject.tag, ValueTypeId.Utf16String, path: "GameObject.Tag"));
                properties.Add(CreateInspectorProperty(ref propertyId, "Layer", gameObject.layer, ValueTypeId.Int32, path: "GameObject.Layer"));
                properties.Add(CreateInspectorProperty(ref propertyId, "NodeToken", targetId.ToString(CultureInfo.InvariantCulture), ValueTypeId.Utf16String, path: "GameObject.NodeToken"));
            }

            if (includeRawValues)
            {
                for (var index = 0; index < properties.Count; index++)
                {
                    // bool など、すでに canonical な rawValue を持っている項目は上書きしない。
                    // display 用の "True"/"False" で raw を潰すと、viewer 側で機械処理しづらくなる。
                    properties[index].RawValue ??= properties[index].ValueText;
                }
            }

            return properties.ToArray();
        }

        private static InspectorPropertyDtoV1[] BuildTransformProperties(
            Transform transform,
            bool includeRawValues,
            ref int propertyId)
        {
            var properties = new List<InspectorPropertyDtoV1>
            {
                CreateInspectorProperty(ref propertyId, "Parent", transform.parent == null ? "(root)" : transform.parent.name, ValueTypeId.Utf16String, path: "Transform.Parent"),
                CreateInspectorProperty(ref propertyId, "LocalPosition", FormatVector3(transform.localPosition), ValueTypeId.Utf16String, path: "Transform.LocalPosition", rawValue: includeRawValues ? FormatVector3Raw(transform.localPosition) : null),
                CreateInspectorProperty(ref propertyId, "LocalRotation", FormatVector3(transform.localEulerAngles), ValueTypeId.Utf16String, path: "Transform.LocalEulerAngles", rawValue: includeRawValues ? FormatVector3Raw(transform.localEulerAngles) : null, unit: "deg"),
                CreateInspectorProperty(ref propertyId, "LocalScale", FormatVector3(transform.localScale), ValueTypeId.Utf16String, path: "Transform.LocalScale", rawValue: includeRawValues ? FormatVector3Raw(transform.localScale) : null),
                CreateInspectorProperty(ref propertyId, "Position", FormatVector3(transform.position), ValueTypeId.Utf16String, path: "Transform.Position", rawValue: includeRawValues ? FormatVector3Raw(transform.position) : null),
            };

            return properties.ToArray();
        }

        private static InspectorPropertyDtoV1[] BuildComponentProperties(
            Component component,
            bool includeProperties,
            bool includeRawValues,
            ref int propertyId)
        {
            // component 単位の内部 ID は hierarchy / inspector 往復には使っていない。
            // ここで engine 依存の identity を露出すると将来また同じ種類の破綻を招くため、
            // metadata は type 名と公開プロパティ中心に絞る。
            var properties = new List<InspectorPropertyDtoV1>
            {
                CreateInspectorProperty(ref propertyId, "Type", component.GetType().Name, ValueTypeId.Utf16String, path: "Component.Type"),
            };

            if (component is Behaviour behaviour)
            {
                properties.Add(CreateInspectorProperty(ref propertyId, "Enabled", behaviour.enabled, ValueTypeId.Boolean, path: $"{component.GetType().Name}.Enabled"));
            }

            if (!includeProperties)
            {
                return properties.ToArray();
            }

            if (component is Renderer renderer)
            {
                properties.Add(CreateInspectorProperty(ref propertyId, "SortingLayerId", renderer.sortingLayerID, ValueTypeId.Int32, path: $"{component.GetType().Name}.SortingLayerId"));
                properties.Add(CreateInspectorProperty(ref propertyId, "SortingOrder", renderer.sortingOrder, ValueTypeId.Int32, path: $"{component.GetType().Name}.SortingOrder"));
            }

            if (component is Collider collider)
            {
                properties.Add(CreateInspectorProperty(ref propertyId, "IsTrigger", collider.isTrigger, ValueTypeId.Boolean, path: $"{component.GetType().Name}.IsTrigger"));
            }

            if (component is Rigidbody rigidbody)
            {
                properties.Add(CreateInspectorProperty(ref propertyId, "Mass", rigidbody.mass, ValueTypeId.Float64, path: $"{component.GetType().Name}.Mass", rawValue: includeRawValues ? rigidbody.mass.ToString("R", CultureInfo.InvariantCulture) : null));
                properties.Add(CreateInspectorProperty(ref propertyId, "UseGravity", rigidbody.useGravity, ValueTypeId.Boolean, path: $"{component.GetType().Name}.UseGravity"));
                properties.Add(CreateInspectorProperty(ref propertyId, "IsKinematic", rigidbody.isKinematic, ValueTypeId.Boolean, path: $"{component.GetType().Name}.IsKinematic"));
            }

            if (component is AudioSource audioSource)
            {
                properties.Add(CreateInspectorProperty(ref propertyId, "Volume", audioSource.volume, ValueTypeId.Float64, path: $"{component.GetType().Name}.Volume", rawValue: includeRawValues ? audioSource.volume.ToString("R", CultureInfo.InvariantCulture) : null));
                properties.Add(CreateInspectorProperty(ref propertyId, "Loop", audioSource.loop, ValueTypeId.Boolean, path: $"{component.GetType().Name}.Loop"));
                properties.Add(CreateInspectorProperty(ref propertyId, "PlayOnAwake", audioSource.playOnAwake, ValueTypeId.Boolean, path: $"{component.GetType().Name}.PlayOnAwake"));
            }

            if (component is Camera camera)
            {
                properties.Add(CreateInspectorProperty(ref propertyId, "FieldOfView", camera.fieldOfView, ValueTypeId.Float64, path: $"{component.GetType().Name}.FieldOfView", rawValue: includeRawValues ? camera.fieldOfView.ToString("R", CultureInfo.InvariantCulture) : null, unit: "deg"));
                properties.Add(CreateInspectorProperty(ref propertyId, "NearClipPlane", camera.nearClipPlane, ValueTypeId.Float64, path: $"{component.GetType().Name}.NearClipPlane", rawValue: includeRawValues ? camera.nearClipPlane.ToString("R", CultureInfo.InvariantCulture) : null));
                properties.Add(CreateInspectorProperty(ref propertyId, "FarClipPlane", camera.farClipPlane, ValueTypeId.Float64, path: $"{component.GetType().Name}.FarClipPlane", rawValue: includeRawValues ? camera.farClipPlane.ToString("R", CultureInfo.InvariantCulture) : null));
            }

            return properties.ToArray();
        }

        private static InspectorPropertyDtoV1 CreateInspectorProperty(
            ref int propertyId,
            string displayName,
            string valueText,
            int valueTypeId,
            string? path = null,
            string? rawValue = null,
            string? unit = null)
        {
            return new InspectorPropertyDtoV1
            {
                PropertyId = propertyId++,
                ValueTypeId = valueTypeId,
                Flags = InspectorPropertyFlags.ReadOnly,
                DisplayName = displayName,
                ValueText = valueText,
                RawValue = rawValue,
                Unit = unit,
                Path = path,
            };
        }

        private static InspectorPropertyDtoV1 CreateInspectorProperty(
            ref int propertyId,
            string displayName,
            bool value,
            int valueTypeId,
            string? path = null)
        {
            return CreateInspectorProperty(ref propertyId, displayName, value ? "True" : "False", valueTypeId, path, value ? "true" : "false");
        }

        private static InspectorPropertyDtoV1 CreateInspectorProperty(
            ref int propertyId,
            string displayName,
            int value,
            int valueTypeId,
            string? path = null)
        {
            var invariant = value.ToString(CultureInfo.InvariantCulture);
            return CreateInspectorProperty(ref propertyId, displayName, invariant, valueTypeId, path, invariant);
        }

        private static InspectorPropertyDtoV1 CreateInspectorProperty(
            ref int propertyId,
            string displayName,
            float value,
            int valueTypeId,
            string? path = null,
            string? rawValue = null,
            string? unit = null)
        {
            return CreateInspectorProperty(ref propertyId, displayName, value.ToString("0.###", CultureInfo.InvariantCulture), valueTypeId, path, rawValue, unit);
        }

        private static string FormatVector3(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0:0.###}, {1:0.###}, {2:0.###})",
                value.x,
                value.y,
                value.z);
        }

        private static string FormatVector3Raw(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:R},{1:R},{2:R}",
                value.x,
                value.y,
                value.z);
        }

        private bool TryFindGameObjectByNodeId(long targetId, out Scene scene, out GameObject? gameObject)
        {
            lock (_gate)
            {
                if (!_nodeIdToGameObjects.TryGetValue(targetId, out gameObject) || gameObject == null)
                {
                    RemoveRuntimeNodeMappingUnsafe(targetId);
                    scene = default;
                    gameObject = null;
                    return false;
                }

                scene = gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    RemoveRuntimeNodeMappingUnsafe(targetId);
                    gameObject = null;
                    scene = default;
                    return false;
                }

                return true;
            }
        }

        private static class ValueTypeId
        {
            public const int Boolean = 1;
            public const int Int32 = 2;
            public const int Float64 = 4;
            public const int Utf16String = 5;
        }

        /// <summary>
        /// hierarchy / inspector 往復で共有する service-local token を返す。
        ///
        /// <para>
        /// 以前は SceneHandle + Unity object identity を合成した値を wire に流していたが、
        /// Unity 6.5 以降はその前提が obsolete API に強く依存してしまう。
        /// そこで wire には service が管理する token だけを流し、
        /// Unity 内部の identity はこの service のキャッシュ内へ閉じ込める。
        /// </para>
        /// </summary>
        private long CreateRuntimeNodeIdUnsafe(GameObject gameObject)
        {
            var runtimeIdentityKey = EntityId.ToULong(gameObject.GetEntityId());

            if (_runtimeIdentityToNodeIds.TryGetValue(runtimeIdentityKey, out var existingNodeId))
            {
                if (_nodeIdToGameObjects.TryGetValue(existingNodeId, out var existingGameObject) &&
                    existingGameObject == gameObject)
                {
                    return existingNodeId;
                }

                // internal key が再利用されたか、reverse 側が stale になっている。
                // 旧 mapping を残したまま再利用すると別オブジェクトへ query が誤着弾するため、
                // token は使い回さず新しく採番し直す。
                RemoveRuntimeNodeMappingUnsafe(existingNodeId);
            }

            var nodeId = _nextRuntimeNodeId++;
            _runtimeIdentityToNodeIds[runtimeIdentityKey] = nodeId;
            _nodeIdToRuntimeIdentities[nodeId] = runtimeIdentityKey;
            _nodeIdToGameObjects[nodeId] = gameObject;
            return nodeId;
        }

        /// <summary>
        /// hierarchy / inspector 導線では root 配列を毎回 new しないよう、
        /// service 内で再利用する list へ scene roots を詰める。
        ///
        /// <para>
        /// main-thread 限定で使う前提なので、1 本の scratch list を使い回す。
        /// これだけでも `Scene.GetRootGameObjects()` の配列確保を避けられる。
        /// </para>
        /// </summary>
        private List<GameObject> GetRootGameObjectsNonAlloc(Scene scene)
        {
            _rootGameObjectBuffer.Clear();
            scene.GetRootGameObjects(_rootGameObjectBuffer);
            return _rootGameObjectBuffer;
        }

        /// <summary>
        /// token から forward / reverse の両方を整合したまま外す。
        ///
        /// <para>
        /// stale query, hierarchy prune, session reset のすべてがここを通ることで、
        /// 片側だけ残った half-broken mapping を作らないようにする。
        /// </para>
        /// </summary>
        private void RemoveRuntimeNodeMappingUnsafe(long nodeId)
        {
            if (_nodeIdToRuntimeIdentities.TryGetValue(nodeId, out var runtimeIdentityKey))
            {
                if (_runtimeIdentityToNodeIds.TryGetValue(runtimeIdentityKey, out var mappedNodeId) &&
                    mappedNodeId == nodeId)
                {
                    _runtimeIdentityToNodeIds.Remove(runtimeIdentityKey);
                }

                _nodeIdToRuntimeIdentities.Remove(nodeId);
            }

            _nodeIdToGameObjects.Remove(nodeId);
        }

        /// <summary>
        /// 今回の full capture に現れなかった token をまとめて掃除する。
        ///
        /// <para>
        /// capture が成功した後だけ呼ぶ。
        /// 途中失敗した不完全な seen 集合で prune すると、生きている object の token まで消してしまうため。
        /// </para>
        /// </summary>
        private void PruneRuntimeNodeMappingsUnsafe(HashSet<long> seenNodeIds)
        {
            List<long>? staleNodeIds = null;
            foreach (var pair in _nodeIdToRuntimeIdentities)
            {
                if (seenNodeIds.Contains(pair.Key))
                {
                    continue;
                }

                staleNodeIds ??= new List<long>();
                staleNodeIds.Add(pair.Key);
            }

            if (staleNodeIds == null)
            {
                return;
            }

            for (var index = 0; index < staleNodeIds.Count; index++)
            {
                RemoveRuntimeNodeMappingUnsafe(staleNodeIds[index]);
            }
        }

        /// <summary>
        /// 単一クライアントの送受信を担当する内部セッション。
        ///
        /// <para>
        /// WebSocket は同時 Send に弱いので、
        /// logger / telemetry / command result の送信はすべて 1 本の queue に集約する。
        /// </para>
        /// </summary>
        /// <summary>
        /// 送信キューに積む 1 フレーム分の所有権を表す軽量 DTO。
        ///
        /// <para>
        /// realtime log は ArrayPool から借りたバッファをそのまま載せ、
        /// protocol helper が返す通常配列は owned 配列としてそのまま扱う。
        /// これにより hot path の log frame だけでも GC を大きく減らせる。
        /// </para>
        /// </summary>
        private readonly struct OutgoingFrame
        {
            public readonly byte[]? Buffer;
            public readonly int Count;
            public readonly bool ReturnToPool;

            private OutgoingFrame(byte[]? buffer, int count, bool returnToPool)
            {
                Buffer = buffer;
                Count = count;
                ReturnToPool = returnToPool;
            }

            public bool IsEmpty => Buffer == null || Count <= 0;

            public static OutgoingFrame CreateOwned(byte[] buffer)
            {
                return new OutgoingFrame(buffer, buffer?.Length ?? 0, returnToPool: false);
            }

            public static OutgoingFrame CreatePooled(byte[] buffer, int count)
            {
                return new OutgoingFrame(buffer, count, returnToPool: true);
            }

            public ArraySegment<byte> AsSegment()
            {
                return new ArraySegment<byte>(Buffer!, 0, Count);
            }

            public void Release()
            {
                if (ReturnToPool && Buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(Buffer);
                }
            }
        }

        private sealed class ClientSession
        {
            private readonly object _queueGate = new();
            private readonly DebugSocketService _owner;
            private readonly WebSocket _socket;
            private readonly int _maxQueueLength;
            private readonly CancellationTokenSource _cts;
            private readonly Queue<OutgoingFrame> _outgoingMessages = new();
            private readonly SemaphoreSlim _queueSignal = new(0);
            private readonly UniTaskCompletionSource _completionSource = new();

            private UniTask? _sendLoopTask;
            private UniTask? _receiveLoopTask;
            private int _closeStarted;
            private bool _cleanedUp;

            public ClientSession(
                DebugSocketService owner,
                WebSocket socket,
                int maxQueueLength,
                CancellationToken serviceCancellationToken)
            {
                _owner = owner;
                _socket = socket;
                _maxQueueLength = maxQueueLength;
                _cts = CancellationTokenSource.CreateLinkedTokenSource(serviceCancellationToken);
            }

            public string SessionId { get; } = Guid.NewGuid().ToString("N");
            public UniTask Completion => _completionSource.Task;
            public int PendingQueueLength
            {
                get
                {
                    lock (_queueGate)
                    {
                        return _outgoingMessages.Count;
                    }
                }
            }

            /// <summary>
            /// capability hello 後に確定した、このセッション向けの negotiated capability。
            /// scene event 側から差分送信可否を判定するため、session に保持する。
            /// </summary>
            public bool HasCompletedCapabilityHello { get; set; }

            public DebugStudioCapability NegotiatedCapabilities { get; set; } = DebugStudioCapability.None;

            /// <summary>
            /// send / receive の両ループを起動する。
            /// セッション生成直後に一度だけ呼ばれる想定。
            /// </summary>
            public void Start()
            {
                // ActivateSessionAsync では current へ公開してから Start するため、
                // 極端な再接続/停止 race では「Start 前に close 済み」になり得る。
                // その場合に ObjectDisposedException を投げるより、
                // 既に閉じられたセッションとして静かに起動を諦める。
                if (Volatile.Read(ref _closeStarted) != 0 || _cleanedUp || _cts.IsCancellationRequested)
                {
                    CloseAsync("start-skipped", CancellationToken.None).Forget();
                    return;
                }

                try
                {
                    _sendLoopTask = SendLoopAsync(_cts.Token);
                    _receiveLoopTask = ReceiveLoopAsync(_cts.Token);
                }
                catch (ObjectDisposedException)
                {
                }
            }

            /// <summary>
            /// 送信キューへメッセージを積む。
            /// v1 方針どおり bounded queue + oldest drop をここで実装する。
            /// </summary>
            public void Enqueue(byte[] framedMessage)
            {
                Enqueue(OutgoingFrame.CreateOwned(framedMessage));
            }

            public void Enqueue(in OutgoingFrame framedMessage)
            {
                var shouldSignal = false;
                var droppedCount = 0;
                lock (_queueGate)
                {
                    if (framedMessage.IsEmpty || _cts.IsCancellationRequested || Volatile.Read(ref _closeStarted) != 0 || _cleanedUp)
                    {
                        framedMessage.Release();
                        return;
                    }

                    while (_outgoingMessages.Count >= _maxQueueLength)
                    {
                        // 最新の観測を優先したいので、古いものから捨てる。
                        var droppedFrame = _outgoingMessages.Dequeue();
                        droppedFrame.Release();
                        droppedCount++;
                    }

                    _outgoingMessages.Enqueue(framedMessage);
                    shouldSignal = true;
                }

                if (droppedCount > 0)
                {
                    _owner.RecordQueueOverflowDrops(droppedCount);
                }

                if (!shouldSignal)
                {
                    return;
                }

                try
                {
                    _queueSignal.Release();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            /// <summary>
            /// 外側から明示停止するときの close。
            /// 停止処理では send / receive の両 loop 終了も待ち合わせる。
            /// </summary>
            public UniTask CloseAsync(string reason, CancellationToken cancellationToken)
            {
                return CloseCoreAsync(reason, cancellationToken, awaitSendLoop: true, awaitReceiveLoop: true);
            }

            /// <summary>
            /// receive loop 自身から close を要求するときの入口。
            /// 自分自身の receive task を await しない構成に固定する。
            /// </summary>
            public UniTask CloseFromReceiveLoopAsync(string reason)
            {
                return CloseCoreAsync(reason, CancellationToken.None, awaitSendLoop: true, awaitReceiveLoop: false);
            }

            /// <summary>
            /// queue から順に取り出し、WebSocket へ 1 メッセージずつ送る。
            /// ここ以外から SendAsync しないことで同時送信競合を防ぐ。
            /// </summary>
            private async UniTask SendLoopAsync(CancellationToken cancellationToken)
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await _queueSignal.WaitAsync(cancellationToken).AsUniTask();

                        while (TryDequeue(out var framedMessage))
                        {
                            if (_socket.State != WebSocketState.Open)
                            {
                                framedMessage.Release();
                                return;
                            }

                            try
                            {
                                await _socket.SendAsync(
                                        framedMessage.AsSegment(),
                                        WebSocketMessageType.Binary,
                                        true,
                                        cancellationToken)
                                    .AsUniTask();
                            }
                            finally
                            {
                                // send 完了後、または例外で中断した時点で ownership を閉じる。
                                framedMessage.Release();
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (WebSocketException)
                {
                    // 送信失敗はセッション継続不能として扱い、自分自身を閉じる。
                    await CloseCoreAsync("send-loop-ended", CancellationToken.None, awaitSendLoop: false, awaitReceiveLoop: true);
                }
            }

            /// <summary>
            /// WebSocket から binary message を受け取り、protocol として service へ渡す。
            /// text frame は v1 では不採用なので protocol error とする。
            /// </summary>
            private async UniTask ReceiveLoopAsync(CancellationToken cancellationToken)
            {
                var receiveBuffer = new byte[8192];
                using var memoryStream = new MemoryStream(8192);

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        memoryStream.SetLength(0);

                        WebSocketReceiveResult receiveResult;
                        do
                        {
                            receiveResult = await _socket.ReceiveAsync(
                                    new ArraySegment<byte>(receiveBuffer),
                                    cancellationToken)
                                .AsUniTask();

                            if (receiveResult.MessageType == WebSocketMessageType.Close)
                            {
                                return;
                            }

                            if (receiveResult.Count > 0)
                            {
                                memoryStream.Write(receiveBuffer, 0, receiveResult.Count);
                                if (memoryStream.Length > MaxInboundMessageBytes)
                                {
                                    Enqueue(_owner.CreateServiceStatus(
                                        "protocol-error",
                                        $"Inbound message exceeded {MaxInboundMessageBytes.ToString(CultureInfo.InvariantCulture)} bytes."));
                                    await CloseFromReceiveLoopAsync("message-too-large");
                                    return;
                                }
                            }
                        }
                        while (!receiveResult.EndOfMessage);

                        if (receiveResult.MessageType != WebSocketMessageType.Binary)
                        {
                            Enqueue(_owner.CreateServiceStatus("protocol-error", "Only binary WebSocket messages are supported."));
                            continue;
                        }

                        // ToArray() は毎メッセージ新しい配列を作るため、
                        // 高頻度 log/telemetry では GC 圧が無視できなくなる。
                        // TryGetBuffer() で内部バッファを直接参照し、今回有効な長さだけを slice して渡す。
                        if (!memoryStream.TryGetBuffer(out var segment))
                        {
                            Enqueue(_owner.CreateServiceStatus("protocol-error", "Failed to access the receive buffer."));
                            continue;
                        }

                        await _owner.HandleInboundMessageAsync(
                            this,
                            new ReadOnlyMemory<byte>(segment.Array!, segment.Offset, (int)memoryStream.Length),
                            cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (WebSocketException)
                {
                }
                finally
                {
                    // receive loop 自身から閉じるので、自分自身の await はしない。
                    await CloseCoreAsync("receive-loop-ended", CancellationToken.None, awaitSendLoop: true, awaitReceiveLoop: false);
                }
            }

            /// <summary>
            /// 共通 close 実装。
            /// 呼び出し元が send/receive loop 自身かどうかで待ち合わせ対象を切り替える。
            /// </summary>
            private async UniTask CloseCoreAsync(
                string reason,
                CancellationToken cancellationToken,
                bool awaitSendLoop,
                bool awaitReceiveLoop)
            {
                // close の主導権は必ず 1 経路だけが取る。
                // send loop / receive loop / 外部 CloseAsync が同時に来ても、
                // 後続は sibling loop を待たずに completion だけ待つ。
                // そうしないと send 側が receive を待ち、receive 側が send を待つ相互待機が起こり得る。
                if (Interlocked.Exchange(ref _closeStarted, 1) != 0)
                {
                    await Completion.SuppressCancellationThrow();
                    return;
                }

                _cts.Cancel();

                if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    try
                    {
                        using var closeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        closeCts.CancelAfter(WebSocketCloseTimeout);
                        await _socket.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                reason,
                                closeCts.Token)
                            .AsUniTask();
                    }
                    catch (OperationCanceledException)
                    {
                        _socket.Abort();
                    }
                    catch
                    {
                    }
                }

                if (awaitSendLoop && _sendLoopTask.HasValue)
                {
                    await _sendLoopTask.Value.SuppressCancellationThrow();
                }

                if (awaitReceiveLoop && _receiveLoopTask.HasValue)
                {
                    await _receiveLoopTask.Value.SuppressCancellationThrow();
                }

                CleanupManagedResources();
            }

            /// <summary>
            /// セッションの後始末を一度だけ実行する。
            /// 複数経路から close されても二重 dispose しないよう guard を入れている。
            /// </summary>
            private void CleanupManagedResources()
            {
                if (_cleanedUp)
                {
                    return;
                }

                _cleanedUp = true;
                ReleasePendingOutgoingMessages();
                _queueSignal.Dispose();
                _cts.Dispose();
                _socket.Dispose();
                _owner.OnSessionClosed(this);
                _completionSource.TrySetResult();
            }

            private bool TryDequeue(out OutgoingFrame framedMessage)
            {
                lock (_queueGate)
                {
                    if (_outgoingMessages.Count > 0)
                    {
                        framedMessage = _outgoingMessages.Dequeue();
                        return true;
                    }
                }

                framedMessage = default;
                return false;
            }

            private void ReleasePendingOutgoingMessages()
            {
                lock (_queueGate)
                {
                    while (_outgoingMessages.Count > 0)
                    {
                        var pendingFrame = _outgoingMessages.Dequeue();
                        pendingFrame.Release();
                    }
                }
            }
        }
    }
}
