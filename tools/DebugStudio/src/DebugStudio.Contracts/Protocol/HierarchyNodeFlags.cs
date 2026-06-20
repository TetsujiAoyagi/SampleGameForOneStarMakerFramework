#nullable enable

using System;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// hierarchy node の状態フラグ。
///
/// <para>
/// WPF 側はこの bitset だけ見れば「有効か」「scene root か」などの表示判断ができる。
/// bool を複数生やすより wire size を抑えやすく、Unity 側 sender も packed に生成しやすい。
/// </para>
/// </summary>
[Flags]
public enum HierarchyNodeFlags
{
    None = 0,
    ActiveSelf = 1 << 0,
    ActiveInHierarchy = 1 << 1,
    SceneRoot = 1 << 2,
    PrefabInstance = 1 << 3,
    DontDestroyOnLoad = 1 << 4,
    HasChildren = 1 << 5,
}
