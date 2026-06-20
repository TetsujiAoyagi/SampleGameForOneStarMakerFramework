#nullable enable

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Client.Internal;

/// <summary>
/// DebugSocket protocol message の serialize + WebSocket send を共通化する helper。
/// client/server どちらの transport でも framing と送信条件を揃えたいので、ここへ集約する。
/// </summary>
internal static class DebugSocketSendOperations
{
    public static async Task SendMessageAsync<TPayload>(
        WebSocket socket,
        DebugSocketMessageType messageType,
        TPayload payload,
        string? requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);

        if (socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("DebugStudio session is not connected.");
        }

        // framing は send 直前に行い、送られない message の無駄な serialize を増やさない。
        var framedMessage = DebugSocketProtocol.SerializeMessage(
            messageType,
            payload,
            requestId);

        await socket.SendAsync(new ArraySegment<byte>(framedMessage), WebSocketMessageType.Binary, true, cancellationToken)
            .ConfigureAwait(false);
    }
}
