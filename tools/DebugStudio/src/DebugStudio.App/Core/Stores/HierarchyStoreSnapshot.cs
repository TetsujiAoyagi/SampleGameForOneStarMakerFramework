#nullable enable

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// hierarchy store の軽量状態。
/// WPF 側はノード列本体を引かずに、件数や revision だけを即時参照できる。
/// </summary>
public readonly record struct HierarchyStoreSnapshot(
    long Revision,
    long CapturedAtUnixTimeMilliseconds,
    string ScopeName,
    int NodeCount,
    long? SelectedNodeId);
