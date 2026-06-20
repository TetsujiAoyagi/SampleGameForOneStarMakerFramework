#nullable enable

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Models;
using DebugStudio.Client;
using DebugStudio.Client.Internal;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Server;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// DebugStudio.Server の待受を使って inbound WebSocket を受け取り、
/// App 層からは通常の session transport と同じ形で扱えるようにする adapter。
/// </summary>
public sealed class DebugStudioServerSessionTransport : ISessionTransport
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly DebugStudioServerOptions _serverOptionsTemplate;
    private readonly DebugSocketInboundRouter _inboundRouter;
    private readonly DebugSocketReceiveLoop _receiveLoop;

    private DebugStudioWebSocketServer? _server;
    private WebSocket? _socket;
    private CancellationTokenSource? _sessionCts;
    private Task? _receiveLoopTask;
    private Uri? _serverUri;
    private bool _disposed;

    public DebugStudioServerSessionTransport(DebugStudioServerOptions? serverOptions = null)
    {
        _serverOptionsTemplate = CloneServerOptions(serverOptions ?? new DebugStudioServerOptions());
        _inboundRouter = new DebugSocketInboundRouter();
        _receiveLoop = new DebugSocketReceiveLoop(_inboundRouter);

        _inboundRouter.LogReceived += RaiseLogReceived;
        _inboundRouter.TelemetryReceived += RaiseTelemetryReceived;
        _inboundRouter.ServiceStatusReceived += RaiseServiceStatusReceived;
        _inboundRouter.CommandResultReceived += RaiseCommandResultReceived;
        _inboundRouter.CapabilityWelcomeReceived += RaiseCapabilityWelcomeReceived;
        _inboundRouter.HierarchySnapshotReceived += RaiseHierarchySnapshotReceived;
        _inboundRouter.HierarchyDeltaReceived += RaiseHierarchyDeltaReceived;
        _inboundRouter.InspectorDetailReceived += RaiseInspectorDetailReceived;
    }

    public DebugSocketConnectionState State { get; private set; } = DebugSocketConnectionState.Disconnected;

    public event Action<DebugSocketConnectionSnapshot>? ConnectionStateChanged;
    public event Action<LogEnvelopeV1>? LogReceived;
    public event Action<DebugTelemetryEnvelopeV1>? TelemetryReceived;
    public event Action<DebugSocketServiceStatusEnvelopeV1>? ServiceStatusReceived;
    public event Action<DebugCommandResultEnvelopeV1>? CommandResultReceived;
    public event Action<CapabilityHandshakeWelcomeEnvelopeV1>? CapabilityWelcomeReceived;
    public event Action<HierarchySnapshotEnvelopeV1>? HierarchySnapshotReceived;
    public event Action<HierarchyDeltaEnvelopeV1>? HierarchyDeltaReceived;
    public event Action<InspectorDetailEnvelopeV1>? InspectorDetailReceived;

    public async Task ConnectAsync(DebugSocketClientOptions options, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(options);

        var serverUri = options.ServerUri;
        var server = new DebugStudioWebSocketServer(CreateServerOptions(serverUri));
        var sessionCts = new CancellationTokenSource();
        WebSocket? acceptedSocket = null;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_server != null || _socket != null || _receiveLoopTask != null)
            {
                throw new InvalidOperationException("DebugStudio session transport is already active.");
            }

            _server = server;
            _sessionCts = sessionCts;
            _serverUri = serverUri;
        }
        finally
        {
            _gate.Release();
        }

        UpdateState(
            DebugSocketConnectionState.Connecting,
            serverUri,
            $"Waiting for an inbound DebugStudio.Server WebSocket on {serverUri}...");

        try
        {
            server.StartListening();

            // connect 呼び出しの cancel と transport 自身の stop/dispose を両方 accept 待機へ流す。
            // どちらか一方だけだと「UI は切断済みなのに accept が残る」状態になりやすい。
            using var acceptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionCts.Token);
            var context = await server.AcceptWebSocketAsync(acceptCts.Token).ConfigureAwait(false);
            acceptedSocket = context.WebSocket;

            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (!ReferenceEquals(_server, server))
                {
                    await TryCloseSocketAsync(acceptedSocket, "server-session-replaced", CancellationToken.None).ConfigureAwait(false);
                    throw new OperationCanceledException("The pending server session was replaced before it became active.");
                }

                _socket = acceptedSocket;
                _receiveLoopTask = StartReceiveLoopAsync(server, acceptedSocket, serverUri, sessionCts.Token);
            }
            finally
            {
                _gate.Release();
            }

            UpdateState(
                DebugSocketConnectionState.Connected,
                serverUri,
                $"Accepted an inbound DebugStudio.Server WebSocket on {serverUri}.");
        }
        catch (Exception ex)
        {
            if (await TryDetachCurrentSessionAsync(server).ConfigureAwait(false) &&
                !_disposed &&
                !(ex is OperationCanceledException && sessionCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested))
            {
                UpdateState(DebugSocketConnectionState.Faulted, serverUri, ex.Message);
            }

            await ShutdownSessionAsync(
                    server,
                    acceptedSocket,
                    receiveLoopTask: null,
                    sessionCts,
                    closeDescription: "server-connect-aborted",
                    cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return DisconnectAsyncCore(updateState: true, cancellationToken);
    }

    public Task SendCommandAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return SendMessageAsync(DebugSocketMessageType.DebugCommand, command, command.RequestId, cancellationToken);
    }

    public async Task SendMessageAsync<TPayload>(
        DebugSocketMessageType messageType,
        TPayload payload,
        string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_socket == null)
            {
                throw new InvalidOperationException("DebugStudio session is not connected.");
            }

            await DebugSocketSendOperations.SendMessageAsync(
                    _socket,
                    messageType,
                    payload,
                    requestId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _inboundRouter.LogReceived -= RaiseLogReceived;
        _inboundRouter.TelemetryReceived -= RaiseTelemetryReceived;
        _inboundRouter.ServiceStatusReceived -= RaiseServiceStatusReceived;
        _inboundRouter.CommandResultReceived -= RaiseCommandResultReceived;
        _inboundRouter.CapabilityWelcomeReceived -= RaiseCapabilityWelcomeReceived;
        _inboundRouter.HierarchySnapshotReceived -= RaiseHierarchySnapshotReceived;
        _inboundRouter.HierarchyDeltaReceived -= RaiseHierarchyDeltaReceived;
        _inboundRouter.InspectorDetailReceived -= RaiseInspectorDetailReceived;
        await DisconnectAsyncCore(updateState: false, CancellationToken.None).ConfigureAwait(false);
        _gate.Dispose();
    }

    private async Task StartReceiveLoopAsync(
        DebugStudioWebSocketServer server,
        WebSocket socket,
        Uri serverUri,
        CancellationToken cancellationToken)
    {
        var result = await _receiveLoop.RunAsync(socket, cancellationToken).ConfigureAwait(false);
        await FinalizeReceiveLoopAsync(server, socket, serverUri, result.FatalError, result.Detail).ConfigureAwait(false);
    }

    private async Task FinalizeReceiveLoopAsync(
        DebugStudioWebSocketServer server,
        WebSocket socket,
        Uri serverUri,
        Exception? fatalError,
        string detail)
    {
        CancellationTokenSource? sessionCts;

        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(_server, server) || !ReferenceEquals(_socket, socket))
            {
                return;
            }

            sessionCts = _sessionCts;
            _server = null;
            _socket = null;
            _sessionCts = null;
            _receiveLoopTask = null;
            _serverUri = null;
        }
        finally
        {
            _gate.Release();
        }

        await ShutdownSessionAsync(
                server,
                socket,
                receiveLoopTask: null,
                sessionCts,
                closeDescription: fatalError == null ? "server-remote-close" : "server-transport-fault",
                cancellationToken: CancellationToken.None)
            .ConfigureAwait(false);

        if (!_disposed)
        {
            UpdateState(
                fatalError == null ? DebugSocketConnectionState.Disconnected : DebugSocketConnectionState.Faulted,
                serverUri,
                detail);
        }
    }

    private async Task DisconnectAsyncCore(bool updateState, CancellationToken cancellationToken)
    {
        DebugStudioWebSocketServer? server;
        WebSocket? socket;
        CancellationTokenSource? sessionCts;
        Task? receiveLoopTask;
        Uri? serverUri;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            server = _server;
            socket = _socket;
            sessionCts = _sessionCts;
            receiveLoopTask = _receiveLoopTask;
            serverUri = _serverUri;

            _server = null;
            _socket = null;
            _sessionCts = null;
            _receiveLoopTask = null;
            _serverUri = null;
        }
        finally
        {
            _gate.Release();
        }

        if (server == null && socket == null && sessionCts == null)
        {
            if (updateState)
            {
                UpdateState(DebugSocketConnectionState.Disconnected, serverUri, "Disconnected.");
            }

            return;
        }

        if (updateState)
        {
            UpdateState(DebugSocketConnectionState.Disconnecting, serverUri, "Disconnecting...");
        }

        await ShutdownSessionAsync(
                server,
                socket,
                receiveLoopTask,
                sessionCts,
                closeDescription: "server-disconnect",
                cancellationToken)
            .ConfigureAwait(false);

        if (updateState && !_disposed)
        {
            UpdateState(DebugSocketConnectionState.Disconnected, serverUri, "Disconnected.");
        }
    }

    private async Task<bool> TryDetachCurrentSessionAsync(DebugStudioWebSocketServer server)
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(_server, server))
            {
                return false;
            }

            _server = null;
            _socket = null;
            _sessionCts = null;
            _receiveLoopTask = null;
            _serverUri = null;
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task ShutdownSessionAsync(
        DebugStudioWebSocketServer? server,
        WebSocket? socket,
        Task? receiveLoopTask,
        CancellationTokenSource? sessionCts,
        string closeDescription,
        CancellationToken cancellationToken)
    {
        try
        {
            sessionCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        if (socket != null)
        {
            await TryCloseSocketAsync(socket, closeDescription, cancellationToken).ConfigureAwait(false);
        }

        if (receiveLoopTask != null)
        {
            try
            {
                await receiveLoopTask.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        if (server != null)
        {
            server.Stop();
            server.Dispose();
        }

        sessionCts?.Dispose();
    }

    private static async Task TryCloseSocketAsync(WebSocket socket, string description, CancellationToken cancellationToken)
    {
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, description, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
        }
    }

    private DebugStudioServerOptions CreateServerOptions(Uri serverUri)
    {
        if (!_serverOptionsTemplate.Enabled)
        {
            throw new InvalidOperationException("DebugStudio server transport is disabled.");
        }

        if (!string.Equals(serverUri.Scheme, Uri.UriSchemeWs, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("DebugStudio server transport currently supports only ws:// endpoints.");
        }

        var serverOptions = CloneServerOptions(_serverOptionsTemplate);
        serverOptions.Host = serverUri.Host;
        serverOptions.Port = serverUri.Port;
        serverOptions.WebSocketPath = string.IsNullOrWhiteSpace(serverUri.AbsolutePath) ? "/" : serverUri.AbsolutePath;
        return serverOptions;
    }

    private static DebugStudioServerOptions CloneServerOptions(DebugStudioServerOptions options)
    {
        return new DebugStudioServerOptions
        {
            Host = options.Host,
            Port = options.Port,
            WebSocketPath = options.WebSocketPath,
            Enabled = options.Enabled,
            AcceptTimeoutSeconds = options.AcceptTimeoutSeconds,
        };
    }

    private void UpdateState(DebugSocketConnectionState state, Uri? serverUri, string detail)
    {
        State = state;

        ConnectionStateChanged?.Invoke(new DebugSocketConnectionSnapshot(
            state,
            serverUri,
            detail,
            DateTimeOffset.Now));
    }

    private void RaiseLogReceived(LogEnvelopeV1 envelope) => LogReceived?.Invoke(envelope);
    private void RaiseTelemetryReceived(DebugTelemetryEnvelopeV1 envelope) => TelemetryReceived?.Invoke(envelope);
    private void RaiseServiceStatusReceived(DebugSocketServiceStatusEnvelopeV1 envelope) => ServiceStatusReceived?.Invoke(envelope);
    private void RaiseCommandResultReceived(DebugCommandResultEnvelopeV1 envelope) => CommandResultReceived?.Invoke(envelope);
    private void RaiseCapabilityWelcomeReceived(CapabilityHandshakeWelcomeEnvelopeV1 envelope) => CapabilityWelcomeReceived?.Invoke(envelope);
    private void RaiseHierarchySnapshotReceived(HierarchySnapshotEnvelopeV1 envelope) => HierarchySnapshotReceived?.Invoke(envelope);
    private void RaiseHierarchyDeltaReceived(HierarchyDeltaEnvelopeV1 envelope) => HierarchyDeltaReceived?.Invoke(envelope);
    private void RaiseInspectorDetailReceived(InspectorDetailEnvelopeV1 envelope) => InspectorDetailReceived?.Invoke(envelope);
}
