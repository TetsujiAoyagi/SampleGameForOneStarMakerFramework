#nullable enable

using System;
using System.Collections.Generic;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Stores;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// retain 済み raw log に対する検索サービス。
///
/// <para>
/// 検索ロジックを ViewModel へ埋め込むと、UI ごとに同じ条件式が増殖しやすい。
/// ここでは store snapshot を受け取り、「どの record を見せるか」の判断だけを app service 化している。
/// </para>
/// </summary>
public sealed class LogQueryService
{
    public IReadOnlyList<LogRecord> Query(LogStore store, LogQueryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        return Query(store.GetSnapshot(), options);
    }

    public IReadOnlyList<LogRecord> Query(IReadOnlyList<LogRecord> source, LogQueryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        options ??= new LogQueryOptions();
        var searchText = options.SearchText?.Trim() ?? string.Empty;
        var hasSearch = searchText.Length > 0;
        var results = new List<LogRecord>(source.Count);

        for (var index = source.Count - 1; index >= 0; index--)
        {
            var record = source[index];

            if (options.Kind.HasValue && record.Kind != options.Kind.Value)
            {
                continue;
            }

            if (hasSearch && !MatchesSearch(record, searchText))
            {
                continue;
            }

            results.Add(record);
        }

        return results;
    }

    private static bool MatchesSearch(LogRecord record, string searchText)
    {
        return Contains(record.ApplicationName, searchText) ||
            Contains(record.Category, searchText) ||
            Contains(record.Message, searchText) ||
            Contains(record.EventName, searchText) ||
            Contains(record.Exception, searchText) ||
            Contains(record.ThreadName, searchText) ||
            Contains(record.MemberName, searchText) ||
            Contains(record.FilePath, searchText);
    }

    private static bool Contains(string? candidate, string searchText)
    {
        return !string.IsNullOrEmpty(candidate) &&
            candidate.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }
}
