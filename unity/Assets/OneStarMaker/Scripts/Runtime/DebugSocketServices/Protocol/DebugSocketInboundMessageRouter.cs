#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime.DebugSocketServices.Commands;

namespace OneStarMaker.Runtime.DebugSocketServices.Protocol
{
    /// <summary>
    /// inbound セッションが router へ渡す最小の送受信面。
    /// <see cref="DebugSocketClientSession"/> 全体ではなく、protocol routing に必要な状態だけを公開する。
    /// </summary>
    internal interface IDebugSocketInboundSession
    {
        string SessionId { get; }

        /// <summary>capability hello 完了後にのみ DebugCommand / InspectorQuery を受理する。</summary>
        bool HasCompletedCapabilityHello { get; set; }

        DebugStudioCapability NegotiatedCapabilities { get; set; }

        void Enqueue(byte[] framedMessage);

        UniTask CloseFromReceiveLoopAsync(string reason);
    }

    /// <summary>
    /// Unity API や共有状態へ触れる処理を router から切り離す callback 群。
    /// </summary>
    internal sealed class DebugSocketInboundMessageRouterCallbacks
    {
        public Func<IDebugSocketInboundSession, bool> IsCurrentSession { get; init; } = _ => false;
        public Func<string, string, byte[]> CreateServiceStatus { get; init; } = (_, _) => Array.Empty<byte>();
        public Func<string, CapabilityHandshakeHelloEnvelopeV1, byte[]> CreateCapabilityWelcomeFrame { get; init; } =
            (_, _) => Array.Empty<byte>();
        public Func<DebugStudioCapability> GetRuntimeAvailableCapabilities { get; init; } = () => DebugStudioCapability.None;
        public int CurrentSchemaVersion { get; init; }
        public Func<byte[]> CreateHierarchySnapshotFrame { get; init; } = () => Array.Empty<byte>();
        public Func<bool> HasMainThreadContext { get; init; } = () => false;
        public string MainThreadContextUnavailableMessage { get; init; } = string.Empty;
        public Func<CancellationToken, UniTask> SwitchToMainThreadAsync { get; init; } =
            _ => UniTask.CompletedTask;
        public Func<DebugSocketService.RuntimeDiagnosticsSnapshot> GetRuntimeDiagnosticsSnapshot { get; init; } =
            () => default;
        public Func<DebugCommandEnvelopeV1, CancellationToken, UniTask<DebugCommandResultEnvelopeV1>> DispatchCommandAsync { get; init; } =
            (_, _) => UniTask.FromResult(new DebugCommandResultEnvelopeV1());
        public Func<InspectorQueryEnvelopeV1, string?, byte[]> CreateInspectorDetailFrame { get; init; } =
            (_, _) => Array.Empty<byte>();
        public Func<InspectorQueryEnvelopeV1, string?, byte[]> CreateInspectorMainThreadUnavailableFrame { get; init; } =
            (_, _) => Array.Empty<byte>();
        public Func<InspectorQueryEnvelopeV1, string?, string, byte[]> CreateInspectorFaultFrame { get; init; } =
            (_, _, _) => Array.Empty<byte>();
    }

    /// <summary>
    /// 受信 framed message の envelope decode と message type 別 routing を担当する。
    ///
    /// <para>
    /// <see cref="DebugSocketService"/> や transport session 全体へ依存せず、
    /// <see cref="IDebugSocketInboundSession"/> と callback だけで protocol 応答を組み立てる。
    /// これにより inbound 分岐を service のライフサイクル/依存組み立てから切り離す。
    /// </para>
    ///
    /// <para>
    /// stale session 防御:
    /// envelope decode 直後と、main-thread 切替後の Unity API 呼び出し前に
    /// <c>IsCurrentSession</c> を確認する。
    /// 新接続へ current が切り替わった後に旧 session から遅延到着した inbound が
    /// hierarchy token cache や inspector 正本を汚染しないようにするため。
    /// </para>
    ///
    /// <para>
    /// capability hello 完了前の DebugCommand / InspectorQuery は protocol-error または fault で拒否する。
    /// </para>
    /// </summary>
    internal sealed class DebugSocketInboundMessageRouter
    {
        private readonly DebugSocketInboundMessageRouterCallbacks _callbacks;

        public DebugSocketInboundMessageRouter(DebugSocketInboundMessageRouterCallbacks callbacks)
        {
            _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        }

        /// <summary>
        /// 受信した binary message を protocol として解釈し、必要なら応答 frame を session へ積む。
        /// </summary>
        public async UniTask RouteAsync(
            IDebugSocketInboundSession session,
            ReadOnlyMemory<byte> framedMessage,
            CancellationToken cancellationToken)
        {
            if (!DebugSocketProtocol.TryDeserializeEnvelope(framedMessage, out var envelope) || envelope == null)
            {
                session.Enqueue(_callbacks.CreateServiceStatus("protocol-error", "Received invalid framed message."));
                return;
            }

            // decode 後かつ Unity API へ触る前に current を確認する。
            // 旧 session の遅延 inbound が新 session 用の共有状態を汚染するのを防ぐ。
            if (!_callbacks.IsCurrentSession(session))
            {
                return;
            }

            switch ((DebugSocketMessageType)envelope.MessageType)
            {
                case DebugSocketMessageType.CapabilityHello:
                    await HandleCapabilityHelloAsync(session, envelope, cancellationToken);
                    return;

                case DebugSocketMessageType.DebugCommand:
                    await HandleDebugCommandAsync(session, envelope, cancellationToken);
                    return;

                case DebugSocketMessageType.InspectorQuery:
                    await HandleInspectorQueryAsync(session, envelope, cancellationToken);
                    return;

                default:
                    session.Enqueue(_callbacks.CreateServiceStatus(
                        "protocol-error",
                        $"Unsupported inbound message type: {(DebugSocketMessageType)envelope.MessageType}."));
                    return;
            }
        }

        private async UniTask HandleCapabilityHelloAsync(
            IDebugSocketInboundSession session,
            DebugSocketEnvelopeV1 envelope,
            CancellationToken cancellationToken)
        {
            if (!DebugSocketProtocol.TryDeserializePayload<CapabilityHandshakeHelloEnvelopeV1>(envelope, out var hello) ||
                hello == null)
            {
                session.Enqueue(_callbacks.CreateServiceStatus("protocol-error", "Failed to decode capability hello payload."));
                return;
            }

            var schemaCompatible =
                hello.MinSchemaVersion <= _callbacks.CurrentSchemaVersion &&
                _callbacks.CurrentSchemaVersion <= hello.MaxSchemaVersion;
            var runtimeAvailableCapabilities = _callbacks.GetRuntimeAvailableCapabilities();
            session.HasCompletedCapabilityHello = true;
            session.NegotiatedCapabilities = schemaCompatible
                ? runtimeAvailableCapabilities & hello.SupportedCapabilities
                : DebugStudioCapability.None;
            session.Enqueue(_callbacks.CreateCapabilityWelcomeFrame(session.SessionId, hello));
            if (!schemaCompatible)
            {
                await session.CloseFromReceiveLoopAsync("schema-mismatch");
                return;
            }

            if ((session.NegotiatedCapabilities & DebugStudioCapability.HierarchySnapshot) != 0)
            {
                if (!_callbacks.HasMainThreadContext())
                {
                    session.Enqueue(_callbacks.CreateServiceStatus(
                        "main-thread-unavailable",
                        _callbacks.MainThreadContextUnavailableMessage));
                    return;
                }

                await _callbacks.SwitchToMainThreadAsync(cancellationToken);
                if (!_callbacks.IsCurrentSession(session))
                {
                    return;
                }

                session.Enqueue(_callbacks.CreateHierarchySnapshotFrame());
            }
        }

        private async UniTask HandleDebugCommandAsync(
            IDebugSocketInboundSession session,
            DebugSocketEnvelopeV1 envelope,
            CancellationToken cancellationToken)
        {
            if (!DebugSocketProtocol.TryDeserializePayload<DebugCommandEnvelopeV1>(envelope, out var command) || command == null)
            {
                session.Enqueue(_callbacks.CreateServiceStatus("protocol-error", "Failed to decode debug command payload."));
                return;
            }

            if (DebugSocketBuiltInCommandHandler.TryHandle(
                    command,
                    _callbacks.GetRuntimeDiagnosticsSnapshot(),
                    out var builtInResult))
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
                session.Enqueue(_callbacks.CreateServiceStatus("protocol-error", "Debug command capability is not negotiated."));
                return;
            }

            try
            {
                var result = await _callbacks.DispatchCommandAsync(command, cancellationToken);
                result.RequestId = command.RequestId;

                session.Enqueue(DebugSocketProtocol.SerializeMessage(
                    DebugSocketMessageType.CommandResult,
                    result,
                    command.RequestId));
            }
            catch (Exception ex)
            {
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
        }

        private async UniTask HandleInspectorQueryAsync(
            IDebugSocketInboundSession session,
            DebugSocketEnvelopeV1 envelope,
            CancellationToken cancellationToken)
        {
            if (!DebugSocketProtocol.TryDeserializePayload<InspectorQueryEnvelopeV1>(envelope, out var inspectorQuery) ||
                inspectorQuery == null)
            {
                session.Enqueue(_callbacks.CreateServiceStatus("protocol-error", "Failed to decode inspector query payload."));
                return;
            }

            if (!session.HasCompletedCapabilityHello ||
                (session.NegotiatedCapabilities & DebugStudioCapability.InspectorQuery) == 0 ||
                (session.NegotiatedCapabilities & DebugStudioCapability.InspectorDetail) == 0)
            {
                session.Enqueue(_callbacks.CreateInspectorFaultFrame(
                    inspectorQuery,
                    envelope.RequestId,
                    "Inspector query/detail capability is not negotiated."));
                return;
            }

            if (!_callbacks.HasMainThreadContext())
            {
                session.Enqueue(_callbacks.CreateInspectorMainThreadUnavailableFrame(inspectorQuery, envelope.RequestId));
                return;
            }

            await _callbacks.SwitchToMainThreadAsync(cancellationToken);
            if (!_callbacks.IsCurrentSession(session))
            {
                return;
            }

            try
            {
                session.Enqueue(_callbacks.CreateInspectorDetailFrame(inspectorQuery, envelope.RequestId));
            }
            catch (Exception ex)
            {
                session.Enqueue(_callbacks.CreateInspectorFaultFrame(inspectorQuery, envelope.RequestId, ex.Message));
            }
        }
    }
}
