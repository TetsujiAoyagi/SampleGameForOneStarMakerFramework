#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.DebugSocket;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// コマンド実行直前に Unity メインスレッドへ戻す decorator。
    ///
    /// <para>
    /// WebSocket の receive loop はバックグラウンドで動くため、
    /// そこから直接 Unity API を叩くと安全ではない。
    /// そのため dispatcher へ渡す直前に必ず main thread へ切り替える。
    /// </para>
    /// </summary>
    internal sealed class MainThreadDebugCommandDispatcher : IDebugCommandDispatcher
    {
        private readonly IDebugCommandDispatcher _inner;
        private readonly SynchronizationContext? _mainThreadContext;

        public MainThreadDebugCommandDispatcher(
            IDebugCommandDispatcher inner,
            SynchronizationContext? mainThreadContext)
        {
            _inner = inner;
            _mainThreadContext = mainThreadContext;
        }

        public async UniTask<DebugCommandResultEnvelopeV1> DispatchAsync(
            DebugCommandEnvelopeV1 command,
            CancellationToken cancellationToken)
        {
            // Unity 起動直後の文脈が壊れていると SynchronizationContext を捕まえられない。
            // その場合にバックグラウンドで危険な実行をするより、失敗を明示した方が安全。
            if (_mainThreadContext == null)
            {
                return new DebugCommandResultEnvelopeV1
                {
                    RequestId = command.RequestId,
                    Success = false,
                    Message = "Main thread synchronization context is not available.",
                };
            }

            // 実コマンドが Unity API に触れてもよいよう、ここで main thread context へ戻す。
            await new SwitchToSynchronizationContextAwaitable(_mainThreadContext, cancellationToken);
            return await _inner.DispatchAsync(command, cancellationToken);
        }
    }
}
