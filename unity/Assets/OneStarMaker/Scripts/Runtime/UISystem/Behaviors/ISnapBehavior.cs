#nullable enable

namespace OneStarMaker.Runtime.UISystem.Behaviors
{
    /// <summary>
    /// キャンセル・破棄時に最終表示状態へ即時収束させる Behavior の契約。
    /// </summary>
    public interface ISnapBehavior
    {
        /// <summary>
        /// 遷移の終端状態（Stable State から導出される値）へ即時スナップする。
        /// </summary>
        /// <param name="context">実行コンテキスト。</param>
        void SnapToEnd(UIBehaviorContext context);
    }
}
