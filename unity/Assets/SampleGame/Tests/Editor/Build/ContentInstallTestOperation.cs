#nullable enable

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace OneStarMaker.Tests.Editor.Build
{
    // fixture は source と server の終了まで含む一回の install を所有する。
    // cancel 要求だけでは ContentCacheStore の transaction lease 解放を証明できない。
    internal sealed class ContentInstallTestOperation
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Func<CancellationToken, Task> _callback;
        private Task? _completion;
        private bool _failureObserved;
        private bool _disposed;

        internal ContentInstallTestOperation(Func<CancellationToken, Task> callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        internal void Start()
        {
            if (_completion != null) throw new InvalidOperationException("Install operation already started.");
            // fixture がこの owner を保持した後で callback を呼ぶ。同期例外も task に捕捉する。
            try { _completion = _callback(_cancellation.Token) ?? Task.FromException(
                new InvalidOperationException("Install callback returned no task.")); }
            catch (Exception failure) { _completion = Task.FromException(failure); }
        }

        internal bool IsCompleted => _completion?.IsCompleted ?? false;

        internal void ObserveTerminal()
        {
            if (_completion == null || !_completion.IsCompleted)
                throw new InvalidOperationException("Install operation is still running.");
            // Unity iterator が再開してから同期的に終端を渡す。ここへ到達する前に timeout なら
            // 元の fault は未観測のままなので TearDown が報告する。
            _failureObserved = true;
            _completion.GetAwaiter().GetResult();
        }

        internal async Task<DrainResult> CancelAndDrainAsync(CancellationToken deadline)
        {
            var completion = _completion ?? throw new InvalidOperationException("Install operation not started.");
            Exception? cancellationFailure = null;
            try { _cancellation.Cancel(); }
            catch (Exception exception) { cancellationFailure = exception; }
            var deadlineSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (deadline.Register(() => deadlineSignal.TrySetResult(true)))
            {
                if (await Task.WhenAny(completion, deadlineSignal.Task) != completion)
                    return new DrainResult(false, cancellationFailure);
            }

            Exception? failure = null;
            try { await completion; }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
            catch (Exception exception) when (!_failureObserved) { failure = exception; }
            catch (Exception) { /* 同じ fault は test 本体の iterator が受け取っている。 */ }
            _cancellation.Dispose();
            _disposed = true;
            if (cancellationFailure != null && failure != null)
                failure = new AggregateException(cancellationFailure, failure);
            else failure ??= cancellationFailure;
            return new DrainResult(true, failure);
        }

        internal bool CancellationDisposed => _disposed;

        internal static async Task RunWithShutdownAsync(Func<Task> operation, Func<Task> shutdown)
        {
            Exception? primary = null;
            try { await operation(); }
            catch (Exception failure) { primary = failure; }
            Exception? shutdownFailure = null;
            try { await shutdown(); }
            catch (Exception failure) { shutdownFailure = failure; }
            // finally の例外置換で元の install/source fault を失わない。
            if (primary != null && shutdownFailure != null)
                throw new AggregateException(primary, shutdownFailure);
            if (primary != null) ExceptionDispatchInfo.Capture(primary).Throw();
            if (shutdownFailure != null) ExceptionDispatchInfo.Capture(shutdownFailure).Throw();
        }
    }

    internal readonly struct DrainResult
    {
        internal DrainResult(bool completed, Exception? failure)
        {
            Completed = completed;
            Failure = failure;
        }
        internal bool Completed { get; }
        internal Exception? Failure { get; }
    }
}
