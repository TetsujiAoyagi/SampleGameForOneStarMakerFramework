#nullable enable

using UnityEngine;

namespace SampleGame.InGame.Player
{
    /// <summary>
    /// InGameUI 等が Session 経由で参照する飛行プレイヤーの読み取り専用面。
    /// 物理・入力の所有は PlayerScene / FlyController に残し、ここは表示・注視点用の射影だけを晒す。
    /// </summary>
    public interface IFlightReadModel
    {
        /// <summary>ワールド座標（HUD / Focus 供給用）。</summary>
        Vector3 Position { get; }

        /// <summary>入力が有効か（遷移中ロック等の表示に使える）。</summary>
        bool InputEnabled { get; }
    }
}
