#nullable enable

using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// <see cref="DebugSocketTransportHost"/> から service facade へ session 置換と診断更新を橋渡しする最小 contract。
    /// </summary>
    internal interface IDebugSocketTransportHostCallbacks
    {
        UniTask<DebugSocketClientSession> ActivateSessionAsync(
            WebSocket socket,
            CancellationToken cancellationToken,
            string connectedMessage);

        void SetLastStartError(string? message);
    }
}
