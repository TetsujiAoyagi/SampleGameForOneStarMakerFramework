#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// <see cref="DebugSocketClientSession"/> から service facade へ逆依存せず橋渡しする最小 contract。
    /// </summary>
    internal interface IDebugSocketClientSessionHost
    {
        void RecordQueueOverflowDrops(int droppedCount);

        void OnSessionClosed(DebugSocketClientSession session);

        byte[] CreateServiceStatus(string status, string message);

        UniTask HandleInboundMessageAsync(
            DebugSocketClientSession session,
            ReadOnlyMemory<byte> framedMessage,
            CancellationToken cancellationToken);
    }
}
