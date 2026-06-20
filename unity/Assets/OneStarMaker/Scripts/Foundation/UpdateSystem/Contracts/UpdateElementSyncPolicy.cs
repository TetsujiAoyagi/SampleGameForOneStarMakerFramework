using System;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// update element の同期ポリシー。
    /// handle が指す element に対して、どの同期経路を許可するかを表す。
    /// </summary>
    [Flags]
    public enum UpdateElementSyncPolicy
    {
        None = 0,
        AllowMainThreadApply = 1 << 0,
        AllowFullSyncFallback = 1 << 1,
    }
}
