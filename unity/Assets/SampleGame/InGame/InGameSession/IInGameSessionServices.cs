#nullable enable

using SampleGame.InGame.LevelStreaming;
using SampleGame.InGame.Player;
using UnityEngine;

namespace SampleGame.InGame
{
    /// <summary>
    /// InGameSession が子シーン（PlayerScene / InGameUI）へ公開する親サービス面。
    /// OutGame の <c>IOutGameBackgroundRequests</c> と同型で、兄弟同士の直参照を避け、
    /// 依存方向を「子 → 親」に固定する（シーンツリー = DI コンテナ方針）。
    /// </summary>
    /// <remarks>
    /// 将来 Cell Streaming を載せるときも、注視点（Focus）の供給口はこの面に集約する想定。
    /// Level 単位トンネルデモ自体は捨てる前提だが、UI / Player のハブ契約は残す。
    /// </remarks>
    public interface IInGameSessionServices
    {
        /// <summary>
        /// 現行（暫定）の Level ストリーミング調停者。未初期化時は null。
        /// Cell Streaming 置換後は別 API に移行する。
        /// </summary>
        LevelStreamCoordinator<InGameSession>? Coordinator { get; }

        /// <summary>
        /// ストリーミング演出のフィードバック口。
        /// Coordinator は UI 具象を知らず、この抽象へ Show/Hide するだけにする。
        /// </summary>
        ILevelStreamTransitionFeedback TransitionFeedback { get; }

        /// <summary>
        /// PlayerScene が登録した飛行状態の読み取り口。未登録時は null。
        /// 位置の物理所有は Player 側に残し、Session は解決ハブに徹する。
        /// </summary>
        IFlightReadModel? Flight { get; }

        /// <summary>
        /// 将来の Cell Streaming / カメラ注視点用。現状は Flight.Position を返す。
        /// </summary>
        Vector3? FocusWorldPosition { get; }

        /// <summary>PlayerScene が Stable 後に飛行モデルを登録する。</summary>
        void RegisterFlight(IFlightReadModel flight);

        /// <summary>PlayerScene アンロード時に登録を外す（二重登録・ダングリング防止）。</summary>
        void UnregisterFlight(IFlightReadModel flight);
    }
}
