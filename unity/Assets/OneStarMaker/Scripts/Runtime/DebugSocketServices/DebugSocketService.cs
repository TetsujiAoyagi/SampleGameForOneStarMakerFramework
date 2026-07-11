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
using OneStarMaker.Runtime.DebugSocketServices.Protocol;
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
    ///
    /// <para>
    /// partial ごとの責務:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>DebugSocketService.cs</c> — 公開 API、共有フィールド、起動/停止、共通 helper</description></item>
    /// <item><description><c>DebugSocketService.Transport.cs</c> — transport host 連携の予約 partial</description></item>
    /// <item><description><c>DebugSocketService.Session.cs</c> — 送信キュー橋渡し、session 置換、activation</description></item>
    /// <item><description><c>DebugSocketService.Inbound.cs</c> — inbound router 委譲、frame 生成 callback、capability 広告</description></item>
    /// <item><description><c>Protocol/DebugSocketInboundMessageRouter.cs</c> — envelope decode、message type routing、stale session 防御</description></item>
    /// <item><description><c>DebugSocketService.Hierarchy.cs</c> — hierarchy capture/delta、token registry、scene callbacks</description></item>
    /// <item><description><c>DebugSocketService.Inspector.cs</c> — inspector DTO 構築、target lookup、<c>ValueTypeId</c></description></item>
    /// </list>
    ///
    /// <para>
    /// 共有状態（<c>_currentSession</c>、hierarchy 正本、token cache など）は <c>_gate</c> で一括保護する。
    /// </para>
    /// </summary>
    public sealed partial class DebugSocketService : IDisposable, IDebugSocketClientSessionHost, IDebugSocketTransportHostCallbacks
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
        private readonly DebugSocketRuntimeNodeRegistry _runtimeNodeRegistry = new();
        private readonly DebugSocketHierarchyPublisher _hierarchyPublisher;
        private readonly SynchronizationContext? _mainThreadContext;

        private readonly DebugSocketInboundMessageRouter _inboundMessageRouter;
        private readonly DebugSocketTransportHost _transportHost;
        private CancellationTokenSource? _cts;
        private DebugSocketClientSession? _currentSession;
        private long _inspectorRevision;
        private long _droppedBeforeSessionCount;
        private long _droppedQueueOverflowCount;
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
            _hierarchyPublisher = new DebugSocketHierarchyPublisher(_runtimeNodeRegistry);
            _inboundMessageRouter = CreateInboundMessageRouter();
            _transportHost = new DebugSocketTransportHost(Options, this);

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
            DebugSocketClientSession? session;
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
                        _transportHost.StartListener(_cts.Token);
                        break;

                    case DebugSocketTransportMode.Connect:
                        _transportHost.StartOutbound(_cts.Token);
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

                _transportHost.AbortListenerOnStartFailure();
                _cts?.Dispose();
                _cts = null;
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
            if (_cts == null)
            {
                return;
            }

            // まず loop 側へ停止を通知する。
            _cts?.Cancel();

            // 現在のクライアントセッションを閉じる。
            DebugSocketClientSession? sessionToClose;
            lock (_gate)
            {
                sessionToClose = _currentSession;
                _currentSession = null;
                ResetPublishedHierarchyUnsafe();
            }

            // GetContextAsync を抜けさせるため listener 自体も止める。
            _transportHost.StopListener();

            if (sessionToClose != null)
            {
                await sessionToClose.CloseAsync("service-stopping", CancellationToken.None);
            }

            // transport loop の終了も待ってから token source を片付ける。
            await _transportHost.WaitForTransportLoopAsync();

            _cts?.Dispose();
            _cts = null;
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

            EnqueueOutgoingMessage(DebugSocketOutgoingFrame.CreatePooled(framedMessageBuffer, count));
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

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DebugSocketService));
            }
        }

        private async UniTask SwitchToMainThreadAsync(CancellationToken cancellationToken)
        {
            await new SwitchToSynchronizationContextAwaitable(_mainThreadContext, cancellationToken);
        }
    }
}
