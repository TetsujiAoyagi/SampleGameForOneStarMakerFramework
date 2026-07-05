#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace OneStarMaker.Runtime.UISystem.Behaviors
{
    /// <summary>
    /// 子 Behavior を <see cref="UniTask.WhenAll"/> で並列実行する合成 Behavior。
    /// 1 つの失敗/キャンセルが全体へ伝播する。
    /// </summary>
    public sealed class ParallelBehavior : IUIBehavior, ISnapBehavior, IRewindableBehavior
    {
        private readonly IUIBehavior[] _steps;

        /// <summary>
        /// 並列実行する子 Behavior 列を指定する。
        /// </summary>
        /// <param name="steps">子 Behavior。</param>
        public ParallelBehavior(params IUIBehavior[] steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            _steps = steps;
        }

        /// <inheritdoc/>
        public async UniTask ExecuteAsync(UIBehaviorContext context, CancellationToken ct)
        {
            var tasks = new UniTask[_steps.Length];
            for (int i = 0; i < _steps.Length; i++)
            {
                tasks[i] = _steps[i].ExecuteAsync(context, ct);
            }

            await UniTask.WhenAll(tasks);
        }

        /// <inheritdoc/>
        public void SnapToEnd(UIBehaviorContext context)
        {
            for (int i = 0; i < _steps.Length; i++)
            {
                if (_steps[i] is ISnapBehavior snap)
                {
                    snap.SnapToEnd(context);
                }
            }
        }

        /// <inheritdoc/>
        public async UniTask RewindAsync(UIBehaviorContext context, float progress, CancellationToken ct)
        {
            var tasks = new UniTask[_steps.Length];
            for (int i = 0; i < _steps.Length; i++)
            {
                if (_steps[i] is IRewindableBehavior rewindable)
                {
                    tasks[i] = rewindable.RewindAsync(context, progress, ct);
                }
                else
                {
                    tasks[i] = UniTask.CompletedTask;
                }
            }

            await UniTask.WhenAll(tasks);
        }
    }
}
