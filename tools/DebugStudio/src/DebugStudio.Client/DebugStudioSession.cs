#nullable enable

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Client.Internal;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Client;

public sealed class DebugStudioSession : IDebugStudioCommandSession
{
    private readonly DebugSocketConnectionLifecycle _lifecycle;
    private readonly DebugSocketSendGateway _sendGateway;
    private readonly DebugSocketReceiveLoop _receiveLoop;
    private readonly DebugSocketInboundRouter _inboundRouter;

    private Task? _receiveLoopTask;
    private bool _disposed;

    public DebugStudioSession()
    {
        _lifecycle = new DebugSocketConnectionLifecycle();
        _sendGateway = new DebugSocketSendGateway(_lifecycle);
        _inboundRouter = new DebugSocketInboundRouter();
        _receiveLoop = new DebugSocketReceiveLoop(_inboundRouter);

        _inboundRouter.LogReceived += OnLogReceived;
        _inboundRouter.TelemetryReceived += OnTelemetryReceived;
        _inboundRouter.ServiceStatusReceived += OnServiceStatusReceived;
        _inboundRouter.CommandResultReceived += OnCommandResultReceived;
        _inboundRouter.CapabilityWelcomeReceived += OnCapabilityWelcomeReceived;
        _inboundRouter.HierarchySnapshotReceived += OnHierarchySnapshotReceived;
        _inboundRouter.HierarchyDeltaReceived += OnHierarchyDeltaReceived;
        _inboundRouter.InspectorDetailReceived += OnInspectorDetailReceived;
    }

    public DebugSocketConnectionState State { get; private set; } = DebugSocketConnectionState.Disconnected;

    public Uri? ServerUri { get; private set; }

    public event Action<DebugSocketConnectionSnapshot>? ConnectionStateChanged;
    public event Action<LogEnvelopeV1>? LogReceived;
    public event Action<DebugTelemetryEnvelopeV1>? TelemetryReceived;
    public event Action<DebugSocketServiceStatusEnvelopeV1>? ServiceStatusReceived;
    public event Action<DebugCommandResultEnvelopeV1>? CommandResultReceived;
    public event Action<CapabilityHandshakeWelcomeEnvelopeV1>? CapabilityWelcomeReceived;
    public event Action<HierarchySnapshotEnvelopeV1>? HierarchySnapshotReceived;
    public event Action<HierarchyDeltaEnvelopeV1>? HierarchyDeltaReceived;
    public event Action<InspectorDetailEnvelopeV1>? InspectorDetailReceived;

    private void OnLogReceived(LogEnvelopeV1 log) => LogReceived?.Invoke(log);
    private void OnTelemetryReceived(DebugTelemetryEnvelopeV1 telemetry) => TelemetryReceived?.Invoke(telemetry);
    private void OnServiceStatusReceived(DebugSocketServiceStatusEnvelopeV1 status) => ServiceStatusReceived?.Invoke(status);
    private void OnCommandResultReceived(DebugCommandResultEnvelopeV1 result) => CommandResultReceived?.Invoke(result);
    private void OnCapabilityWelcomeReceived(CapabilityHandshakeWelcomeEnvelopeV1 welcome) => CapabilityWelcomeReceived?.Invoke(welcome);
    private void OnHierarchySnapshotReceived(HierarchySnapshotEnvelopeV1 snapshot) => HierarchySnapshotReceived?.Invoke(snapshot);
    private void OnHierarchyDeltaReceived(HierarchyDeltaEnvelopeV1 delta) => HierarchyDeltaReceived?.Invoke(delta);
    private void OnInspectorDetailReceived(InspectorDetailEnvelopeV1 detail) => InspectorDetailReceived?.Invoke(detail);

    /// <summary>
    /// Unity 側 DebugSocket サーバーへ接続する。
    ///
    /// <para>
    /// このメソッドは「接続成功後に receive loop を起動する」までを責務とし、
    /// 受信後の payload 振り分けは内部の inbound router へ委譲する。
    /// </para>
    /// </summary>
    public async Task ConnectAsync(DebugSocketClientOptions options, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(options);

        // state は「接続要求を受けた」時点で先に Connecting へ進める。
        // 実 socket connect が遅い環境でも UI/上位 service が待機状態を認識できるようにするため。
        UpdateState(DebugSocketConnectionState.Connecting, options.ServerUri, $"Connecting to {options.ServerUri}...");

        ClientWebSocket? socket = null;
        try
        {
            // lifecycle collaborator が socket 作成・keepalive・origin header・CTS 構築を担当する。
            socket = await _lifecycle.ConnectAsync(options, cancellationToken).ConfigureAwait(false);

            // receive loop は fire-and-track で開始する。
            // 受信完了や fault の最終処理は StartReceiveLoopAsync → FinalizeReceiveLoopAsync 側へ流す。
            _receiveLoopTask = StartReceiveLoopAsync(socket);

            UpdateState(DebugSocketConnectionState.Connected, options.ServerUri, $"Connected to {options.ServerUri}.");
        }
        catch (Exception ex)
        {
            UpdateState(DebugSocketConnectionState.Faulted, options.ServerUri, ex.Message);
            throw;
        }
    }

    private async Task StartReceiveLoopAsync(ClientWebSocket socket)
    {
        // receive loop は session token に従って終了する。
        // ここでは「loop の実行」と「終端処理の統一」を 1 メソッドに閉じ込める。
        var sessionToken = _lifecycle.SessionToken;
        var result = await _receiveLoop.RunAsync(socket, sessionToken).ConfigureAwait(false);
        await FinalizeReceiveLoopAsync(socket, result.FatalError, result.Detail).ConfigureAwait(false);
    }

    /// <summary>
    /// 現在のセッションを安全に切断する。
    ///
    /// <para>
    /// 先に参照を切ってから close/dispose へ進むことで、
    /// 他スレッドから古い socket を再利用されないようにしている。
    /// </para>
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var serverUri = ServerUri;
        UpdateState(DebugSocketConnectionState.Disconnecting, serverUri, "Disconnecting...");

        // lifecycle 側で現役 socket 参照を 먼저切り離し、
        // 以後の send が古い socket を再利用しないようにしてから close/dispose へ進む。
        var disconnectResult = await _lifecycle.DisconnectAsync(cancellationToken).ConfigureAwait(false);

        if (disconnectResult.Socket == null)
        {
            UpdateState(DebugSocketConnectionState.Disconnected, serverUri, "Disconnected.");
            return;
        }

        Task? receiveLoopTask = _receiveLoopTask;
        _receiveLoopTask = null;

        try
        {
            if (receiveLoopTask != null)
            {
                try
                {
                    // receive loop の終了を待つことで、dispose 中に並行して state 更新が飛ぶ競合を減らす。
                    await receiveLoopTask.ConfigureAwait(false);
                }
                catch
                {
                    // disconnect 中は receive loop 側の例外をここで再送出しても回復不能なので握りつぶす。
                }
            }
        }
        finally
        {
            disconnectResult.Socket.Dispose();

            if (!_disposed)
            {
                UpdateState(DebugSocketConnectionState.Disconnected, serverUri, "Disconnected.");
            }
        }
    }

    /// <summary>
    /// DebugStudio から Unity へデバッグコマンドを送る。
    /// </summary>
    public async Task SendCommandAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await SendMessageAsync(DebugSocketMessageType.DebugCommand, command, command.RequestId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// DebugSocket の任意 protocol message を送信する共通入口。
    /// </summary>
    public async Task SendMessageAsync<TPayload>(
        DebugSocketMessageType messageType,
        TPayload payload,
        string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        // 送信経路の排他・serialize・socket 生存確認は send gateway 側へ集約する。
        // Session 自身は public API の入口だけを持つ。
        await _sendGateway.SendMessageAsync(messageType, payload, requestId, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
        await _lifecycle.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// receive loop の終了処理。
    /// まだ current socket が同じインスタンスなら state を更新し、参照とリソースを片付ける。
    /// すでに再接続済みで socket が差し替わっている場合は何もしない。
    /// </summary>
    private async Task FinalizeReceiveLoopAsync(ClientWebSocket socket, Exception? fatalError, string detail)
    {
        // receive loop 終了と同時に別経路で再接続/切断が走る可能性があるため、
        // lifecycle gate を取り、まだこの socket が current かを確認してから後始末する。
        if (!await _lifecycle.TryAcquireGateAsync(CancellationToken.None).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            if (!_lifecycle.IsSameSocket(socket))
            {
                // すでに別 socket へ差し替わっているなら、古い loop は現役 state を触らない。
                return;
            }

            _lifecycle.ClearSocketIfSame(socket);
            _receiveLoopTask = null;

            // current だった socket だけをここで dispose する。
            socket.Dispose();

            if (_disposed)
            {
                return;
            }

            // 例外の有無で disconnected/faulted を切り替える。
            // detail は receive loop が握っていた「最後に UI へ見せるべき説明文」を使う。
            UpdateState(
                fatalError == null ? DebugSocketConnectionState.Disconnected : DebugSocketConnectionState.Faulted,
                ServerUri,
                detail);
        }
        finally
        {
            _lifecycle.ReleaseGate();
        }
    }

    private void UpdateState(DebugSocketConnectionState state, Uri? serverUri, string detail)
    {
        // state と serverUri を先に内部更新し、その後 snapshot event を発火する。
        // subscriber から見た時点でプロパティ値と event payload が一致していることを保証したい。
        State = state;
        ServerUri = serverUri;

        ConnectionStateChanged?.Invoke(new DebugSocketConnectionSnapshot(
            state,
            serverUri,
            detail,
            DateTimeOffset.Now));
    }
}
