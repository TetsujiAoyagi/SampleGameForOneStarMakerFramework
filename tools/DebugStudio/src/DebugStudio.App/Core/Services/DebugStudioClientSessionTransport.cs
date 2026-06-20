#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Models;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// 既存の <see cref="DebugStudioSession"/> を App 層向け transport 契約へ包む adapter。
/// App 層はこの adapter 越しに扱うことで、将来 server transport へ差し替えやすくする。
/// </summary>
public sealed class DebugStudioClientSessionTransport : ISessionTransport
{
    private readonly DebugStudioSession _innerSession;

    public DebugStudioClientSessionTransport(DebugStudioSession innerSession)
    {
        _innerSession = innerSession ?? throw new ArgumentNullException(nameof(innerSession));
        _innerSession.ConnectionStateChanged += RaiseConnectionStateChanged;
        _innerSession.LogReceived += RaiseLogReceived;
        _innerSession.TelemetryReceived += RaiseTelemetryReceived;
        _innerSession.ServiceStatusReceived += RaiseServiceStatusReceived;
        _innerSession.CommandResultReceived += RaiseCommandResultReceived;
        _innerSession.CapabilityWelcomeReceived += RaiseCapabilityWelcomeReceived;
        _innerSession.HierarchySnapshotReceived += RaiseHierarchySnapshotReceived;
        _innerSession.HierarchyDeltaReceived += RaiseHierarchyDeltaReceived;
        _innerSession.InspectorDetailReceived += RaiseInspectorDetailReceived;
    }

    public DebugSocketConnectionState State => _innerSession.State;

    public event Action<DebugSocketConnectionSnapshot>? ConnectionStateChanged;
    public event Action<LogEnvelopeV1>? LogReceived;
    public event Action<DebugTelemetryEnvelopeV1>? TelemetryReceived;
    public event Action<DebugSocketServiceStatusEnvelopeV1>? ServiceStatusReceived;
    public event Action<DebugCommandResultEnvelopeV1>? CommandResultReceived;
    public event Action<CapabilityHandshakeWelcomeEnvelopeV1>? CapabilityWelcomeReceived;
    public event Action<HierarchySnapshotEnvelopeV1>? HierarchySnapshotReceived;
    public event Action<HierarchyDeltaEnvelopeV1>? HierarchyDeltaReceived;
    public event Action<InspectorDetailEnvelopeV1>? InspectorDetailReceived;

    public Task ConnectAsync(DebugSocketClientOptions options, CancellationToken cancellationToken = default)
        => _innerSession.ConnectAsync(options, cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
        => _innerSession.DisconnectAsync(cancellationToken);

    public Task SendCommandAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default)
        => _innerSession.SendCommandAsync(command, cancellationToken);

    public Task SendMessageAsync<TPayload>(
        DebugSocketMessageType messageType,
        TPayload payload,
        string? requestId = null,
        CancellationToken cancellationToken = default)
        => _innerSession.SendMessageAsync(messageType, payload, requestId, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        _innerSession.ConnectionStateChanged -= RaiseConnectionStateChanged;
        _innerSession.LogReceived -= RaiseLogReceived;
        _innerSession.TelemetryReceived -= RaiseTelemetryReceived;
        _innerSession.ServiceStatusReceived -= RaiseServiceStatusReceived;
        _innerSession.CommandResultReceived -= RaiseCommandResultReceived;
        _innerSession.CapabilityWelcomeReceived -= RaiseCapabilityWelcomeReceived;
        _innerSession.HierarchySnapshotReceived -= RaiseHierarchySnapshotReceived;
        _innerSession.HierarchyDeltaReceived -= RaiseHierarchyDeltaReceived;
        _innerSession.InspectorDetailReceived -= RaiseInspectorDetailReceived;
        await _innerSession.DisposeAsync().ConfigureAwait(false);
    }

    private void RaiseConnectionStateChanged(DebugSocketConnectionSnapshot snapshot) => ConnectionStateChanged?.Invoke(snapshot);
    private void RaiseLogReceived(LogEnvelopeV1 envelope) => LogReceived?.Invoke(envelope);
    private void RaiseTelemetryReceived(DebugTelemetryEnvelopeV1 envelope) => TelemetryReceived?.Invoke(envelope);
    private void RaiseServiceStatusReceived(DebugSocketServiceStatusEnvelopeV1 envelope) => ServiceStatusReceived?.Invoke(envelope);
    private void RaiseCommandResultReceived(DebugCommandResultEnvelopeV1 envelope) => CommandResultReceived?.Invoke(envelope);
    private void RaiseCapabilityWelcomeReceived(CapabilityHandshakeWelcomeEnvelopeV1 envelope) => CapabilityWelcomeReceived?.Invoke(envelope);
    private void RaiseHierarchySnapshotReceived(HierarchySnapshotEnvelopeV1 envelope) => HierarchySnapshotReceived?.Invoke(envelope);
    private void RaiseHierarchyDeltaReceived(HierarchyDeltaEnvelopeV1 envelope) => HierarchyDeltaReceived?.Invoke(envelope);
    private void RaiseInspectorDetailReceived(InspectorDetailEnvelopeV1 envelope) => InspectorDetailReceived?.Invoke(envelope);
}
