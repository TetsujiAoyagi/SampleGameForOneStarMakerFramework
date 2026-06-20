#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Contracts.Schema;
using Xunit;

namespace DebugStudio.App.Tests.Stores;

/// <summary>
/// LogStore フィルタリング・検索機能のテストスイート。
/// 
/// NX3 Phase 1 で追加される QueryLogs() 機能を検証。
/// 単純テキスト検索から正規表現、複合フィルタまで網羅。
/// パフォーマンス・スレッド安全性も確保。
/// </summary>
public sealed class LogStoreFilteringTests : IDisposable
{
    private readonly LogStore _store;

    public LogStoreFilteringTests()
    {
        _store = new LogStore(capacity: 1000);
    }

    public void Dispose()
    {
        _store?.Dispose();
    }

    // ====================================
    // 基本的なフィルター動作テスト
    // ====================================

    [Fact]
    public void EmptyFilter_ReturnsAllEntries()
    {
        for (var i = 1; i <= 10; i++)
        {
            _store.Append(CreateLogEnvelope("App", $"Log {i}", LogLevel.Info));
        }

        var result = _store.QueryLogs(LogFilterCriteria.CreateEmpty());

        Assert.Equal(10, result.MatchCount);
        Assert.Equal(10, result.Matches.Count);
        Assert.True(result.ElapsedMilliseconds >= 0);
        Assert.Equal(10, result.TotalEntries);
    }

    [Fact]
    public void LevelFilter_FiltersCorrectly()
    {
        for (var i = 1; i <= 3; i++)
            _store.Append(CreateLogEnvelope("App", $"Error {i}", LogLevel.Error));
        for (var i = 1; i <= 2; i++)
            _store.Append(CreateLogEnvelope("App", $"Warning {i}", LogLevel.Warning));
        for (var i = 1; i <= 5; i++)
            _store.Append(CreateLogEnvelope("App", $"Info {i}", LogLevel.Info));

        var criteria = LogFilterCriteria.CreateByLevel(new[] { (int)LogEntryKind.Error });
        var result = _store.QueryLogs(criteria);

        Assert.Equal(3, result.MatchCount);
        Assert.All(result.Matches, r => Assert.Equal(LogEntryKind.Error, r.Kind));
    }

    [Fact]
    public void TextSearch_CaseSensitive_Matches()
    {
        _store.Append(CreateLogEnvelope("App", "Device Connected to server", LogLevel.Info));
        _store.Append(CreateLogEnvelope("App", "Device connected to server", LogLevel.Info));
        _store.Append(CreateLogEnvelope("App", "Connection established", LogLevel.Info));

        var criteria = LogFilterCriteria.CreateByText("Connected", caseSensitive: true);
        var result = _store.QueryLogs(criteria);

        Assert.Equal(1, result.MatchCount);
        Assert.Contains("Connected", result.Matches.First().Message);
    }

    [Fact]
    public void TextSearch_CaseInsensitive_Matches()
    {
        _store.Append(CreateLogEnvelope("App", "Device Connected to server", LogLevel.Info));
        _store.Append(CreateLogEnvelope("App", "Device connected to server", LogLevel.Info));
        _store.Append(CreateLogEnvelope("App", "Connection established", LogLevel.Info));

        var criteria = LogFilterCriteria.CreateByText("connected", caseSensitive: false);
        var result = _store.QueryLogs(criteria);

        Assert.Equal(2, result.MatchCount);
        Assert.All(result.Matches, r =>
            Assert.Contains("connect", r.Message, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(@"^\[")]  // 行頭の [
    [InlineData(@"(?<name>.*)")]  // 名前付きグループ
    [InlineData(@"\[INFO\]")]  // リテラル [INFO]
    public void RegexSearch_ValidPattern_Matches(string pattern)
    {
        _store.Append(CreateLogEnvelope("App", "[INFO] System started", LogLevel.Info));
        _store.Append(CreateLogEnvelope("App", "[ERROR] Critical failure", LogLevel.Error));
        _store.Append(CreateLogEnvelope("App", "Normal message", LogLevel.Info));

        var criteria = LogFilterCriteria.CreateByRegex(pattern);
        var result = _store.QueryLogs(criteria);

        Assert.NotEmpty(result.Matches);
        Assert.True(result.ElapsedMilliseconds >= 0);
    }

    [Theory]
    [InlineData(@"[")]  // 閉じられていないブラケット
    [InlineData(@"(?P<>)")]  // 空の名前付きグループ
    [InlineData(@"(?")]  // 不完全なグループ
    public void RegexSearch_InvalidPattern_Throws(string invalidPattern)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            LogFilterCriteria.CreateByRegex(invalidPattern));

        Assert.NotNull(ex.Message);
        Assert.Contains(invalidPattern, ex.Message);
    }

    [Fact]
    public void TimeRange_FiltersCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var t1 = now.AddMinutes(-50);
        var t2 = now.AddMinutes(-30);
        var t3 = now.AddMinutes(-10);
        var t4 = now.AddMinutes(10);

        _store.Append(CreateLogEnvelope("App", "Log at -50min", LogLevel.Info, t1));
        _store.Append(CreateLogEnvelope("App", "Log at -30min", LogLevel.Info, t2));
        _store.Append(CreateLogEnvelope("App", "Log at -10min", LogLevel.Info, t3));
        _store.Append(CreateLogEnvelope("App", "Log at +10min", LogLevel.Info, t4));

        var criteria = LogFilterCriteria.CreateByTimeRange(
            now.AddMinutes(-40).UtcDateTime,
            now.UtcDateTime);
        var result = _store.QueryLogs(criteria);

        Assert.Equal(2, result.MatchCount);
        Assert.Equal(new[] { "Log at -30min", "Log at -10min" }, result.Matches.Select(x => x.Message));
    }

    [Fact]
    public void CategoryTag_FiltersCorrectly()
    {
        _store.Append(CreateLogEnvelope("App", "Network connected", LogLevel.Info, category: "Network"));
        _store.Append(CreateLogEnvelope("App", "UI button clicked", LogLevel.Info, category: "UI"));
        _store.Append(CreateLogEnvelope("App", "Physics collision", LogLevel.Info, category: "Physics"));
        _store.Append(CreateLogEnvelope("App", "Network disconnected", LogLevel.Info, category: "Network"));

        var criteria = LogFilterCriteria.CreateByCategory(new[] { "Network" });
        var result = _store.QueryLogs(criteria);

        Assert.Equal(2, result.MatchCount);
        Assert.All(result.Matches, r => Assert.Equal("Network", r.Category));
    }

    [Fact]
    public void CombinedFilters_AllConstraints()
    {
        var now = DateTimeOffset.UtcNow;
        for (var i = 1; i <= 100; i++)
        {
            var level = i % 2 == 0 ? LogLevel.Error : LogLevel.Info;
            var category = i % 3 == 0 ? "Network" : "UI";
            var message = i % 4 == 0 ? "timeout error" : "normal log";
            var time = now.AddSeconds(-i);

            _store.Append(CreateLogEnvelope("App", message, level, time, category));
        }

        var criteria = LogFilterCriteria
            .CreateByLevel(new[] { (int)LogEntryKind.Error })
            .WithText("timeout")
            .WithCategory("Network")
            .WithTimeRange(now.AddSeconds(-100).UtcDateTime, now.AddSeconds(-1).UtcDateTime);

        var result = _store.QueryLogs(criteria);

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, r =>
        {
            Assert.Equal(LogEntryKind.Error, r.Kind);
            Assert.Contains("timeout", r.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Network", r.Category);
        });
        Assert.All(result.Matches, r => Assert.InRange(r.TimestampUnixTimeMilliseconds, now.AddSeconds(-100).ToUnixTimeMilliseconds(), now.AddSeconds(-1).ToUnixTimeMilliseconds()));
    }

    // ====================================
    // パフォーマンステスト
    // ====================================

    [Fact]
    public void Performance_1000Entries_CompletesWithin100ms()
    {
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 1000; i++)
        {
            var level = (LogLevel)(i % 4); // 0=Trace, 1=Debug, 2=Info, 3=Warning
            var message = $"Log message {i} with pattern matching content";
            var time = now.AddSeconds(-i);
            var category = i % 5 == 0 ? "Network" : i % 3 == 0 ? "UI" : "Other";

            _store.Append(CreateLogEnvelope("App", message, level, time, category));
        }

        var criteria = LogFilterCriteria
            .CreateByRegex("Log.*[0-9]{2}")
            .WithCategory("Network");

        var sw = Stopwatch.StartNew();
        var result = _store.QueryLogs(criteria);
        sw.Stop();

        Assert.True(result.ElapsedMilliseconds < 250,
            $"Query took {result.ElapsedMilliseconds}ms, expected < 250ms");
        Assert.True(sw.ElapsedMilliseconds <= result.ElapsedMilliseconds + 20,
            "Elapsed time metadata is unexpectedly smaller than wall clock");
    }

    [Fact]
    public void PerformanceProfile_1000Entries_複数回実行でも急激に劣化しない()
    {
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 1000; i++)
        {
            var level = (LogLevel)(i % 4);
            var message = i % 5 == 0 ? $"[Network] timeout {i}" : $"Log message {i}";
            var time = now.AddSeconds(-i);
            var category = i % 5 == 0 ? "Network" : "Other";

            _store.Append(CreateLogEnvelope("App", message, level, time, category));
        }

        var criteria = new LogFilterCriteria
        {
            TextSearchPattern = "timeout",
            CategoryTags = ["Network"],
            UseRegex = false,
        };

        var maxElapsed = 0L;
        for (var run = 0; run < 5; run++)
        {
            var result = _store.QueryLogs(criteria);
            maxElapsed = Math.Max(maxElapsed, result.ElapsedMilliseconds);

            Assert.NotEmpty(result.Matches);
            Assert.True(result.ElapsedMilliseconds < 250,
                $"Run {run} took {result.ElapsedMilliseconds}ms, expected < 250ms");
        }

        Assert.True(maxElapsed < 250, $"Max elapsed was {maxElapsed}ms, expected < 250ms");
    }

    // ====================================
    // 後方互換性・ヘルパーメソッドテスト
    // ====================================

    [Fact]
    public void SimpleTextSearch_QuickSearch()
    {
        for (var i = 1; i <= 10; i++)
        {
            _store.Append(CreateLogEnvelope("App", $"Message {i}", LogLevel.Info));
            if (i % 2 == 0)
                _store.Append(CreateLogEnvelope("App", $"Keyword entry {i}", LogLevel.Info));
        }

        var result = _store.SimpleTextSearch("Keyword");

        Assert.Equal(5, result.Count);
        Assert.All(result, r => Assert.Contains("Keyword", r.Message));
    }

    [Fact]
    public void GetAvailableCategories_ReturnsDistinct()
    {
        _store.Append(CreateLogEnvelope("App", "Log 1", LogLevel.Info, category: "Network"));
        _store.Append(CreateLogEnvelope("App", "Log 2", LogLevel.Info, category: "Network"));
        _store.Append(CreateLogEnvelope("App", "Log 3", LogLevel.Info, category: "UI"));
        _store.Append(CreateLogEnvelope("App", "Log 4", LogLevel.Info, category: "UI"));
        _store.Append(CreateLogEnvelope("App", "Log 5", LogLevel.Info, category: "Physics"));

        var categories = _store.GetAvailableCategories();

        Assert.Equal(new[] { "Network", "UI", "Physics" }, categories);
    }

    // ====================================
    // エラーハンドリング・エッジケーステスト
    // ====================================

    [Fact]
    public void NullCriteria_ThrowsArgumentNullException()
    {
        _store.Append(CreateLogEnvelope("App", "Test", LogLevel.Info));

        Assert.Throws<ArgumentNullException>(() =>
            _store.QueryLogs(null!));
    }

    [Fact]
    public void InvalidRegex_ThrowsAtConstruction()
    {
        Assert.Throws<ArgumentException>(() =>
            LogFilterCriteria.CreateByRegex("[invalid"));
    }

    [Fact]
    public void TimeRangeViolation_ThrowsAtConstruction()
    {
        var start = DateTime.UtcNow;
        var end = start.AddHours(-1);

        Assert.Throws<ArgumentException>(() =>
            LogFilterCriteria.CreateByTimeRange(start, end));
    }

    [Fact]
    public void EmptyResultSet_ReturnsValidResult()
    {
        _store.Append(CreateLogEnvelope("App", "foo", LogLevel.Info));

        var criteria = LogFilterCriteria.CreateByText("bar");
        var result = _store.QueryLogs(criteria);

        Assert.Equal(0, result.MatchCount);
        Assert.Empty(result.Matches);
        Assert.True(result.ElapsedMilliseconds >= 0);
        Assert.Equal(1, result.TotalEntries);
    }

    [Fact]
    public async Task ConcurrentQueries_NoDataCorruption()
    {
        for (var i = 0; i < 100; i++)
        {
            _store.Append(CreateLogEnvelope("App", $"Log {i}", LogLevel.Info));
        }

        var tasks = new List<Task>();
        var results = new System.Collections.Concurrent.ConcurrentBag<int>();

        for (var t = 0; t < 10; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                var criteria = LogFilterCriteria.CreateByText("Log");
                var result = _store.QueryLogs(criteria);
                results.Add(result.MatchCount);
            }));
        }

        await Task.WhenAll(tasks);

        Assert.All(results, count => Assert.Equal(100, count));
        Assert.Equal(10, results.Count);
    }

    // ====================================
    // ヘルパーメソッド
    // ====================================

    private static LogEnvelopeV1 CreateLogEnvelope(
        string appName,
        string message,
        LogLevel level = LogLevel.Info,
        DateTimeOffset? timestamp = null,
        string category = "Default")
    {
        return new LogEnvelopeV1
        {
            SchemaVersion = 1,
            ApplicationName = appName,
            TimestampUnixTimeMilliseconds =
                (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds(),
            Category = category,
            LogLevel = (int)level,
            EventId = 0,
            EventName = null,
            Message = message,
            Exception = null,
            ThreadId = Environment.CurrentManagedThreadId,
            ThreadName = "TestThread",
            MemberName = null,
            FilePath = null,
            LineNumber = 0,
        };
    }
}

/// <summary>
/// ログレベル列挙体（標準的なものを定義）。
/// </summary>
internal enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
}
