#nullable enable

using System;
using DebugStudio.App.Core.Stores;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// 接続/再接続時の store 群クリア／リセット戦略を一箇所に閉じ込める。
///
/// <para>
/// SessionService が各 store の詳細を直接握ると「どのタイミングで何を消すか」が散らばりやすい。
/// このポリシークラスへ切り出し、connect/disconnect ごとの責務を明示する。
/// </para>
/// </summary>
public sealed class SessionResetPolicy
{
    private readonly LogStore _logStore;
    private readonly HierarchyStore _hierarchyStore;
    private readonly InspectorStore _inspectorStore;
    private readonly TelemetryStore _telemetryStore;
    private readonly CommandStore _commandStore;
    private readonly CapabilityStateStore _capabilityStateStore;

    public SessionResetPolicy(
        LogStore logStore,
        HierarchyStore hierarchyStore,
        InspectorStore inspectorStore,
        TelemetryStore telemetryStore,
        CommandStore commandStore,
        CapabilityStateStore capabilityStateStore)
    {
        _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
        _hierarchyStore = hierarchyStore ?? throw new ArgumentNullException(nameof(hierarchyStore));
        _inspectorStore = inspectorStore ?? throw new ArgumentNullException(nameof(inspectorStore));
        _telemetryStore = telemetryStore ?? throw new ArgumentNullException(nameof(telemetryStore));
        _commandStore = commandStore ?? throw new ArgumentNullException(nameof(commandStore));
        _capabilityStateStore = capabilityStateStore ?? throw new ArgumentNullException(nameof(capabilityStateStore));
    }

    public void ResetForConnect(Uri serverUri)
    {
        _hierarchyStore.Clear();
        _inspectorStore.Clear();
        _telemetryStore.Reset();
        _commandStore.Reset();
        _capabilityStateStore.ResetForConnect(serverUri);
    }

    public void MarkDisconnected(string detail)
    {
        _commandStore.MarkDisconnected(detail, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _capabilityStateStore.MarkDisconnected(detail);
    }
}
