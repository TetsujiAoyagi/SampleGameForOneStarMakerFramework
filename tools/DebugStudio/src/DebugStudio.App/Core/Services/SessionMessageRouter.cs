#nullable enable

using System;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// DebugStudioSession から受信した protocol envelope を各 store へ配線する inbound router。
///
/// <para>
/// SessionService が直接 event handler を 10 本抱えると、
/// 「routing」「再発行」「store 更新」が混ざり責務が読みにくい。
/// このクラスは routing + store mutation だけを担い、SessionService は再発行だけに集中する。
/// </para>
/// </summary>
public sealed class SessionMessageRouter
{
    private readonly LogStore _logStore;
    private readonly HierarchyStore _hierarchyStore;
    private readonly InspectorStore _inspectorStore;
    private readonly TelemetryStore _telemetryStore;
    private readonly CommandStore _commandStore;
    private readonly CapabilityStateStore _capabilityStateStore;
    private readonly TelemetrySessionAttributesStore _telemetrySessionAttributesStore;

    public SessionMessageRouter(
        LogStore logStore,
        HierarchyStore hierarchyStore,
        InspectorStore inspectorStore,
        TelemetryStore telemetryStore,
        CommandStore commandStore,
        CapabilityStateStore capabilityStateStore,
        TelemetrySessionAttributesStore telemetrySessionAttributesStore)
    {
        _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
        _hierarchyStore = hierarchyStore ?? throw new ArgumentNullException(nameof(hierarchyStore));
        _inspectorStore = inspectorStore ?? throw new ArgumentNullException(nameof(inspectorStore));
        _telemetryStore = telemetryStore ?? throw new ArgumentNullException(nameof(telemetryStore));
        _commandStore = commandStore ?? throw new ArgumentNullException(nameof(commandStore));
        _capabilityStateStore = capabilityStateStore ?? throw new ArgumentNullException(nameof(capabilityStateStore));
        _telemetrySessionAttributesStore = telemetrySessionAttributesStore
            ?? throw new ArgumentNullException(nameof(telemetrySessionAttributesStore));
    }

    public event Action<LogRecord>? LogReceived;
    public event Action<DebugTelemetryEnvelopeV1>? TelemetryReceived;
    public event Action<DebugSocketServiceStatusEnvelopeV1>? ServiceStatusReceived;
    public event Action<DebugCommandResultEnvelopeV1>? CommandResultReceived;
    public event Action<CapabilityHandshakeWelcomeEnvelopeV1>? CapabilityWelcomeReceived;
    public event Action<HierarchySnapshotEnvelopeV1>? HierarchySnapshotReceived;
    public event Action<HierarchyDeltaEnvelopeV1>? HierarchyDeltaReceived;
    public event Action<InspectorDetailEnvelopeV1>? InspectorDetailReceived;

    public void RouteLogMessage(LogEnvelopeV1 envelope)
    {
        // Log は ring buffer へ append した後、その結果として得られた latest record を再発行する。
        // 元の envelope を外へ流すのではなく LogRecord を流すことで、
        // UI 側は retain 済みの app model を前提に扱える。
        var snapshot = _logStore.Append(envelope);
        if (snapshot.LatestRecord != null)
        {
            LogReceived?.Invoke(snapshot.LatestRecord);
        }
    }

    public void RouteTelemetryMessage(DebugTelemetryEnvelopeV1 telemetry)
    {
        _telemetryStore.AppendTelemetry(telemetry);
        TelemetryReceived?.Invoke(telemetry);
    }

    public void RouteServiceStatusMessage(DebugSocketServiceStatusEnvelopeV1 status)
    {
        _telemetryStore.AppendServiceStatus(status);
        ServiceStatusReceived?.Invoke(status);
    }

    public void RouteCommandResultMessage(DebugCommandResultEnvelopeV1 result)
    {
        // command result は negotiation 完了前や capability 非対応時には受理しない。
        // stray / stale frame で command UI 状態が巻き戻るのを避けるため。
        if (!_capabilityStateStore.Supports(DebugStudioCapability.CommandResult))
        {
            return;
        }

        _commandStore.AppendResult(result);
        CommandResultReceived?.Invoke(result);
    }

    public void RouteCapabilityWelcomeMessage(CapabilityHandshakeWelcomeEnvelopeV1 welcome)
    {
        // capability welcome だけは store mutation が単なる蓄積ではなく、
        // negotiation 結果の正本更新になる。ここで state store を更新してから外へ通知する。
        _capabilityStateStore.ApplyWelcome(welcome);
        _telemetrySessionAttributesStore.ApplyWelcome(welcome);
        CapabilityWelcomeReceived?.Invoke(welcome);
    }

    public void RouteHierarchySnapshotMessage(HierarchySnapshotEnvelopeV1 snapshot)
    {
        // snapshot は delta より強い正本なので、そのまま store 全体を差し替える。
        _hierarchyStore.ApplySnapshot(snapshot);
        HierarchySnapshotReceived?.Invoke(snapshot);
    }

    public void RouteHierarchyDeltaMessage(HierarchyDeltaEnvelopeV1 delta)
    {
        // delta の整合性判定自体は store 側へ閉じ込める。
        // router は「この envelope を hierarchy 系へ流す」という配線責務だけ持つ。
        _hierarchyStore.ApplyDelta(delta);
        HierarchyDeltaReceived?.Invoke(delta);
    }

    public void RouteInspectorDetailMessage(InspectorDetailEnvelopeV1 detail)
    {
        _inspectorStore.ApplyDetail(detail);
        InspectorDetailReceived?.Invoke(detail);
    }
}
