#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Models;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// WPF app から見た transport session の最小契約。
///
/// client/server どちらの session transport を使う場合でも、
/// App 層が concrete 型へ直結しないようにする境界。
/// </summary>
public interface ISessionTransport : IAsyncDisposable
{
    DebugSocketConnectionState State { get; }

    event Action<DebugSocketConnectionSnapshot>? ConnectionStateChanged;
    event Action<LogEnvelopeV1>? LogReceived;
    event Action<DebugTelemetryEnvelopeV1>? TelemetryReceived;
    event Action<DebugSocketServiceStatusEnvelopeV1>? ServiceStatusReceived;
    event Action<DebugCommandResultEnvelopeV1>? CommandResultReceived;
    event Action<CapabilityHandshakeWelcomeEnvelopeV1>? CapabilityWelcomeReceived;
    event Action<HierarchySnapshotEnvelopeV1>? HierarchySnapshotReceived;
    event Action<HierarchyDeltaEnvelopeV1>? HierarchyDeltaReceived;
    event Action<InspectorDetailEnvelopeV1>? InspectorDetailReceived;

    Task ConnectAsync(DebugSocketClientOptions options, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task SendCommandAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default);
    Task SendMessageAsync<TPayload>(
        DebugSocketMessageType messageType,
        TPayload payload,
        string? requestId = null,
        CancellationToken cancellationToken = default);
}
