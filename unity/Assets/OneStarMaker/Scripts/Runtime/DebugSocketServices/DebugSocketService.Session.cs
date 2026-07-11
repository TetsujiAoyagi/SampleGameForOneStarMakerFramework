#nullable enable

using System;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    public sealed partial class DebugSocketService
    {
        /// <summary>
        /// 現在のセッションへメッセージを積む。
        /// セッションがなければ drop する。v1 では未接続時のバッファ保持はしない。
        /// </summary>
        private void EnqueueOutgoingMessage(byte[] framedMessage)
        {
            EnqueueOutgoingMessage(DebugSocketOutgoingFrame.CreateOwned(framedMessage));
        }

        /// <summary>
        /// 現在のセッションへ framed message を積む。
        ///
        /// <para>
        /// pooled buffer の場合、未接続や drop 時でも release を忘れないことが重要。
        /// 送信経路のどこで ownership が終わるかをこのメソッドから先で一貫させる。
        /// </para>
        /// </summary>
        private void EnqueueOutgoingMessage(in DebugSocketOutgoingFrame framedMessage)
        {
            DebugSocketClientSession? session;
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

        private void EnqueueRuntimeDiagnosticsIfNeeded(DebugSocketClientSession session)
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

        private bool IsCurrentSession(DebugSocketClientSession session)
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
        private void OnSessionClosed(DebugSocketClientSession session)
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

        /// <summary>
        /// 新しい WebSocket を current session として登録し、旧 session を置き換える。
        ///
        /// <para>
        /// 新 session を current にして Start してから旧 session を Close する。
        /// viewer 側の連続再接続でも送信空白時間を最小化するため、この順序を維持する。
        /// </para>
        /// </summary>
        private async UniTask<DebugSocketClientSession> ActivateSessionAsync(
            WebSocket socket,
            CancellationToken cancellationToken,
            string connectedMessage)
        {
            var session = new DebugSocketClientSession(this, socket, Options.MaxQueueLength, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                await session.CloseAsync("activation-cancelled", CancellationToken.None);
                cancellationToken.ThrowIfCancellationRequested();
            }

            DebugSocketClientSession? previousSession;
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

            session.Start();
            session.Enqueue(CreateServiceStatus("connected", connectedMessage));
            EnqueueRuntimeDiagnosticsIfNeeded(session);

            if (previousSession != null)
            {
                await previousSession.CloseAsync("replaced-by-new-client", CancellationToken.None);
            }

            return session;
        }

        void IDebugSocketClientSessionHost.RecordQueueOverflowDrops(int droppedCount) =>
            RecordQueueOverflowDrops(droppedCount);

        void IDebugSocketClientSessionHost.OnSessionClosed(DebugSocketClientSession session) =>
            OnSessionClosed(session);

        byte[] IDebugSocketClientSessionHost.CreateServiceStatus(string status, string message) =>
            CreateServiceStatus(status, message);

        UniTask IDebugSocketClientSessionHost.HandleInboundMessageAsync(
            DebugSocketClientSession session,
            ReadOnlyMemory<byte> framedMessage,
            CancellationToken cancellationToken) =>
            HandleInboundMessageAsync(session, framedMessage, cancellationToken);

        UniTask<DebugSocketClientSession> IDebugSocketTransportHostCallbacks.ActivateSessionAsync(
            WebSocket socket,
            CancellationToken cancellationToken,
            string connectedMessage) =>
            ActivateSessionAsync(socket, cancellationToken, connectedMessage);

        void IDebugSocketTransportHostCallbacks.SetLastStartError(string? message) =>
            _lastStartError = message;
    }
}
