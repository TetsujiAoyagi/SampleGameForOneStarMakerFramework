#nullable enable

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// プレイヤー操作と UI 操作のアクションマップ。
    /// 物理キーではなく、既存 InputActionAsset のマップ名に対応する枠である。
    /// 整数は保存しない。並びを変えるとマップ切替の判定が変わるため、値を足す場合はスライスを分ける。
    /// </summary>
    public enum InputControlMap
    {
        /// <summary>ゲームプレイ用マップ。既存アセットの "Player"。</summary>
        Player = 0,

        /// <summary>UI 用マップ。既存アセットの "UI"。フォーカスやタブ順はここではない。</summary>
        UI = 1,
    }
}
