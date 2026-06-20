#nullable enable

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Client.Internal;

/// <summary>
/// DebugSocket プロトコル送信のゲートウェイ。
/// 送信中に socket が破棄されないよう lifecycle gate を取得し、
/// serialize → send を一貫して実行する。
/// </summary>
internal sealed class DebugSocketSendGateway
{
    private readonly DebugSocketConnectionLifecycle _lifecycle;

    public DebugSocketSendGateway(DebugSocketConnectionLifecycle lifecycle)
    {
        _lifecycle = lifecycle;
    }

    public async Task SendMessageAsync<TPayload>(
        DebugSocketMessageType messageType,
        TPayload payload,
        string? requestId,
        CancellationToken cancellationToken)
    {
        // serialize と SendAsync の間に disconnect が割り込むと disposed socket へ送る危険があるため、
        // 送信全体を lifecycle gate の臨界区間に載せる。
        if (!await _lifecycle.TryAcquireGateAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new ObjectDisposedException(nameof(DebugStudioSession));
        }

        try
        {
            // gate を保持中なので、socket 参照は unsafe accessor から直接読む。
            // 再度 gate を取る accessor を使うと self-deadlock する。
            var socket = _lifecycle.GetCurrentSocketUnsafe();
            if (socket == null || socket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("DebugStudio session is not connected.");
            }

            await DebugSocketSendOperations.SendMessageAsync(
                    socket,
                    messageType,
                    payload,
                    requestId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.ReleaseGate();
        }
    }
}
