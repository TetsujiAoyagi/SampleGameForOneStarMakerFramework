#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Stores;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// transport session と app store 群の間を仲介する薄い orchestration service。
///
/// <para>
/// MainWindowViewModel が socket event を直接握ると、
/// 「接続制御」「log 保持」「UI 更新」が一箇所へ混ざりやすい。
/// この service は session から受信した raw DTO を app 層の store / event へ配線し、
/// WPF shell には接続操作と購読ポイントだけを見せる。
/// </para>
/// <para>
/// rf2: orchestration 責務を 3 つの coordinator へ分離。
/// SessionService は安定した facade として残し、内部的に委譲する形へリファクタ。
/// - <see cref="SessionResetPolicy"/>: connect/disconnect 時の store クリア戦略
/// - <see cref="SessionMessageRouter"/>: inbound message の store routing
/// - <see cref="SessionCapabilityCoordinator"/>: capability hello 送信とエラーハンドリング
/// </para>
/// </summary>
public sealed class SessionService : IAsyncDisposable
{
    private readonly ISessionTransport _session;
    private readonly SessionResetPolicy _resetPolicy;
    private readonly SessionMessageRouter _messageRouter;
    private readonly SessionCapabilityCoordinator _capabilityCoordinator;

    public SessionService(
        ISessionTransport session,
        SessionResetPolicy resetPolicy,
        SessionMessageRouter messageRouter,
        SessionCapabilityCoordinator capabilityCoordinator)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _resetPolicy = resetPolicy ?? throw new ArgumentNullException(nameof(resetPolicy));
        _messageRouter = messageRouter ?? throw new ArgumentNullException(nameof(messageRouter));
        _capabilityCoordinator = capabilityCoordinator ?? throw new ArgumentNullException(nameof(capabilityCoordinator));

        _session.ConnectionStateChanged += OnConnectionStateChanged;
        _session.LogReceived += OnLogReceived;
        _session.TelemetryReceived += OnTelemetryReceived;
        _session.ServiceStatusReceived += OnServiceStatusReceived;
        _session.CommandResultReceived += OnCommandResultReceived;
        _session.CapabilityWelcomeReceived += OnCapabilityWelcomeReceived;
        _session.HierarchySnapshotReceived += OnHierarchySnapshotReceived;
        _session.HierarchyDeltaReceived += OnHierarchyDeltaReceived;
        _session.InspectorDetailReceived += OnInspectorDetailReceived;

        _messageRouter.LogReceived += ForwardLogReceived;
        _messageRouter.TelemetryReceived += ForwardTelemetryReceived;
        _messageRouter.ServiceStatusReceived += ForwardServiceStatusReceived;
        _messageRouter.CommandResultReceived += ForwardCommandResultReceived;
        _messageRouter.CapabilityWelcomeReceived += ForwardCapabilityWelcomeReceived;
        _messageRouter.HierarchySnapshotReceived += ForwardHierarchySnapshotReceived;
        _messageRouter.HierarchyDeltaReceived += ForwardHierarchyDeltaReceived;
        _messageRouter.InspectorDetailReceived += ForwardInspectorDetailReceived;
    }

    public DebugSocketConnectionState State => _session.State;

    public event Action<DebugSocketConnectionSnapshot>? ConnectionStateChanged;

    public event Action<LogRecord>? LogReceived;

    public event Action<DebugTelemetryEnvelopeV1>? TelemetryReceived;

    public event Action<DebugSocketServiceStatusEnvelopeV1>? ServiceStatusReceived;

    public event Action<DebugCommandResultEnvelopeV1>? CommandResultReceived;

    public event Action<CapabilityHandshakeWelcomeEnvelopeV1>? CapabilityWelcomeReceived;

    public event Action<HierarchySnapshotEnvelopeV1>? HierarchySnapshotReceived;

    public event Action<HierarchyDeltaEnvelopeV1>? HierarchyDeltaReceived;

    public event Action<InspectorDetailEnvelopeV1>? InspectorDetailReceived;

    /// <summary>
    /// 接続前に必要な state を初期化し、transport 接続後に capability hello まで送る。
    ///
    /// <para>
    /// 呼び出し側から見ると「ConnectAsync したら session が交渉開始状態に入る」ことが重要であり、
    /// reset と hello 送信の順序はこの facade が責任を持つ。
    /// </para>
    /// </summary>
    public async Task ConnectAsync(DebugSocketClientOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        // 古い hierarchy/inspector/telemetry/command 状態を残したまま再接続すると、
        // UI が別セッションの残骸を表示してしまうため、connect 前に必ず reset する。
        _resetPolicy.ResetForConnect(options.ServerUri);

        // socket 接続が成功して初めて hello を送れる。
        // 先に hello を送ろうとすると未接続 send になるので、この順序は固定する。
        await _session.ConnectAsync(options, cancellationToken).ConfigureAwait(false);
        await _capabilityCoordinator.SendCapabilityHelloAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return _session.DisconnectAsync(cancellationToken);
    }

    public Task SendCommandAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default)
    {
        return _session.SendCommandAsync(command, cancellationToken);
    }

    public Task SendProtocolMessageAsync<TPayload>(
        DebugSocketMessageType messageType,
        TPayload payload,
        string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        return _session.SendMessageAsync(messageType, payload, requestId, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        // SessionService は facade なので、自前で握る transport event を必ず解除してから
        // session dispose へ進む。解除順は機能順ではなく「session にぶら下がる購読を全て切る」ことが目的。
        _session.ConnectionStateChanged -= OnConnectionStateChanged;
        _session.LogReceived -= OnLogReceived;
        _session.TelemetryReceived -= OnTelemetryReceived;
        _session.ServiceStatusReceived -= OnServiceStatusReceived;
        _session.CommandResultReceived -= OnCommandResultReceived;
        _session.CapabilityWelcomeReceived -= OnCapabilityWelcomeReceived;
        _session.HierarchySnapshotReceived -= OnHierarchySnapshotReceived;
        _session.HierarchyDeltaReceived -= OnHierarchyDeltaReceived;
        _session.InspectorDetailReceived -= OnInspectorDetailReceived;
        _messageRouter.LogReceived -= ForwardLogReceived;
        _messageRouter.TelemetryReceived -= ForwardTelemetryReceived;
        _messageRouter.ServiceStatusReceived -= ForwardServiceStatusReceived;
        _messageRouter.CommandResultReceived -= ForwardCommandResultReceived;
        _messageRouter.CapabilityWelcomeReceived -= ForwardCapabilityWelcomeReceived;
        _messageRouter.HierarchySnapshotReceived -= ForwardHierarchySnapshotReceived;
        _messageRouter.HierarchyDeltaReceived -= ForwardHierarchyDeltaReceived;
        _messageRouter.InspectorDetailReceived -= ForwardInspectorDetailReceived;
        await _session.DisposeAsync().ConfigureAwait(false);
    }

    private void OnConnectionStateChanged(DebugSocketConnectionSnapshot snapshot)
    {
        // 接続断や fault は capability negotiation の文脈でも「現在の交渉結果は無効」とみなす。
        // そのため disconnected/faulted の時だけ reset policy 側へ detail を流す。
        if (snapshot.State is DebugSocketConnectionState.Disconnected or DebugSocketConnectionState.Faulted)
        {
            _resetPolicy.MarkDisconnected(snapshot.Detail);
        }

        // facade として外側へは transport snapshot をそのまま再通知する。
        ConnectionStateChanged?.Invoke(snapshot);
    }

    private void OnLogReceived(LogEnvelopeV1 envelope)
    {
        // 以降の handler は「transport event を受けたら routing collaborator へ委譲するだけ」に揃える。
        // SessionService 自体に store mutation を戻さないのが rf2 の狙い。
        _messageRouter.RouteLogMessage(envelope);
    }

    private void OnTelemetryReceived(DebugTelemetryEnvelopeV1 telemetry)
    {
        _messageRouter.RouteTelemetryMessage(telemetry);
    }

    private void OnServiceStatusReceived(DebugSocketServiceStatusEnvelopeV1 status)
    {
        _messageRouter.RouteServiceStatusMessage(status);
    }

    private void OnCommandResultReceived(DebugCommandResultEnvelopeV1 result)
    {
        _messageRouter.RouteCommandResultMessage(result);
    }

    private void OnCapabilityWelcomeReceived(CapabilityHandshakeWelcomeEnvelopeV1 welcome)
    {
        _messageRouter.RouteCapabilityWelcomeMessage(welcome);
    }

    private void OnHierarchySnapshotReceived(HierarchySnapshotEnvelopeV1 snapshot)
    {
        _messageRouter.RouteHierarchySnapshotMessage(snapshot);
    }

    private void OnHierarchyDeltaReceived(HierarchyDeltaEnvelopeV1 delta)
    {
        _messageRouter.RouteHierarchyDeltaMessage(delta);
    }

    private void OnInspectorDetailReceived(InspectorDetailEnvelopeV1 detail)
    {
        _messageRouter.RouteInspectorDetailMessage(detail);
    }

    private void ForwardLogReceived(LogRecord log) => LogReceived?.Invoke(log);

    private void ForwardTelemetryReceived(DebugTelemetryEnvelopeV1 telemetry) => TelemetryReceived?.Invoke(telemetry);

    private void ForwardServiceStatusReceived(DebugSocketServiceStatusEnvelopeV1 status) => ServiceStatusReceived?.Invoke(status);

    private void ForwardCommandResultReceived(DebugCommandResultEnvelopeV1 result) => CommandResultReceived?.Invoke(result);

    private void ForwardCapabilityWelcomeReceived(CapabilityHandshakeWelcomeEnvelopeV1 welcome) => CapabilityWelcomeReceived?.Invoke(welcome);

    private void ForwardHierarchySnapshotReceived(HierarchySnapshotEnvelopeV1 snapshot) => HierarchySnapshotReceived?.Invoke(snapshot);

    private void ForwardHierarchyDeltaReceived(HierarchyDeltaEnvelopeV1 delta) => HierarchyDeltaReceived?.Invoke(delta);

    private void ForwardInspectorDetailReceived(InspectorDetailEnvelopeV1 detail) => InspectorDetailReceived?.Invoke(detail);
}
