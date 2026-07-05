#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.UIElements;

namespace OneStarMaker.Runtime.UISystem.Behaviors
{
    /// <summary>
    /// 1 ターゲット 1 トラックの Behavior 実行・割り込み・収束を担う Runner。
    /// 純 C# オブジェクト（MonoBehaviour 非使用）。
    /// Unity メインスレッド専用（マルチスレッド安全ではない）。
    /// Run / Dispose / IsTransitioning へのアクセスはすべてメインスレッドから行うこと。
    /// </summary>
    public sealed class BehaviorRunner : IDisposable
    {
        private readonly VisualElement _target;
        private readonly InterruptPolicy _interruptPolicy;
        private readonly VisualStateStore _visualState = new();
        private readonly UIBehaviorContext _context;
        private readonly ReactiveProperty<bool> _isTransitioning = new(false);
        private readonly CancellationTokenSource _lifetimeCts = new();
        private readonly object _gate = new();

        private IUIBehavior? _activeBehavior;
        private CancellationTokenSource? _activeCts;
        private UniTaskCompletionSource? _activeRunDone;
        private long _runStartTimestamp;
        private int _runGeneration;

        /// <summary>
        /// 進行中の割り込み処理（キャンセル待ち〜新規 Run 登録まで）の深さ。
        /// 多重割り込みで複数の Run が同時に割り込み区間に入るためカウンタで管理する。
        /// </summary>
        private int _interruptDepth;

        private bool _disposed;

        /// <summary>
        /// 演出対象の VisualElement。
        /// </summary>
        public VisualElement Target => _target;

        /// <summary>
        /// 遷移中かどうか。割り込み連鎖中も true を維持する。
        /// </summary>
        public ReadOnlyReactiveProperty<bool> IsTransitioning => _isTransitioning;

        /// <summary>
        /// Runner の生存期間に紐づくキャンセルトークン。
        /// </summary>
        public CancellationToken LifetimeToken => _lifetimeCts.Token;

        /// <summary>
        /// Runner を生成する。
        /// </summary>
        /// <param name="target">演出対象。</param>
        /// <param name="interruptPolicy">割り込みポリシー。</param>
        /// <param name="services">手動 DI 用サービスリゾルバ。省略可。</param>
        public BehaviorRunner(
            VisualElement target,
            InterruptPolicy interruptPolicy,
            IServiceResolver? services = null)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _interruptPolicy = interruptPolicy;
            _context = new UIBehaviorContext(
                _target,
                new TransitionPayload(null, null),
                _visualState,
                services);
        }

        /// <summary>
        /// Behavior パイプラインを実行する。実行中の再入は <see cref="InterruptPolicy"/> に従う。
        /// </summary>
        /// <param name="behavior">実行する Behavior。</param>
        /// <param name="payload">遷移ペイロード。</param>
        /// <param name="ct">外部キャンセルトークン。</param>
        /// <returns>当該 Run の完了を表す UniTask。</returns>
        public UniTask Run(IUIBehavior behavior, TransitionPayload payload, CancellationToken ct)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BehaviorRunner));
            }

            if (behavior == null)
            {
                throw new ArgumentNullException(nameof(behavior));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            return RunInternalAsync(behavior, payload, ct);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            IUIBehavior? behaviorToSnap = null;
            CancellationTokenSource? ctsToCancel = null;

            lock (_gate)
            {
                behaviorToSnap = _activeBehavior;
                ctsToCancel = _activeCts;
                _activeBehavior = null;
                _activeCts = null;
                _activeRunDone = null;
            }

            // _activeCts が非 null なら当該 Run はまだ finally 未到達（dispose 済み CTS は指さない）。
            // CTS の Dispose は当該 Run の finally が担う（ここでは Cancel のみ）。
            ctsToCancel?.Cancel();

            if (behaviorToSnap != null)
            {
                SnapToEnd(behaviorToSnap);
            }

            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();

            // 遷移中の破棄でも購読者が false を観測できるよう、Dispose 前に必ず false を流す。
            _isTransitioning.Value = false;
            _isTransitioning.Dispose();
        }

        private async UniTask RunInternalAsync(
            IUIBehavior behavior,
            TransitionPayload payload,
            CancellationToken ct)
        {
            var runGeneration = Interlocked.Increment(ref _runGeneration);

            IUIBehavior? previousBehavior = null;
            CancellationTokenSource? previousCts = null;
            UniTaskCompletionSource? previousDone = null;

            lock (_gate)
            {
                if (_activeBehavior != null)
                {
                    previousBehavior = _activeBehavior;
                    previousCts = _activeCts;
                    previousDone = _activeRunDone;
                }
            }

            var interrupting = previousBehavior != null;
            CancellationTokenSource? linkedCts = null;
            UniTaskCompletionSource? runDone = null;
            var registered = false;

            if (interrupting)
            {
                EnsureTransitioningTrue();
                _interruptDepth++;
            }

            try
            {
                if (interrupting)
                {
                    // _activeCts は Run の finally で先行クリアされるため null の場合がある
                    // （null = 前 Run は完了済み。await は即座に抜ける）。
                    previousCts?.Cancel();
                    await previousDone!.Task;

                    if (_disposed || runGeneration != Volatile.Read(ref _runGeneration))
                    {
                        return;
                    }

                    payload = await ApplyInterruptPolicyAsync(previousBehavior!, payload, ct);

                    if (_disposed || runGeneration != Volatile.Read(ref _runGeneration))
                    {
                        return;
                    }
                }

                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token, ct);
                runDone = new UniTaskCompletionSource();

                lock (_gate)
                {
                    if (runGeneration != _runGeneration)
                    {
                        return;
                    }

                    _activeBehavior = behavior;
                    _activeCts = linkedCts;
                    _activeRunDone = runDone;
                    _runStartTimestamp = Stopwatch.GetTimestamp();
                    registered = true;
                }
            }
            finally
            {
                if (interrupting)
                {
                    lock (_gate)
                    {
                        _interruptDepth--;

                        // 割り込み処理が例外・早期 return で中断し、後続の登録も無い場合は
                        // 前 Run の残骸（完了済みエントリ）をクリアして idle へ戻す。
                        if (!registered
                            && _interruptDepth == 0
                            && _activeRunDone == previousDone)
                        {
                            _activeBehavior = null;
                            _activeCts = null;
                            _activeRunDone = null;
                        }
                    }
                }

                if (!registered)
                {
                    linkedCts?.Dispose();

                    if (interrupting)
                    {
                        SetTransitioningFalseIfIdle();
                    }
                }
            }

            EnsureTransitioningTrue();
            _context.SetPayload(payload);

            try
            {
                await behavior.ExecuteAsync(_context, linkedCts!.Token);
            }
            catch (OperationCanceledException) when (linkedCts!.Token.IsCancellationRequested)
            {
                // 割り込みによるキャンセルはポリシー側（Restart スナップ等）が、
                // Runner 破棄によるキャンセルは Dispose がスナップを担うため、
                // ここでスナップするのは外部キャンセルのみ（二重スナップ防止）。
                if (_interruptDepth == 0 && !_disposed)
                {
                    SnapToEnd(behavior);
                }
            }
            finally
            {
                var isCurrentRun = false;
                lock (_gate)
                {
                    if (_activeRunDone == runDone)
                    {
                        isCurrentRun = true;

                        // CTS はこの直後に Dispose するため、dispose 済み CTS への参照が
                        // _activeCts に残らないよう割り込み中でも必ずクリアする。
                        _activeCts = null;

                        // _activeBehavior / _activeRunDone は割り込み側のポリシー適用と
                        // 完了待ちに必要なため、割り込み中は後続 Run の上書きに委ねる。
                        if (_interruptDepth == 0)
                        {
                            _activeBehavior = null;
                            _activeRunDone = null;
                        }
                    }
                }

                linkedCts!.Dispose();
                runDone!.TrySetResult();

                if (isCurrentRun && _interruptDepth == 0)
                {
                    SetTransitioningFalseIfIdle();
                }
            }
        }

        private async UniTask<TransitionPayload> ApplyInterruptPolicyAsync(
            IUIBehavior previousBehavior,
            TransitionPayload payload,
            CancellationToken ct)
        {
            switch (_interruptPolicy)
            {
                case InterruptPolicy.Restart:
                    SnapToEnd(previousBehavior);
                    return payload;

                case InterruptPolicy.FromCurrent:
                {
                    var currentValue = _visualState.GetOr<object?>(
                        VisualStateStore.CurrentTransitionKey,
                        payload.OldValue);
                    return new TransitionPayload(currentValue, payload.NewValue);
                }

                case InterruptPolicy.Rewind:
                    if (previousBehavior is IRewindableBehavior rewindable)
                    {
                        var progress = CalculateProgress();
                        try
                        {
                            await RewindAsync(rewindable, progress, ct);
                        }
                        catch (OperationCanceledException)
                        {
                            // 逆再生の中断でも収束不変条件を守るため最終値へスナップする。
                            SnapToEnd(previousBehavior);
                            throw;
                        }

                        return payload;
                    }

                    SnapToEnd(previousBehavior);
                    return payload;

                default:
                    SnapToEnd(previousBehavior);
                    return payload;
            }
        }

        private async UniTask RewindAsync(IRewindableBehavior rewindable, float progress, CancellationToken ct)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token, ct);
            await rewindable.RewindAsync(_context, progress, linkedCts.Token);
        }

        private float CalculateProgress()
        {
            var delta = Stopwatch.GetTimestamp() - _runStartTimestamp;
            var elapsedSeconds = (double)delta / Stopwatch.Frequency;
            var duration = _visualState.GetOr(VisualStateStore.EstimatedDurationKey, 1f);
            if (duration <= 0f)
            {
                return 1f;
            }

            var progress = (float)(elapsedSeconds / duration);
            if (progress > 1f)
            {
                return 1f;
            }

            if (progress < 0f)
            {
                return 0f;
            }

            return progress;
        }

        private void EnsureTransitioningTrue()
        {
            if (!_isTransitioning.CurrentValue)
            {
                _isTransitioning.Value = true;
            }
        }

        private void SetTransitioningFalseIfIdle()
        {
            if (_disposed)
            {
                return;
            }

            lock (_gate)
            {
                if (_activeBehavior == null && _interruptDepth == 0)
                {
                    _isTransitioning.Value = false;
                }
            }
        }

        private void SnapToEnd(IUIBehavior behavior)
        {
            if (behavior is ISnapBehavior snap)
            {
                snap.SnapToEnd(_context);
            }
        }
    }
}
