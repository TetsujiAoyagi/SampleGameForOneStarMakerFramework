#nullable enable

namespace DebugStudio.App.Core.Models;

/// <summary>
/// command dispatch 1 件の進行状態。
/// pending だけでなく、送信失敗・timeout・切断終端も UI から追えるようにする。
/// </summary>
public enum CommandDispatchState
{
    Pending,
    Succeeded,
    Failed,
    DispatchFailed,
    TimedOut,
    Disconnected,
    Orphaned,
}
