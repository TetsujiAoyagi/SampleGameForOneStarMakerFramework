#nullable enable

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// シーンのライフサイクル状態を定義する。
    /// 状態の並び順は整数比較によるガード条件に使われるため、順序を変更してはならない。
    /// </summary>
    public enum SceneState
    {
        /// <summary>SceneBase インスタンス作成直後。</summary>
        None = 0,

        /// <summary>OnPreLoaded 実行中。</summary>
        PreLoading = 1,

        /// <summary>OnPreLoaded 完了。Unity Scene ロード前の事前準備済み。</summary>
        PreLoaded = 2,

        /// <summary>OnLoaded 実行中（Addressable アセットのロード等）。</summary>
        Loading = 3,

        /// <summary>OnLoaded 完了。</summary>
        Loaded = 4,

        /// <summary>子シーンのロード待ち。</summary>
        WaitLoadChildScene = 5,

        /// <summary>UIView の ViewIn 実行中。</summary>
        Initializing = 6,

        /// <summary>ロードがキャンセルされた。</summary>
        LoadCanceled = 7,

        /// <summary>安定状態。ユーザー操作を受け付けられる唯一の状態。</summary>
        Stable = 8,

        /// <summary>OnPreUnLoad 実行中（ViewOut + リソース解放準備）。</summary>
        PreUnloading = 9,

        /// <summary>OnPreUnLoad 完了。</summary>
        PreUnloaded = 10,

        /// <summary>Unity Scene アンロード中。</summary>
        Unloading = 11,

        /// <summary>Unity Scene アンロード完了。</summary>
        Unloaded = 12,

        /// <summary>OnAfterUnLoad 実行中（最終クリーンアップ）。</summary>
        AfterUnloading = 13,
    }
}
