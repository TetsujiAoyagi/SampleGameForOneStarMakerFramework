#nullable enable

using System;
using System.Threading;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// Unity producer 側で Log / Telemetry を横断相関する session ID と producer sequence を管理する。
    ///
    /// <para>
    /// <see cref="SessionId"/> は DebugSocket handshake Welcome に載せる ID と必ず同一にする。
    /// export 時に DebugStudio 側で後付けすると、再接続・遅延受信・過去ファイル export で
    /// 別 session を誤付与し得るため、wire message 作成時点で producer が確定させる。
    /// </para>
    ///
    /// <para>
    /// <see cref="NextProducerSequence"/> は Log と Telemetry が共有する単調増加カウンタ。
    /// stream ごとに counter を分けると同一 frame 内の全体順序を再構成できない。
    /// DebugStudio 受信順 (<c>LogRecord.SequenceNumber</c>) は WebSocket / queue / reconnect の影響を受けるため、
    /// Unity で起きた順序の代替にはならない。
    /// </para>
    /// </summary>
    public static class UnitySessionCorrelationContext
    {
        private static readonly object SessionGate = new();
        private static string _sessionId = string.Empty;
        private static long _producerSequence;

        /// <summary>
        /// Unity 起動（または domain reload 後の再初期化）単位の session ID。
        /// 空の間は lazy init され、DebugSocket 接続の Welcome とも同じ値が使われる。
        /// </summary>
        public static string SessionId
        {
            get
            {
                EnsureSessionInitialized();
                return _sessionId;
            }
        }

        /// <summary>
        /// session 内で 1 から単調増加する producer sequence を採番する。
        /// Log formatter と telemetry record 生成の双方がこの API を通す。
        /// </summary>
        public static long NextProducerSequence()
            => Interlocked.Increment(ref _producerSequence);

        /// <summary>
        /// player restart / domain reload の切替点で session ID と sequence をリセットする。
        /// 旧 session の wire message に新 ID を混ぜないため、Bootstrap の SubsystemRegistration から呼ぶ。
        /// </summary>
        public static void ResetForNewPlayerSession()
        {
            lock (SessionGate)
            {
                _sessionId = Guid.NewGuid().ToString("N");
                _producerSequence = 0;
            }
        }

        private static void EnsureSessionInitialized()
        {
            if (!string.IsNullOrEmpty(_sessionId))
            {
                return;
            }

            lock (SessionGate)
            {
                if (string.IsNullOrEmpty(_sessionId))
                {
                    _sessionId = Guid.NewGuid().ToString("N");
                }
            }
        }

        /// <summary>テスト用。本番 bootstrap とは別経路で state を初期化する。</summary>
        internal static void ResetForTests()
        {
            lock (SessionGate)
            {
                _sessionId = string.Empty;
                _producerSequence = 0;
            }
        }
    }
}
