#nullable enable

using System.Collections.Generic;
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
    /// Cell Streaming では注視点（Focus）の供給口をこの面に集約する。
    /// 旧 LevelStreamCoordinator / トンネル演出口は廃止済み。
    /// </remarks>
    public interface IInGameSessionServices
    {
        /// <summary>
        /// PlayerScene が登録した飛行状態の読み取り口。未登録時は null。
        /// 位置の物理所有は Player 側に残し、Session は解決ハブに徹する。
        /// </summary>
        IFlightReadModel? Flight { get; }

        /// <summary>
        /// Cell Streaming / カメラ注視点用。現状は Flight.Position を返す。
        /// WorldStreamingController.Tick の入力になる。
        /// </summary>
        Vector3? FocusWorldPosition { get; }

        /// <summary>
        /// Focus が載っているセル identity（例: Cell_0_0）。
        /// Streaming 未起動・グリッド外・Focus 無しは null。
        /// </summary>
        string? CurrentCellIdentity { get; }

        /// <summary>
        /// Stable 到達済みセル identity のスナップショット（HUD 観測用）。
        /// </summary>
        IReadOnlyList<string> ResidentCellIdentities { get; }

        /// <summary>
        /// 常駐 Cell 配下で Stable 到達済みの職種子（Environment_* 等）のスナップショット。
        /// Cell だけ載って子が未 Add のあいだは空（引っ張られないことの観測口）。
        /// </summary>
        IReadOnlyList<string> LoadedChildSceneIdentities { get; }

        /// <summary>
        /// WorldStreamingController の Tick ループが Start 済みか。
        /// Driver 生成直後（未 Start）は false。セル Add が走り得る状態の判定に使う。
        /// </summary>
        bool IsStreamingActive { get; }

        /// <summary>PlayerScene が Stable 後に飛行モデルを登録する。</summary>
        void RegisterFlight(IFlightReadModel flight);

        /// <summary>PlayerScene アンロード時に登録を外す（二重登録・ダングリング防止）。</summary>
        void UnregisterFlight(IFlightReadModel flight);
    }
}
