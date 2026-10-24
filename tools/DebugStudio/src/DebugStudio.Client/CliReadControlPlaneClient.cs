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
                if (DebugSocketProtocol.TryDeserializeEnvelope(frame.ToArray(), out var envelope) && envelope != null &&
                    (DebugSocketMessageType)envelope.MessageType == DebugSocketMessageType.ControlReadResponse &&
                    DebugSocketProtocol.TryDeserializePayload<ControlReadResponseEnvelopeV1>(envelope, out var response) &&
                    response != null && string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal)) return response;
            }
        }
        catch (OperationCanceledException) { return Error(cancellationToken.IsCancellationRequested ? "cancelled" : "timeout"); }
        catch (Exception) { return Error("control-failed"); }
        finally { await ControlClientClose.TryCloseAsync(socket).ConfigureAwait(false); }
    }
}
