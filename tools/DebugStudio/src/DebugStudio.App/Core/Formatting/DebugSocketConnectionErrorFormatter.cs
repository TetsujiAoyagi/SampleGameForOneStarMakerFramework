#nullable enable

using System;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace DebugStudio.App.Core.Formatting;

public static class DebugSocketConnectionErrorFormatter
{
    public static string Format(Uri serverUri, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(serverUri);
        ArgumentNullException.ThrowIfNull(exception);

        var socketException = FindSocketException(exception);
        if (socketException != null)
        {
            return socketException.SocketErrorCode switch
            {
                SocketError.ConnectionRefused =>
                    $"Connection refused by {serverUri}. Unity listener may not be running yet. Check debugSocket:enabled and whether startup reached DebugSocketService.StartAsync().",
                SocketError.TimedOut =>
                    $"Timed out connecting to {serverUri}. Check host/port reachability and whether Unity has finished startup.",
                SocketError.HostNotFound or SocketError.NoData =>
                    $"Host resolution failed for {serverUri}. Check the host name and network route.",
                _ =>
                    $"Failed to connect to {serverUri}. {socketException.Message}",
            };
        }

        if (exception is WebSocketException webSocketException &&
            webSocketException.WebSocketErrorCode == WebSocketError.NotAWebSocket)
        {
            return $"WebSocket handshake failed for {serverUri}. Check the path and ensure Unity is listening on this URI.";
        }

        return $"Failed to connect to {serverUri}. {exception.GetBaseException().Message}";
    }

    private static SocketException? FindSocketException(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is SocketException socketException)
            {
                return socketException;
            }
        }

        return null;
    }
}
