#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.DebugSocket;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// デバッグコマンド未実装時の既定 dispatcher。
    ///
    /// <para>
    /// v1 では allowlist を dispatcher 側へ寄せる設計なので、
    /// 何も override していないアプリはこの実装を通って
    /// 「未対応コマンド」であることを明示的に返す。
    /// </para>
    /// </summary>
    public sealed class NullDebugCommandDispatcher : IDebugCommandDispatcher
    {
        public static NullDebugCommandDispatcher Instance { get; } = new();

        private NullDebugCommandDispatcher()
        {
        }

        public UniTask<DebugCommandResultEnvelopeV1> DispatchAsync(
            DebugCommandEnvelopeV1 command,
            CancellationToken cancellationToken)
        {
            return UniTask.FromResult(new DebugCommandResultEnvelopeV1
            {
                RequestId = command.RequestId,
                Success = false,
                Message = $"Debug command '{command.CommandType}' is not registered.",
            });
        }
    }
}
