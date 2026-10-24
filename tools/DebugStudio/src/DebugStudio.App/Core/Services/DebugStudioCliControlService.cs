#nullable enable

using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Models;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Server;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// CLI 専用のローカル control plane。
/// Unity 本線の inbound session とは別 listener で受け、受信コマンドを既存 SessionService/CommandService へ中継する。
/// </summary>
public sealed class DebugStudioCliControlService : IAsyncDisposable
{
    private readonly SessionService _sessionService;
    private readonly CommandService _commandService;
    private readonly DebugStudioCliControlOptions _options;
    private readonly CliReadService? _readService;
    private readonly Func<TimeSpan, CancellationToken, CancellationTokenSource> _createDeadline;
    private readonly CancellationTokenSource _shutdownCts = new();

    private DebugStudioWebSocketServer? _server;
    private Task? _acceptLoopTask;
    private bool _started;
    private bool _disposed;

    public DebugStudioCliControlService(
        SessionService sessionService,
        CommandService commandService,
        DebugStudioCliControlOptions? options = null,
        CliReadService? readService = null)
        : this(sessionService, commandService, options, readService, CreateDeadline)
    {
    }

    internal DebugStudioCliControlService(
        SessionService sessionService,
        CommandService commandService,
        DebugStudioCliControlOptions? options,
        CliReadService? readService,
        Func<TimeSpan, CancellationToken, CancellationTokenSource> createDeadline)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
        _options = options ?? new DebugStudioCliControlOptions();
        _readService = readService;
        _createDeadline = createDeadline ?? throw new ArgumentNullException(nameof(createDeadline));
    }

    public Uri ControlUri => _options.ControlUri;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_started)
        {
            return Task.CompletedTask;
        }

        var server = new DebugStudioWebSocketServer(CreateServerOptions(_options));
        server.StartListening();

        _server = server;
        _acceptLoopTask = RunAcceptLoopAsync(server, _shutdownCts.Token);
        _started = true;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdownCts.Cancel();

        if (_server != null)
        {
            _server.Stop();
            _server.Dispose();
            _server = null;
        }

        if (_acceptLoopTask != null)
        {
            try
            {
                await _acceptLoopTask.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        _shutdownCts.Dispose();
    }

    private async Task RunAcceptLoopAsync(DebugStudioWebSocketServer server, CancellationToken cancellationToken)
    {
        // CLI は短命な接続を前提にしているため、control plane は 1 接続ずつ順番に処理する。
        // これにより Unity 本線の単一 session と独立させつつ、control listener 自身のライフサイクルを単純化する。
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerWebSocketContext? context = null;
            try
            {
                context = await server.AcceptWebSocketAsync(cancellationToken, continueOnIdleTimeout: true).ConfigureAwait(false);
                await HandleControlConnectionAsync(context.WebSocket, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (TimeoutException)
            {
            }
            catch
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    internal async Task HandleControlConnectionAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var envelope = await ReceiveEnvelopeAsync(socket, cancellationToken).ConfigureAwait(false);
                if (envelope == null)
                {
                    return;
                }

                if ((DebugSocketMessageType)envelope.MessageType == DebugSocketMessageType.ControlReadRequest &&
                    DebugSocketProtocol.TryDeserializePayload<ControlReadRequestEnvelopeV1>(envelope, out var readRequest) &&
                    readRequest != null)
                {
                    var readResponse = _readService != null
                        ? await _readService.ReadAsync(readRequest, cancellationToken).ConfigureAwait(false)
                        : new ControlReadResponseEnvelopeV1
                        {
                            RequestId = readRequest.RequestId,
                            Status = "error",
                            Reason = "read-unavailable",
                        };
                    await SendResponseAsync(socket, DebugSocketMessageType.ControlReadResponse,
                        readResponse, readResponse.RequestId, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if ((DebugSocketMessageType)envelope.MessageType != DebugSocketMessageType.ControlCommandRequest ||
                    !DebugSocketProtocol.TryDeserializePayload<ControlCommandRequestEnvelopeV1>(envelope, out var request) ||
                    request == null)
                {
                    var invalidResponse = new ControlCommandResponseEnvelopeV1
                    {
                        RequestId = envelope.RequestId ?? string.Empty,
                        Status = ControlCommandRoundtripStatus.Failed,
                        Detail = "The control plane received an unsupported message.",
                    };

                    await SendControlResponseAsync(socket, invalidResponse, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var response = await DispatchCommandAsync(request, cancellationToken).ConfigureAwait(false);
                await SendControlResponseAsync(socket, response, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await TryCloseSocketAsync(socket, "control-session-complete").ConfigureAwait(false);
        }
    }

    private async Task<ControlCommandResponseEnvelopeV1> DispatchCommandAsync(
        ControlCommandRequestEnvelopeV1 request,
        CancellationToken cancellationToken)
    {
        var commandType = request.CommandType?.Trim() ?? string.Empty;
        var requestId = string.IsNullOrWhiteSpace(request.RequestId)
            ? DebugStudio.Client.DebugCommandRequestIdFactory.Create(commandType)
            : request.RequestId;

        if (string.IsNullOrWhiteSpace(commandType))
        {
            return new ControlCommandResponseEnvelopeV1
            {
                RequestId = requestId,
                Status = ControlCommandRoundtripStatus.Failed,
                Detail = "The control request must specify a command type.",
            };
        }

        var timeout = request.TimeoutMilliseconds > 0
            ? TimeSpan.FromMilliseconds(request.TimeoutMilliseconds)
            : TimeSpan.FromSeconds(15);

        var completionSource = new TaskCompletionSource<DebugCommandResultEnvelopeV1>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnCommandResultReceived(DebugCommandResultEnvelopeV1 result)
        {
            if (string.Equals(result.RequestId, requestId, StringComparison.Ordinal))
            {
                completionSource.TrySetResult(result);
            }
        }

        void OnConnectionStateChanged(DebugSocketConnectionSnapshot snapshot)
        {
            if (snapshot.State is DebugSocketConnectionState.Disconnected or DebugSocketConnectionState.Faulted)
            {
                var detail = string.IsNullOrWhiteSpace(snapshot.Detail)
                    ? "The Unity session closed before a matching CommandResult was received."
                    : snapshot.Detail;
                completionSource.TrySetException(new InvalidOperationException(detail));
            }
        }

        _sessionService.CommandResultReceived += OnCommandResultReceived;
        _sessionService.ConnectionStateChanged += OnConnectionStateChanged;

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(timeout);

            // control request と Unity 側 result は同じ requestId で束ねる。
            // これにより既存 CommandStore/UI の相関ルールを崩さず CLI だけを外側へ足せる。
            var command = new DebugCommandEnvelopeV1
            {
                RequestId = requestId,
                CommandType = commandType,
                PayloadJson = request.PayloadJson ?? "{}",
            };

            await _commandService.SendAsync(command, linkedCts.Token).ConfigureAwait(false);
            var result = await completionSource.Task.WaitAsync(linkedCts.Token).ConfigureAwait(false);

            return new ControlCommandResponseEnvelopeV1
            {
                RequestId = requestId,
                Status = ControlCommandRoundtripStatus.Completed,
                Detail = result.Message,
                CommandResult = result,
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _commandService.SweepTimedOutCommands(timeout);
            return new ControlCommandResponseEnvelopeV1
            {
                RequestId = requestId,
                Status = ControlCommandRoundtripStatus.TimedOut,
                Detail = $"Timed out after {timeout.TotalSeconds:0.###} second(s) waiting for CommandResult for request '{requestId}'.",
            };
        }
        catch (Exception ex)
        {
            return new ControlCommandResponseEnvelopeV1
            {
                RequestId = requestId,
                Status = ControlCommandRoundtripStatus.Failed,
                Detail = ex.Message,
            };
        }
        finally
        {
            _sessionService.CommandResultReceived -= OnCommandResultReceived;
            _sessionService.ConnectionStateChanged -= OnConnectionStateChanged;
        }
    }

    private async Task<DebugSocketEnvelopeV1?> ReceiveEnvelopeAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        // One deadline covers the entire frame; fragments must not renew an idle peer's lease.
        using var deadline = _createDeadline(TimeSpan.FromSeconds(15), cancellationToken);
        var buffer = new byte[8192];
        await using var memoryStream = new MemoryStream();

        try
        {
            while (true)
            {
                var receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), deadline.Token)
                    .WaitAsync(deadline.Token).ConfigureAwait(false);
                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                await memoryStream.WriteAsync(buffer.AsMemory(0, receiveResult.Count), deadline.Token).ConfigureAwait(false);
                if (!receiveResult.EndOfMessage)
                {
                    continue;
                }

                return DebugSocketProtocol.TryDeserializeEnvelope(memoryStream.ToArray(), out var envelope)
                    ? envelope
                    : null;
            }
        }
        catch (OperationCanceledException)
        {
            socket.Abort();
            throw;
        }
    }

    private Task SendControlResponseAsync(
        WebSocket socket,
        ControlCommandResponseEnvelopeV1 response,
        CancellationToken cancellationToken) => SendResponseAsync(socket,
            DebugSocketMessageType.ControlCommandResponse, response, response.RequestId, cancellationToken);

    private async Task SendResponseAsync<T>(WebSocket socket, DebugSocketMessageType messageType,
        T response, string requestId, CancellationToken cancellationToken)
    {
        using var deadline = _createDeadline(TimeSpan.FromSeconds(3), cancellationToken);
        var frame = DebugSocketProtocol.SerializeMessage(messageType, response, requestId);
        try
        {
            await socket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary,
                endOfMessage: true, deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            socket.Abort();
            throw;
        }
    }

    private async Task TryCloseSocketAsync(WebSocket socket, string description)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var deadline = _createDeadline(TimeSpan.FromSeconds(3), CancellationToken.None);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, description, deadline.Token)
                    .WaitAsync(deadline.Token).ConfigureAwait(false);
            }
        }
        catch
        {
            socket.Abort();
        }
        finally
        {
            socket.Dispose();
        }
    }

    private static CancellationTokenSource CreateDeadline(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        return deadline;
    }

    private static DebugStudioServerOptions CreateServerOptions(DebugStudioCliControlOptions options)
    {
        var controlUri = options.ControlUri;
        if (!string.Equals(controlUri.Scheme, Uri.UriSchemeWs, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("The CLI control plane currently supports only ws:// endpoints.");
        }

        return new DebugStudioServerOptions
        {
            Host = controlUri.Host,
            Port = controlUri.Port,
            WebSocketPath = string.IsNullOrWhiteSpace(controlUri.AbsolutePath) ? "/" : controlUri.AbsolutePath,
            AcceptTimeoutSeconds = options.AcceptTimeoutSeconds,
        };
    }
}
