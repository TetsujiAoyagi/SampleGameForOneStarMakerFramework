#nullable enable

using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Client;

public sealed class DebugCommandControlPlaneClient
{
    public async Task<DebugCommandRoundtripResult> SendAsync(
        DebugCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ServerUri is null)
        {
            throw new ArgumentException("A control URI is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.CommandType))
        {
            throw new ArgumentException("A command type is required.", nameof(request));
        }

        if (request.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Timeout must be greater than zero.");
        }

        var command = new DebugCommandEnvelopeV1
        {
            RequestId = string.IsNullOrWhiteSpace(request.RequestId)
                ? DebugCommandRequestIdFactory.Create(request.CommandType)
                : request.RequestId,
            CommandType = request.CommandType.Trim(),
            PayloadJson = request.PayloadJson ?? "{}",
        };

        var controlRequest = new ControlCommandRequestEnvelopeV1
        {
            RequestId = command.RequestId,
            CommandType = command.CommandType,
            PayloadJson = command.PayloadJson,
            TimeoutMilliseconds = (int)Math.Clamp(request.Timeout.TotalMilliseconds, 1, int.MaxValue),
        };

        using var socket = new ClientWebSocket();
        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationCts.CancelAfter(request.Timeout);

        try
        {
            await socket.ConnectAsync(request.ServerUri, operationCts.Token).ConfigureAwait(false);

            var requestFrame = DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.ControlCommandRequest,
                controlRequest,
                command.RequestId);
            await socket.SendAsync(
                    new ArraySegment<byte>(requestFrame),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cancellationToken: operationCts.Token)
                .ConfigureAwait(false);

            while (true)
            {
                var envelope = await ReceiveEnvelopeAsync(socket, operationCts.Token).ConfigureAwait(false);
                if (envelope == null)
                {
                    return DebugCommandRoundtripResult.Failed(
                        command,
                        $"The control endpoint closed before a response was received for request '{command.RequestId}'.");
                }

                if ((DebugSocketMessageType)envelope.MessageType != DebugSocketMessageType.ControlCommandResponse ||
                    !DebugSocketProtocol.TryDeserializePayload<ControlCommandResponseEnvelopeV1>(envelope, out var response) ||
                    response == null)
                {
                    continue;
                }

                if (!string.Equals(response.RequestId, command.RequestId, StringComparison.Ordinal))
                {
                    continue;
                }

                return response.Status switch
                {
                    ControlCommandRoundtripStatus.Completed when response.CommandResult is not null
                        => DebugCommandRoundtripResult.Completed(command, response.CommandResult),
                    ControlCommandRoundtripStatus.TimedOut
                        => DebugCommandRoundtripResult.TimedOut(command, response.Detail),
                    _ => DebugCommandRoundtripResult.Failed(
                        command,
                        string.IsNullOrWhiteSpace(response.Detail)
                            ? "The control endpoint returned a failure response."
                            : response.Detail),
                };
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && operationCts.IsCancellationRequested)
        {
            return DebugCommandRoundtripResult.TimedOut(
                command,
                $"Timed out after {request.Timeout.TotalSeconds:0.###} second(s) waiting for control response for request '{command.RequestId}'.");
        }
        catch (Exception ex)
        {
            return DebugCommandRoundtripResult.Failed(command, ex.Message);
        }
        finally
        {
            await TryCloseAsync(socket).ConfigureAwait(false);
        }
    }

    private static async Task<DebugSocketEnvelopeV1?> ReceiveEnvelopeAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        await using var memoryStream = new MemoryStream();

        while (true)
        {
            var receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (receiveResult.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            await memoryStream.WriteAsync(buffer.AsMemory(0, receiveResult.Count), cancellationToken).ConfigureAwait(false);
            if (!receiveResult.EndOfMessage)
            {
                continue;
            }

            return DebugSocketProtocol.TryDeserializeEnvelope(memoryStream.ToArray(), out var envelope)
                ? envelope
                : null;
        }
    }

    private static async Task TryCloseAsync(ClientWebSocket socket)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch
        {
        }
    }
}
