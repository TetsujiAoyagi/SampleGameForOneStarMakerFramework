#nullable enable

using System;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// hierarchy node の表示ヒントを packed にまとめたフラグ。
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
}
