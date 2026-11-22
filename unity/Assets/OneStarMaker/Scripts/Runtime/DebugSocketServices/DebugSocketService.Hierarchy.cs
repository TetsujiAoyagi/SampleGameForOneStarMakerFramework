#nullable enable

using OneStarMaker.Foundation.DebugSocket;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    public sealed partial class DebugSocketService
    {
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
        ///
        /// <para>
        /// snapshot / delta の選択規約:
        /// </para>
        /// <list type="bullet">
        /// <item><description>HierarchyDelta capability があり delta が作れた場合は delta を優先送信する。</description></item>
        /// <item><description>HierarchyDelta capability があり published 正本があり、差分が無い場合は何も送らない。</description></item>
        /// <item><description>上記以外（初回、reset 直後、delta capability なし）は snapshot を送る。</description></item>
        /// </list>
        /// </summary>
        private void PublishHierarchyUpdateIfPossible()
        {
            DebugSocketClientSession? session;
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
                var captureResult = _hierarchyPublisher.CaptureUnsafe();
                if ((session.NegotiatedCapabilities & DebugStudioCapability.HierarchyDelta) != 0 &&
                    _hierarchyPublisher.TryCreateDeltaFrameUnsafe(captureResult, out framedMessage))
                {
                    // delta が作れた場合はそれを優先送信する。
                }
                else if ((session.NegotiatedCapabilities & DebugStudioCapability.HierarchyDelta) != 0 &&
                    _hierarchyPublisher.HasPublishedStateUnsafe())
                {
                    // 既存正本があり、差分も発生しなかった場合は何も送らない。
                    return;
                }
                else
                {
                    framedMessage = _hierarchyPublisher.CreateSnapshotFrameUnsafe(captureResult);
                }
            }

            session.Enqueue(framedMessage!);
        }

        /// <summary>
        /// 呼び手が持つ GameObject が直近の hierarchy capture で受けた NodeId を返す。main thread で呼ぶ。
        ///
        /// <para>
        /// 読み取り専用の逆引きで、NodeId から GameObject を返す口は公開しない。
        /// 未 capture（hierarchy を購読するセッションが無い等）、capture で見えなくなって prune 済み、
        /// WPF の接続が替わって registry が reset された後、破棄済みの GameObject は false。
        /// capture・prune・reset と同じ <c>_gate</c> の中で読むので、途中の token 空間を返さない。
        /// </para>
        /// </summary>
        public bool TryGetHierarchyNodeId(GameObject gameObject, out long nodeId)
        {
            lock (_gate)
            {
                return _runtimeNodeRegistry.TryGetNodeIdUnsafe(gameObject, out nodeId);
            }
        }

        private byte[] CreateHierarchySnapshotFrame()
        {
            lock (_gate)
            {
                return _hierarchyPublisher.CreateSnapshotFrameUnsafe(_hierarchyPublisher.CaptureUnsafe());
            }
        }

        private void ResetPublishedHierarchyUnsafe()
        {
            _hierarchyPublisher.ResetUnsafe();
        }
    }
}
