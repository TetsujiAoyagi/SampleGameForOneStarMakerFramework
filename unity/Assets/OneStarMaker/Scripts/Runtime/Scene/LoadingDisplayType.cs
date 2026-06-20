#nullable enable

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// シーン遷移時のローディング表示モード。
    /// </summary>
    public enum LoadingDisplayType
    {
        /// <summary>何もしない。サイレントロード。</summary>
        None,

        /// <summary>黒画面オーバーレイ。フェードイン → ロード → フェードアウト。</summary>
        BlackScreen,

        /// <summary>右下アイコン表示。ノンブロッキング。</summary>
        Indicator,
    }
}
