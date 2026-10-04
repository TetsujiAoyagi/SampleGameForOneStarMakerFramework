#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime.DebugCommands;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// 受信封筒を <see cref="DebugCommandCatalog"/> へ渡す dispatcher。
    /// アプリはこれを <c>CreateDebugCommandDispatcher</c> から返すと、DebugStudio と同じ目録をプロセス内からも実行できる。
    /// 未登録は失敗結果になり、例外にはしない。コマンド自身が投げた例外は、ソケット側の既存の捕捉に任せる。
    /// </summary>
    public sealed class CatalogDebugCommandDispatcher : IDebugCommandDispatcher
    {
        private readonly DebugCommandCatalog _catalog;
        private static readonly DebugCommandResult Canceled = DebugCommandResult.Fail("Debug command was canceled.");

        public CatalogDebugCommandDispatcher(DebugCommandCatalog catalog)
        {
            if (catalog == null)
            {
                throw new System.ArgumentNullException(nameof(catalog));
            }

            _catalog = catalog;
        }

        public UniTask<DebugCommandResultEnvelopeV1> DispatchAsync(
            DebugCommandEnvelopeV1 command,
            CancellationToken cancellationToken)
        {
            if (command == null)
            {
                return UniTask.FromResult(ToEnvelope(string.Empty, DebugCommandResult.Fail("Debug command is missing.")));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return UniTask.FromResult(ToEnvelope(command.RequestId, Canceled));
            }

            _catalog.TryExecute(command.CommandType, command.PayloadJson, out var result);
            return UniTask.FromResult(ToEnvelope(command.RequestId, result));
        }

        private static DebugCommandResultEnvelopeV1 ToEnvelope(string? requestId, DebugCommandResult result)
        {
            return new DebugCommandResultEnvelopeV1
            {
                RequestId = requestId ?? string.Empty,
                Success = result.Success,
                Message = result.Message ?? string.Empty,
                PayloadJson = result.PayloadJson ?? string.Empty,
            };
        }
    }
}
