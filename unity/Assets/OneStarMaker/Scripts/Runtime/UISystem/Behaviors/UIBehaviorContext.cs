#nullable enable

using UnityEngine.UIElements;

namespace OneStarMaker.Runtime.UISystem.Behaviors
{
    /// <summary>
    /// 手動 DI 用のサービス解決契約。実装は呼び出し側が注入する。
    /// </summary>
    public interface IServiceResolver
    {
        /// <summary>
        /// 指定型のサービスを解決する。
        /// </summary>
        /// <typeparam name="T">サービス型。</typeparam>
        /// <returns>解決できたインスタンス。未登録なら null。</returns>
        T? Resolve<T>()
            where T : class;
    }

    /// <summary>
    /// Behavior 実行時に渡される共有コンテキスト。Runner により使い回される。
    /// </summary>
    public sealed class UIBehaviorContext
    {
        /// <summary>
        /// 演出対象の VisualElement。
        /// </summary>
        public VisualElement Target { get; }

        /// <summary>
        /// 今回の遷移ペイロード。
        /// </summary>
        public TransitionPayload Payload { get; private set; }

        /// <summary>
        /// 遷移中の表示状態ストア。
        /// </summary>
        public VisualStateStore VisualState { get; }

        /// <summary>
        /// 手動 DI 注入ポイント。未注入なら null。
        /// </summary>
        public IServiceResolver? Services { get; }

        /// <summary>
        /// コンテキストを生成する。
        /// </summary>
        /// <param name="target">演出対象。</param>
        /// <param name="payload">初期ペイロード。</param>
        /// <param name="visualState">Visual State ストア。</param>
        /// <param name="services">サービスリゾルバ。</param>
        public UIBehaviorContext(
            VisualElement target,
            TransitionPayload payload,
            VisualStateStore visualState,
            IServiceResolver? services)
        {
            Target = target;
            Payload = payload;
            VisualState = visualState;
            Services = services;
        }

        /// <summary>
        /// Runner が次の Run 開始時にペイロードを差し替える。
        /// </summary>
        /// <param name="payload">新しいペイロード。</param>
        internal void SetPayload(TransitionPayload payload)
        {
            Payload = payload;
        }
    }
}
