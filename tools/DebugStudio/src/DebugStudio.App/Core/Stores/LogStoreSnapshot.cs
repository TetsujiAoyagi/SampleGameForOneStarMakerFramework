#nullable enable

using DebugStudio.App.Core.Models;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// ring buffer 更新後に UI / service 層へ渡す軽量 snapshot。
/// store 内部配列そのものは公開せず、保持件数と最新要素だけを安全に共有する。
/// </summary>
/// <param name="Capacity">固定保持上限。</param>
/// <param name="RetainedCount">現在 ring buffer に残っている件数。</param>
/// <param name="TotalReceived">起動後に受け取った総 log 件数。保持件数とは別概念。</param>
/// <param name="LatestRecord">今回の追加後に最新となった record。</param>
public readonly record struct LogStoreSnapshot(
    int Capacity,
    int RetainedCount,
    long TotalReceived,
    LogRecord? LatestRecord);
