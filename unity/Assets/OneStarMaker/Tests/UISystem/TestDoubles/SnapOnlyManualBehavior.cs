#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.UISystem.Behaviors;

namespace OneStarMaker.Tests.UISystem.TestDoubles
{
    /// <summary>
    /// <see cref="IRewindableBehavior"/> を実装しない ManualBehavior 相当。
    /// Rewind ポリシーの Snap フォールバック検証用。
    /// </summary>
    public sealed class SnapOnlyManualBehavior : IUIBehavior, ISnapBehavior
    {
        private UniTaskCompletionSource? _executeTcs;

        /// <summary>ExecuteAsync の呼び出し回数。</summary>
        public int ExecuteCount { get; private set; }

        /// <summary>SnapToEnd の呼び出し回数。</summary>
        public int SnapCount { get; private set; }

        /// <summary>ExecuteAsync 開始直後（await 前）に呼ばれるフック。</summary>
        public Action<UIBehaviorContext>? OnStarted { get; set; }

        /// <summary>待機中の ExecuteAsync を完了させる。</summary>
        public void Complete()
        {
            _executeTcs?.TrySetResult();
        }

        /// <inheritdoc/>
        public async UniTask ExecuteAsync(UIBehaviorContext context, CancellationToken ct)
        {
            ExecuteCount++;
            OnStarted?.Invoke(context);

            _executeTcs = new UniTaskCompletionSource();
            using var registration = ct.Register(static state =>
            {
                var tcs = (UniTaskCompletionSource)state!;
                tcs.TrySetCanceled();
            }, _executeTcs);

            await _executeTcs.Task;
        }

        /// <inheritdoc/>
        public void SnapToEnd(UIBehaviorContext context)
        {
            SnapCount++;
            context.VisualState.Set(VisualStateStore.CurrentTransitionKey, context.Payload.NewValue);
        }
    }
}
