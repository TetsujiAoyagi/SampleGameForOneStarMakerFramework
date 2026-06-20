#nullable enable

using DebugStudio.Contracts.Schema;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// log query 条件の最小セット。
/// Wave 2 では free text と kind 絞り込みに留め、後続で field 単位検索へ拡張しやすい形にする。
/// </summary>
public sealed class LogQueryOptions
{
    public string SearchText { get; init; } = string.Empty;

    public LogEntryKind? Kind { get; init; }
}
