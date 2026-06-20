#nullable enable

using System.Collections.Generic;
using DebugStudio.App.Core.Models;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// hierarchy export 用に、軽量 state と node 列を同一時点で束ねた snapshot。
/// export service が別々に lock を取り直さず、整合した tree 状態を扱えるようにする。
/// </summary>
public readonly record struct HierarchyRetainedSnapshot(
    HierarchyStoreSnapshot State,
    IReadOnlyList<HierarchyNodeRecord> Nodes);
