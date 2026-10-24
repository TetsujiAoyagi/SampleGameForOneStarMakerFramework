#nullable enable

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Client;

internal static class ControlClientClose
{
    internal static async Task TryCloseAsync(WebSocket socket, TimeProvider? timeProvider = null)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3), timeProvider ?? TimeProvider.System);
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", deadline.Token)
                    .WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch { socket.Abort(); }
        // A close failure cannot replace a result that the caller has already received.
    }
}
