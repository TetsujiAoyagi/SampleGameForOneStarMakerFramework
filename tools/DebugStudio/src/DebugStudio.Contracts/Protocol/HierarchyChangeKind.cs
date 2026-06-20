#nullable enable

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// hierarchy delta の 1 要素が何を意味するか。
///
/// <para>
/// v1 では full replace 型の Upsert と Remove に限定し、
/// reparent / reorder のような変更も「最新ノード情報を再送する Upsert」で表現する。
/// これにより sender 実装を単純に保ちながら、receiver 側も差分適用を薄くできる。
/// </para>
/// </summary>
public enum HierarchyChangeKind
{
    Unknown = 0,
    Upsert = 1,
    Remove = 2,
}
