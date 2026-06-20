#nullable enable

using System;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// inspector query が求める情報量。
    /// v1 は viewer-first のため表示に必要な粒度だけを flag 化している。
    /// </summary>
    [Flags]
    public enum InspectorQueryFlags
    {
        None = 0,
        IncludeMetadata = 1 << 0,
        IncludeComponents = 1 << 1,
        IncludeProperties = 1 << 2,
        IncludeRawValues = 1 << 3,
    }
}
