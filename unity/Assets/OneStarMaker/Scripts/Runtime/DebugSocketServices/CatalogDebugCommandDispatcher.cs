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
    /// <remarks>App が初期登録済みの catalog を内部呼出しと明示的に共有する opt-in adapter。
    /// default dispatcher や組込コマンドの配線を置き換えるものではない。catalog と依存の寿命は App 所有者が管理する。
    /// 直接呼出しの handler 例外は伝播し、失敗 envelope への変換は実際に通った socket router の責務。
    /// socket 経路の main thread dispatcher を維持する。内部呼出し側も Unity API の thread 義務を負う。</remarks>
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

            // この検査時点の事前キャンセルだけを扱う。検査後の競合や開始済み同期 handler の中断は保証しない。
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
