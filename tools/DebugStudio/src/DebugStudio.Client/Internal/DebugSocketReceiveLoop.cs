#nullable enable

using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Client.Internal;

/// <summary>
/// WebSocket からの受信ループを実行。
/// 1 WebSocket message = 1 framed binary として受信し、
/// 完全なフレームを inbound router へ引き渡す。
/// </summary>
internal sealed class DebugSocketReceiveLoop
{
    private readonly DebugSocketInboundRouter _router;

    public DebugSocketReceiveLoop(DebugSocketInboundRouter router)
    {
        _router = router;
    }

    public async Task<ReceiveLoopResult> RunAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        // 受信バッファは小さな固定配列を使い回し、1 message が複数 chunk に分かれても
        // memoryStream 側へ継ぎ足して「1 完成フレーム」になった時点で router へ渡す。
        var receiveBuffer = new byte[8192];
        using var memoryStream = new MemoryStream(receiveBuffer.Length);

        Exception? fatalError = null;
        string detail = "Connection closed by server.";

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // WebSocket message ごとに蓄積バッファをリセットする。
                memoryStream.SetLength(0);

                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cancellationToken)
                        .ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // Close frame は protocol error ではなく正常系の終端として扱う。
                        detail = socket.CloseStatusDescription ?? "Connection closed by server.";
                        return new ReceiveLoopResult(null, detail);
                    }

                    if (result.Count > 0)
                    {
                        // chunk ごとに積み上げ、EndOfMessage まで待ってから 1 フレームとして扱う。
                        memoryStream.Write(receiveBuffer, 0, result.Count);
                    }
                }
                while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Binary)
                {
                    // DebugSocket は binary framing 前提なので、text 等は protocol error として捨てる。
                    _router.PublishProtocolError("Only binary WebSocket messages are supported.");
                    continue;
                }

                if (!memoryStream.TryGetBuffer(out var segment))
                {
                    // 通常は到達しないが、バッファ参照が取れない場合は安全側で捨てる。
                    _router.PublishProtocolError("Failed to access the receive buffer.");
                    continue;
                }

                // ここで初めて「完全な 1 framed message」が揃う。
                _router.RouteInboundFrame(new ReadOnlyMemory<byte>(segment.Array!, segment.Offset, (int)memoryStream.Length));
            }

            detail = "Receive loop canceled.";
        }
        catch (OperationCanceledException)
        {
            detail = "Receive loop canceled.";
        }
        catch (WebSocketException ex)
        {
            fatalError = ex;
            detail = ex.Message;
        }
        catch (Exception ex)
        {
            fatalError = ex;
            detail = ex.Message;
        }

        return new ReceiveLoopResult(fatalError, detail);
    }
}

internal readonly record struct ReceiveLoopResult(Exception? FatalError, string Detail);
