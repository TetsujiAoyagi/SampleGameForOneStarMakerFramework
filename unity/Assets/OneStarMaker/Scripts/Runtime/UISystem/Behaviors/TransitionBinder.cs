#nullable enable

using System;
using Cysharp.Threading.Tasks;
using R3;

namespace OneStarMaker.Runtime.UISystem.Behaviors
{
    /// <summary>
    /// Stable State の変化を <see cref="BehaviorRunner"/> へ接続する R3 拡張。
    /// </summary>
    public static class TransitionBinder
    {
        /// <summary>
        /// <paramref name="source"/> の値変化を Pairwise で検出し、<paramref name="behavior"/> を Runner 経由で起動する。
        /// 購読開始時の現在値では発火しない（Pairwise は 2 値目から通知する。
        /// <see cref="ReadOnlyReactiveProperty{T}"/> の Subscribe は最新値を即配信するが、Pairwise が初回をバッファするため初回 Run は抑制される）。
        /// </summary>
        /// <typeparam name="T">Stable State の型。</typeparam>
        /// <param name="source">変化を監視する ReadOnlyReactiveProperty。</param>
        /// <param name="runner">遷移実行先 Runner。</param>
        /// <param name="behavior">値変化時に実行する Behavior。</param>
        /// <returns>購読の Disposable。Dispose 後は Run が起動しない。</returns>
        /// <remarks>
        /// <see cref="TransitionPayload"/> は object? を取るため、T が値型の場合は変化時に boxing が発生する。
        /// 発火は変化時のみなので許容する（T-07 注意点）。
        /// </remarks>
        public static IDisposable BindTransition<T>(
            this ReadOnlyReactiveProperty<T> source,
            BehaviorRunner runner,
            IUIBehavior behavior)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            if (behavior == null)
            {
                throw new ArgumentNullException(nameof(behavior));
            }

            return source
                .Pairwise()
                .Subscribe(pair => runner
                    .Run(
                        behavior,
                        new TransitionPayload(pair.Item1, pair.Item2),
                        runner.LifetimeToken)
                    .Forget());
        }
    }
}
