#nullable enable

using DebugStudio.App.Core.Models;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// command window が参照する command correlation snapshot。
/// </summary>
public readonly record struct CommandStoreSnapshot(
    long DispatchCount,
    long ResultCount,
    int PendingCount,
    int CompletedCount,
    CommandDispatchRecord? LatestEntry,
    DebugCommandResultEnvelopeV1? LatestResult,
    CommandDispatchRecord[] Entries);
