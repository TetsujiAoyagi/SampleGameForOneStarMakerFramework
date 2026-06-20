#nullable enable

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// hierarchy delta の 1 変更が何を意味するか。
    /// v1 は full-node Upsert と Remove に限定する。
    /// </summary>
    public enum HierarchyChangeKind
    {
        Unknown = 0,
        Upsert = 1,
        Remove = 2,
    }
}
