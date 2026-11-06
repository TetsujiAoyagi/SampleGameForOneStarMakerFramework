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
        // read/command の応答後も peer が close handshake を返さない場合に CLI 終了を止めない。
        // deadline 内に閉じられない接続は Abort し、呼出元の結果は変更しない。
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3), timeProvider ?? TimeProvider.System);
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", deadline.Token)
                    .WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch { socket.Abort(); }
        // close 失敗は後始末の診断に留め、呼出元が受け取った結果や終了コードを置き換えない。
    }
}
