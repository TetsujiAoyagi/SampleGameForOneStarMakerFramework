#nullable enable

using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Client;

public sealed class CliReadControlPlaneClient
{
    public async Task<ControlReadResponseEnvelopeV1> ReadAsync(Uri controlUri,
        ControlReadRequestEnvelopeV1 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(controlUri);
        ArgumentNullException.ThrowIfNull(request);
        ControlReadResponseEnvelopeV1 Error(string reason) => new() { RequestId = request.RequestId, Reason = reason };
        if (request.TimeoutMilliseconds is <= 0 or > 60000) return Error("invalid-request");
        using var socket = new ClientWebSocket();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // connect / send / receive を一つの期限で束ねる。各段で期限を更新すると
        // 応答しない local peer が段数分だけ CLI を占有できてしまう。
        deadline.CancelAfter(TimeSpan.FromMilliseconds(request.TimeoutMilliseconds));
        try
        {
            await socket.ConnectAsync(controlUri, deadline.Token).ConfigureAwait(false);
            var bytes = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.ControlReadRequest, request, request.RequestId);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, deadline.Token).ConfigureAwait(false);
            var buffer = new byte[8192];
            while (true)
            {
                using var frame = new MemoryStream();
                WebSocketReceiveResult received;
                do
                {
                    received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), deadline.Token).ConfigureAwait(false);
                    if (received.MessageType == WebSocketMessageType.Close) return Error("control-closed");
                    frame.Write(buffer, 0, received.Count);
                } while (!received.EndOfMessage);
                if (!DebugSocketProtocol.TryDeserializeEnvelope(frame.ToArray(), out var envelope) || envelope == null)
                    continue;
                // 旧 host は read 要求を command の失敗で返す。期限切れと誤認させない。
                if ((DebugSocketMessageType)envelope.MessageType == DebugSocketMessageType.ControlCommandResponse &&
                    string.Equals(envelope.RequestId, request.RequestId, StringComparison.Ordinal)) return Error("control-unsupported");
                if ((DebugSocketMessageType)envelope.MessageType == DebugSocketMessageType.ControlReadResponse &&
                    DebugSocketProtocol.TryDeserializePayload<ControlReadResponseEnvelopeV1>(envelope, out var response) &&
                    response != null && string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal)) return response;
                // listener の応答でない frame は読み捨てる。別 request の応答をこの CLI 呼出しへ流用しない。
            }
        }
        catch (OperationCanceledException) { return Error(cancellationToken.IsCancellationRequested ? "cancelled" : "timeout"); }
        catch (Exception) { return Error("control-failed"); }
        // close は結果を返すための処理ではなく後始末。peer が close を返さなくても、
        // すでに得た timeout / response を上書きしない。
        finally { await ControlClientClose.TryCloseAsync(socket).ConfigureAwait(false); }
    }
}
