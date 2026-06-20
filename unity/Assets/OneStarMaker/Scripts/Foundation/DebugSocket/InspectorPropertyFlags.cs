#nullable enable

using System;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// inspector property の表示ヒント。
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
}
