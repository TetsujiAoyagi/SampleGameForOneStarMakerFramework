#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.UISystem.Behaviors;

namespace OneStarMaker.Tests.UISystem.TestDoubles
{
    /// <summary>
    /// 外部から <see cref="Complete"/> で完了させられるテスト用 Behavior。
    /// 実行回数・スナップ・Rewind・Payload を記録する。
    /// </summary>
    public sealed class ManualBehavior : IUIBehavior, ISnapBehavior, IRewindableBehavior
    {
        private UniTaskCompletionSource? _executeTcs;

        /// <summary>ExecuteAsync の呼び出し回数。</summary>
        public int ExecuteCount { get; private set; }

        /// <summary>SnapToEnd の呼び出し回数。</summary>
        public int SnapCount { get; private set; }

        /// <summary>RewindAsync の呼び出し回数。</summary>
        public int RewindCount { get; private set; }

        /// <summary>直近の Rewind progress 引数。</summary>
        public float LastRewindProgress { get; private set; }

        /// <summary>直近の ExecuteAsync で受け取った Payload。</summary>
        public TransitionPayload? LastPayload { get; private set; }

        /// <summary>直近に VisualState へ反映した NewValue（Snap または正常完了）。</summary>
        public object? LastResolvedNewValue { get; private set; }

        /// <summary>キャンセルにより ExecuteAsync が中断されたか。</summary>
        public bool WasCancelled { get; private set; }

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
            LastPayload = context.Payload;
            OnStarted?.Invoke(context);

            _executeTcs = new UniTaskCompletionSource();
            using var registration = ct.Register(static state =>
            {
                var tcs = (UniTaskCompletionSource)state!;
                tcs.TrySetCanceled();
            }, _executeTcs);

            try
            {
                await _executeTcs.Task;
                ResolveNewValue(context);
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
        }

        /// <inheritdoc/>
        public void SnapToEnd(UIBehaviorContext context)
        {
            SnapCount++;
            ResolveNewValue(context);
        }

        private void ResolveNewValue(UIBehaviorContext context)
        {
            LastResolvedNewValue = context.Payload.NewValue;
            context.VisualState.Set(VisualStateStore.CurrentTransitionKey, context.Payload.NewValue);
        }

        /// <inheritdoc/>
        public UniTask RewindAsync(UIBehaviorContext context, float progress, CancellationToken ct)
        {
            RewindCount++;
            LastRewindProgress = progress;
            return UniTask.CompletedTask;
        }
    }
}
