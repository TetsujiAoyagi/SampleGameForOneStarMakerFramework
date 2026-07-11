#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime.DebugSocketServices.Protocol;
using UnityEngine;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    public sealed partial class DebugSocketService
    {
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

        private DebugSocketInboundMessageRouter CreateInboundMessageRouter()
        {
            return new DebugSocketInboundMessageRouter(new DebugSocketInboundMessageRouterCallbacks
            {
                IsCurrentSession = IsCurrentInboundSession,
                CreateServiceStatus = CreateServiceStatus,
                CreateCapabilityWelcomeFrame = CreateCapabilityWelcomeFrame,
                GetRuntimeAvailableCapabilities = GetRuntimeAvailableCapabilities,
                CurrentSchemaVersion = CurrentSchemaVersion,
                CreateHierarchySnapshotFrame = CreateHierarchySnapshotFrame,
                HasMainThreadContext = () => _mainThreadContext != null,
                MainThreadContextUnavailableMessage = MainThreadContextUnavailableMessage,
                SwitchToMainThreadAsync = SwitchToMainThreadAsync,
                GetRuntimeDiagnosticsSnapshot = GetRuntimeDiagnosticsSnapshot,
                DispatchCommandAsync = (command, cancellationToken) => _dispatcher.DispatchAsync(command, cancellationToken),
                CreateInspectorDetailFrame = CreateInspectorDetailFrame,
                CreateInspectorMainThreadUnavailableFrame = CreateInspectorMainThreadUnavailableFrame,
                CreateInspectorFaultFrame = CreateInspectorFaultFrame,
            });
        }

        private bool IsCurrentInboundSession(IDebugSocketInboundSession session)
        {
            return session is DebugSocketClientSession clientSession && IsCurrentSession(clientSession);
        }

        /// <summary>
        /// 受信した binary message を inbound router へ委譲する。
        /// </summary>
        private UniTask HandleInboundMessageAsync(
            DebugSocketClientSession session,
            ReadOnlyMemory<byte> framedMessage,
            CancellationToken cancellationToken) =>
            _inboundMessageRouter.RouteAsync(session, framedMessage, cancellationToken);
    }
}