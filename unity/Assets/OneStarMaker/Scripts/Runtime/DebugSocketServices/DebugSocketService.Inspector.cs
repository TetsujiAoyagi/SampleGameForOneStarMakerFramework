#nullable enable

using System;
using System.Threading;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime.DebugSocketServices.Inspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    public sealed partial class DebugSocketService
    {
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
            var sections = DebugSocketInspectorBuilder.BuildInspectorSections(query.TargetId, gameObject, scene, query.QueryFlags);
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

        /// <summary>
        /// 直近の hierarchy capture で <paramref name="nodeId"/> を受けた GameObject を返す。main thread で呼ぶ。
        ///
        /// <para>
        /// inspect と同じ lookup で、DebugStudio の利用者が hierarchy で見た NodeId をゲーム側のデバッグコマンドが
        /// GameObject に解決するための読み取り口。未 capture（hierarchy を購読するセッションが無い等）、
        /// capture で見えなくなって prune 済み、WPF の接続が替わって registry が reset された後、破棄済み、
        /// hierarchy に出ない GameObject（DontDestroyOnLoad 等）は false。
        /// </para>
        /// <para>
        /// <see cref="Dispose"/> の後は false を返す。Dispose は <c>_disposed</c> を立ててから片付けを非同期で始めるので、
        /// registry の対応表がしばらく残る。その間に古い対応で GameObject を返さないよう、lookup より先に <c>_disposed</c> を見る。
        /// </para>
        /// </summary>
        public bool TryGetHierarchyGameObject(long nodeId, out GameObject? gameObject)
        {
            gameObject = null;
            if (_disposed)
            {
                return false;
            }

            return TryFindGameObjectByNodeId(nodeId, out _, out gameObject);
        }

        private bool TryFindGameObjectByNodeId(long targetId, out Scene scene, out GameObject? gameObject)
        {
            lock (_gate)
            {
                return _runtimeNodeRegistry.TryFindGameObjectByNodeId(targetId, out scene, out gameObject);
            }
        }
    }
}
