#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using DebugStudio.App.Core.Models;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// log 保持用の固定長 ring buffer store。
///
/// <para>
/// 大量 log を無制限に <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/> へ積むと
/// UI と保持の責務が癒着し、メモリ使用量も予測しづらくなる。
/// そのため app 層ではまず固定長 buffer へ保持し、ViewModel は必要時に snapshot/query 結果だけを引く。
/// ここで retention 戦略を閉じ込めることで、後続 wave の export や replay でも同じ基盤を再利用できる。
/// </para>
/// </summary>
public sealed class LogStore : IDisposable
{
    private readonly object _gate = new();
    private readonly LogRecord?[] _buffer;
    private int _head;
    private int _count;
    private long _totalReceived;

    public LogStore(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _buffer = new LogRecord[capacity];
    }

    public event Action<LogStoreSnapshot>? Changed;

    public int Capacity => _buffer.Length;

    public LogStoreSnapshot Append(LogEnvelopeV1 envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        LogStoreSnapshot snapshot;
        lock (_gate)
        {
            _totalReceived++;
            var record = LogRecord.FromEnvelope(_totalReceived, envelope);

            if (_count < _buffer.Length)
            {
                _buffer[(_head + _count) % _buffer.Length] = record;
                _count++;
            }
            else
            {
                _buffer[_head] = record;
                _head = (_head + 1) % _buffer.Length;
            }

            snapshot = new LogStoreSnapshot(_buffer.Length, _count, _totalReceived, record);
        }

        Changed?.Invoke(snapshot);
        return snapshot;
    }

    public LogStoreSnapshot GetSnapshotState()
    {
        lock (_gate)
        {
            return new LogStoreSnapshot(_buffer.Length, _count, _totalReceived, GetLatestRecordUnsafe());
        }
    }

    public IReadOnlyList<LogRecord> GetSnapshot()
    {
        lock (_gate)
        {
            var results = new List<LogRecord>(_count);
            for (var index = 0; index < _count; index++)
            {
                var record = _buffer[(_head + index) % _buffer.Length];
                if (record != null)
                {
                    results.Add(record);
                }
            }

            return results;
        }
    }

    /// <summary>
    /// 指定された検索条件にマッチするログエントリを取得します。
    /// 
    /// <para>
    /// 複数の条件（テキスト検索、ログレベル、カテゴリタグ、時間範囲）を組み合わせて
    /// フィルタリングを実施します。フィルタ条件が空（LogFilterCriteria.IsEmpty == true）
    /// の場合、全エントリを返します。
    /// 
    /// フィルタ適用順序は以下の通り（パフォーマンス最適化）：
    /// 1. 時間範囲フィルタ（最速、大量削除）
    /// 2. ログレベルフィルタ（高速、メモリ効率的）
    /// 3. カテゴリタグフィルタ（中程度の速度）
    /// 4. テキスト検索（最遅、正規表現は特に遅い）
    /// </para>
    /// </summary>
    /// <param name="criteria">フィルタ条件。null 不可。</param>
    /// <returns>検索結果（マッチしたエントリと統計情報）</returns>
    /// <remarks>
    /// 正規表現パターンが無効な場合、ArgumentException を投げます。
    /// プレーンテキスト検索では <see cref="LogFilterCriteria.CaseSensitive"/> に応じて
    /// 大文字小文字の区別を切り替えます。
    /// 正規表現有効時は、パターン側の指定をそのまま尊重します。
    /// </remarks>
    public LogSearchResult QueryLogs(LogFilterCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var stopwatch = Stopwatch.StartNew();
        List<LogRecord> records;
        var totalEntries = 0;
        var lockElapsedMilliseconds = 0L;

        lock (_gate)
        {
            if (_count == 0)
            {
                stopwatch.Stop();
                return new LogSearchResult
                {
                    Matches = Array.Empty<LogRecord>(),
                    MatchCount = 0,
                    TotalEntries = 0,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                };
            }

            // 空のフィルタ→全エントリを返す
            if (criteria.IsEmpty)
            {
                var allRecords = new List<LogRecord>(_count);
                for (var index = 0; index < _count; index++)
                {
                    var record = _buffer[(_head + index) % _buffer.Length];
                    if (record != null)
                    {
                        allRecords.Add(record);
                    }
                }

                stopwatch.Stop();
                return new LogSearchResult
                {
                    Matches = allRecords,
                    MatchCount = allRecords.Count,
                    TotalEntries = _count,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                };
            }

            // スナップショット取得してから条件適用（ロック最小化）
            records = new List<LogRecord>(_count);
            totalEntries = _count;
            for (var index = 0; index < _count; index++)
            {
                var record = _buffer[(_head + index) % _buffer.Length];
                if (record != null)
                {
                    records.Add(record);
                }
            }

            stopwatch.Stop();
            lockElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
        }

        // ロック終了、以下はロック外で実行

        // 1. 時間範囲フィルタ（最速）
        IEnumerable<LogRecord> filtered = records;
        if (criteria.StartTime.HasValue)
        {
            var startMs = criteria.StartTime.Value.ToUnixTimeMilliseconds();
            filtered = filtered.Where(r => r.TimestampUnixTimeMilliseconds >= startMs);
        }

        if (criteria.EndTime.HasValue)
        {
            var endMs = criteria.EndTime.Value.ToUnixTimeMilliseconds();
            filtered = filtered.Where(r => r.TimestampUnixTimeMilliseconds <= endMs);
        }

        // 2. ログレベルフィルタ
        if (criteria.LevelFilters != null && criteria.LevelFilters.Length > 0)
        {
            var levelSet = new HashSet<byte>(criteria.LevelFilters.Cast<byte>());
            filtered = filtered.Where(r => levelSet.Contains((byte)r.Kind));
        }

        // 3. カテゴリタグフィルタ
        if (criteria.CategoryTags != null && criteria.CategoryTags.Length > 0)
        {
            var categorySet = new HashSet<string>(criteria.CategoryTags, StringComparer.Ordinal);
            filtered = filtered.Where(r => categorySet.Contains(r.Category));
        }

        // 4. テキスト検索（最遅）
        if (!string.IsNullOrEmpty(criteria.TextSearchPattern))
        {
            if (criteria.UseRegex)
            {
                // 正規表現モード
                try
                {
                    var regex = new Regex(criteria.TextSearchPattern, RegexOptions.Compiled);
                    filtered = filtered.Where(r =>
                        regex.IsMatch(r.Message) ||
                        regex.IsMatch(r.Category) ||
                        (r.EventName != null && regex.IsMatch(r.EventName)) ||
                        (r.Exception != null && regex.IsMatch(r.Exception))
                    );
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"正規表現パターンが無効です: {criteria.TextSearchPattern}", ex);
                }
            }
            else
            {
                // プレーンテキスト
                var comparison = criteria.CaseSensitive
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;

                filtered = filtered.Where(r =>
                    r.Message.Contains(criteria.TextSearchPattern, comparison) ||
                    r.Category.Contains(criteria.TextSearchPattern, comparison) ||
                    (r.EventName != null && r.EventName.Contains(criteria.TextSearchPattern, comparison)) ||
                    (r.Exception != null && r.Exception.Contains(criteria.TextSearchPattern, comparison))
                );
            }
        }

        var matchedRecords = filtered.ToList();
        stopwatch.Stop();

        return new LogSearchResult
        {
            Matches = matchedRecords,
            MatchCount = matchedRecords.Count,
            TotalEntries = totalEntries,
            ElapsedMilliseconds = lockElapsedMilliseconds + stopwatch.ElapsedMilliseconds,
        };
    }

    /// <summary>
    /// 特定のキーワードを含むログエントリをシンプルにテキスト検索します。
    /// 
    /// <para>
    /// UI の quick-search 機能など、シンプルなキーワード検索に用いられます。
    /// Message, Category, EventName, Exception フィールドに対して検索を実施します。
    /// </para>
    /// </summary>
    /// <param name="keyword">検索キーワード。null または空文字列の場合は全エントリを返します。</param>
    /// <param name="caseSensitive">大文字小文字を区別するかどうか。デフォルト: false</param>
    /// <returns>マッチしたログエントリのリスト（時系列順）</returns>
    /// <remarks>
    /// 正規表現は使用されません。プレーンテキストマッチのみ。
    /// キーワードが空の場合は、GetSnapshot() と同等の全エントリを返します。
    /// </remarks>
    public IReadOnlyList<LogRecord> SimpleTextSearch(string keyword, bool caseSensitive = false)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            return GetSnapshot();
        }

        lock (_gate)
        {
            var results = new List<LogRecord>(_count);
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            for (var index = 0; index < _count; index++)
            {
                var record = _buffer[(_head + index) % _buffer.Length];
                if (record == null)
                {
                    continue;
                }

                if (record.Message.IndexOf(keyword, comparison) >= 0 ||
                    record.Category.IndexOf(keyword, comparison) >= 0 ||
                    (record.EventName != null && record.EventName.IndexOf(keyword, comparison) >= 0) ||
                    (record.Exception != null && record.Exception.IndexOf(keyword, comparison) >= 0))
                {
                    results.Add(record);
                }
            }

            return results;
        }
    }

    /// <summary>
    /// 登録済みの全カテゴリタグを列挙します。
    /// 
    /// <para>
    /// UI の filter dropdown や category selector が利用可能なカテゴリを列挙する場合に使用します。
    /// 重複は除去され、ソート順は出現順（保持バッファ内での時系列順）です。
    /// </para>
    /// </summary>
    /// <returns>
    /// 相異なるカテゴリタグのリスト（重複なし、時系列順）。
    /// ストアが空の場合は空リストを返します。
    /// </returns>
    /// <remarks>
    /// 返されるリストは snapshot のため、呼び出し後のストア変更は反映されません。
    /// 新しいカテゴリが追加された場合は、再度このメソッドを呼び出す必要があります。
    /// </remarks>
    public IReadOnlyList<string> GetAvailableCategories()
    {
        lock (_gate)
        {
            var categoriesSet = new HashSet<string>(StringComparer.Ordinal);
            var categoriesList = new List<string>();

            for (var index = 0; index < _count; index++)
            {
                var record = _buffer[(_head + index) % _buffer.Length];
                if (record != null && !categoriesSet.Contains(record.Category))
                {
                    categoriesSet.Add(record.Category);
                    categoriesList.Add(record.Category);
                }
            }

            return categoriesList;
        }
    }

    private LogRecord? GetLatestRecordUnsafe()
    {
        if (_count == 0)
        {
            return null;
        }

        var latestIndex = (_head + _count - 1) % _buffer.Length;
        return _buffer[latestIndex];
    }

    public void Dispose()
    {
        // No-op: fixed array doesn't require disposal
        // Kept for IDisposable contract compatibility
    }
}
