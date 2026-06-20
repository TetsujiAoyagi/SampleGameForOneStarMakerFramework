#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.DebugSocket;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// 外部ツールから届いたデバッグコマンドをアプリ側へ橋渡しする抽象。
    ///
    /// <para>
    /// DebugSocketService は「受信」「protocol 復号」「main thread へ戻す」までは面倒を見るが、
    /// 実際に何のコマンドをどう処理するかはアプリ固有のため、この interface で分離する。
    /// </para>
    /// </summary>
    public interface IDebugCommandDispatcher
    {
        /// <summary>
        /// 受信したコマンドを処理し、呼び出し元へ返す結果 DTO を返す。
        /// </summary>
        UniTask<DebugCommandResultEnvelopeV1> DispatchAsync(
            DebugCommandEnvelopeV1 command,
            CancellationToken cancellationToken);
    }
}
