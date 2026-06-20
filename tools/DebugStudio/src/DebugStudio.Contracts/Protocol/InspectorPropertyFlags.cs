#nullable enable

using System;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// inspector property の表示ヒント。
/// v1 では read-only 表示を前提にしつつ、後続 wave で編集可否や null 状態を使い回せる形にする。
/// </summary>
[Flags]
public enum InspectorPropertyFlags
{
    None = 0,
    ReadOnly = 1 << 0,
    Nullable = 1 << 1,
    ExpandedByDefault = 1 << 2,
    MissingValue = 1 << 3,
}
