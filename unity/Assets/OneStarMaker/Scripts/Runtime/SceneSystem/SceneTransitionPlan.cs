#nullable enable

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// 宣言的なシーン遷移プラン。
    /// SceneBase のライフサイクルフック内から SceneDirector を直接呼ぶ代わりに、
    /// このプランを返すことで再入問題を回避する。
    /// SceneDirector がプランを解釈し、安全な順序で実行する。
    /// </summary>
    public class SceneTransitionPlan
    {
        /// <summary>遷移中のローディング表示モード。</summary>
        public LoadingDisplayType LoadingDisplay { get; set; } = LoadingDisplayType.None;

        /// <summary>次に遷移するシーンの ID。null なら遷移しない（Unload のみ）。</summary>
        public string? NextSceneId { get; set; }

        /// <summary>遷移先シーンに渡す型付きコンテキスト。</summary>
        public SceneContext? Context { get; set; }
    }
}
