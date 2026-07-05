#nullable enable

namespace OneStarMaker.Runtime.UISystem.Behaviors
{
    /// <summary>
    /// 実行中に <see cref="BehaviorRunner.Run"/> が再入したときの割り込み方針。
    /// </summary>
    public enum InterruptPolicy
    {
        /// <summary>
        /// 実行中をキャンセルし、最終値へスナップしてから新規実行する。
        /// </summary>
        Restart,

        /// <summary>
        /// 実行中をキャンセルする（スナップしない）。
        /// Visual State の現在値を OldValue として差し替えて新規実行する。
        /// </summary>
        FromCurrent,

        /// <summary>
        /// 実行中をキャンセルし、<see cref="IRewindableBehavior"/> なら逆再生する。
        /// 非対応 Behavior は <see cref="Restart"/> にフォールバックする。
        /// </summary>
        Rewind,
    }
}
