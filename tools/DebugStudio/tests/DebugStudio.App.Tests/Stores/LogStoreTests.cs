#nullable enable

using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Contracts.Schema;

namespace DebugStudio.App.Tests.Stores;

/// <summary>
/// LogStore の保持動作とリングバッファ不変条件を検証。
/// 
/// 主な検証点:
/// - リングバッファ満杯時の古いログ削除
/// - TotalReceived の単調増加
/// - LatestRecord の整合性
/// - スナップショット取得時の順序保証
/// </summary>
public sealed class LogStoreTests
{
    [Fact]
    public void 初期化時_空のストアが作成される()
    {
        var store = new LogStore(capacity: 10);

        var snapshot = store.GetSnapshotState();

        Assert.Equal(10, snapshot.Capacity);
        Assert.Equal(0, snapshot.RetainedCount);
        Assert.Equal(0, snapshot.TotalReceived);
        Assert.Null(snapshot.LatestRecord);
    }

    [Fact]
    public void 容量ゼロ以下でコンストラクタは例外を投げる()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogStore(capacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogStore(capacity: -1));
    }

    [Fact]
    public void 単一ログ追加_RetainedCountとTotalReceivedが1になる()
    {
        var store = new LogStore(capacity: 10);
        var envelope = CreateLogEnvelope("TestApp", "First message");

        var snapshot = store.Append(envelope);

        Assert.Equal(1, snapshot.RetainedCount);
        Assert.Equal(1, snapshot.TotalReceived);
        Assert.NotNull(snapshot.LatestRecord);
        Assert.Equal("First message", snapshot.LatestRecord!.Message);
        Assert.Equal(1, snapshot.LatestRecord.SequenceNumber);
    }

    [Fact]
    public void 複数ログ追加_TotalReceivedは単調増加する()
    {
        var store = new LogStore(capacity: 10);

        var s1 = store.Append(CreateLogEnvelope("App", "Log 1"));
        var s2 = store.Append(CreateLogEnvelope("App", "Log 2"));
        var s3 = store.Append(CreateLogEnvelope("App", "Log 3"));

        Assert.Equal(1, s1.TotalReceived);
        Assert.Equal(2, s2.TotalReceived);
        Assert.Equal(3, s3.TotalReceived);
    }

    [Fact]
    public void リングバッファ満杯時_古いログが削除される()
    {
        var store = new LogStore(capacity: 3);

        store.Append(CreateLogEnvelope("App", "Log 1"));
        store.Append(CreateLogEnvelope("App", "Log 2"));
        store.Append(CreateLogEnvelope("App", "Log 3"));
        var snapshot = store.Append(CreateLogEnvelope("App", "Log 4"));

        // リングバッファは最大3件保持、TotalReceivedは4
        Assert.Equal(3, snapshot.RetainedCount);
        Assert.Equal(4, snapshot.TotalReceived);

        // スナップショット取得して順序確認
        var records = store.GetSnapshot();
        Assert.Equal(3, records.Count);
        Assert.Equal("Log 2", records[0].Message); // Log 1 は削除された
        Assert.Equal("Log 3", records[1].Message);
        Assert.Equal("Log 4", records[2].Message);
    }

    [Fact]
    public void リングバッファが一周する_正しい順序で取得できる()
    {
        var store = new LogStore(capacity: 2);

        store.Append(CreateLogEnvelope("App", "Log 1"));
        store.Append(CreateLogEnvelope("App", "Log 2"));
        store.Append(CreateLogEnvelope("App", "Log 3"));
        store.Append(CreateLogEnvelope("App", "Log 4"));
        store.Append(CreateLogEnvelope("App", "Log 5"));

        var records = store.GetSnapshot();

        Assert.Equal(2, records.Count);
        Assert.Equal("Log 4", records[0].Message);
        Assert.Equal("Log 5", records[1].Message);

        var state = store.GetSnapshotState();
        Assert.Equal(2, state.RetainedCount);
        Assert.Equal(5, state.TotalReceived);
    }

    [Fact]
    public void LatestRecordは常に最新のログを指す()
    {
        var store = new LogStore(capacity: 3);

        var s1 = store.Append(CreateLogEnvelope("App", "First"));
        Assert.Equal("First", s1.LatestRecord!.Message);

        var s2 = store.Append(CreateLogEnvelope("App", "Second"));
        Assert.Equal("Second", s2.LatestRecord!.Message);

        var s3 = store.Append(CreateLogEnvelope("App", "Third"));
        Assert.Equal("Third", s3.LatestRecord!.Message);

        // リングバッファ満杯後も
        var s4 = store.Append(CreateLogEnvelope("App", "Fourth"));
        Assert.Equal("Fourth", s4.LatestRecord!.Message);
    }

    [Fact]
    public void GetSnapshot_時系列順にレコードを返す()
    {
        var store = new LogStore(capacity: 5);

        for (var i = 1; i <= 5; i++)
        {
            store.Append(CreateLogEnvelope("App", $"Log {i}"));
        }

        var records = store.GetSnapshot();

        Assert.Equal(5, records.Count);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal($"Log {i + 1}", records[i].Message);
            Assert.Equal(i + 1, records[i].SequenceNumber);
        }
    }

    [Fact]
    public void GetSnapshot_リングバッファ一周後も正しい順序()
    {
        var store = new LogStore(capacity: 3);

        for (var i = 1; i <= 7; i++)
        {
            store.Append(CreateLogEnvelope("App", $"Log {i}"));
        }

        var records = store.GetSnapshot();

        // 最新3件が順序通り
        Assert.Equal(3, records.Count);
        Assert.Equal("Log 5", records[0].Message);
        Assert.Equal("Log 6", records[1].Message);
        Assert.Equal("Log 7", records[2].Message);
    }

    [Fact]
    public void Changed_イベントが発火される()
    {
        var store = new LogStore(capacity: 10);
        LogStoreSnapshot? receivedSnapshot = null;
        store.Changed += snapshot => receivedSnapshot = snapshot;

        var envelope = CreateLogEnvelope("App", "Test");
        var returned = store.Append(envelope);

        Assert.NotNull(receivedSnapshot);
        Assert.Equal(returned.TotalReceived, receivedSnapshot!.Value.TotalReceived);
        Assert.Equal(returned.RetainedCount, receivedSnapshot.Value.RetainedCount);
        Assert.Equal(returned.LatestRecord?.Message, receivedSnapshot.Value.LatestRecord?.Message);
    }

    [Fact]
    public void TotalReceived_は保持件数に関係なく単調増加()
    {
        var store = new LogStore(capacity: 2);

        store.Append(CreateLogEnvelope("App", "1"));
        store.Append(CreateLogEnvelope("App", "2"));
        var s3 = store.Append(CreateLogEnvelope("App", "3"));
        var s4 = store.Append(CreateLogEnvelope("App", "4"));

        // RetainedCount は 2 だが TotalReceived は 4
        Assert.Equal(2, s4.RetainedCount);
        Assert.Equal(4, s4.TotalReceived);

        var records = store.GetSnapshot();
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public void Append_nullエンベロープは例外()
    {
        var store = new LogStore(capacity: 10);
        Assert.Throws<ArgumentNullException>(() => store.Append(null!));
    }

    [Fact]
    public async Task 複数スレッドから同時にAppend_データ破損しない()
    {
        var store = new LogStore(capacity: 100);
        var tasks = new List<Task>();
        const int threadsCount = 10;
        const int logsPerThread = 50;

        for (var t = 0; t < threadsCount; t++)
        {
            var threadId = t;
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < logsPerThread; i++)
                {
                    store.Append(CreateLogEnvelope("App", $"Thread{threadId}-Log{i}"));
                }
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        var snapshot = store.GetSnapshotState();
        Assert.Equal(threadsCount * logsPerThread, snapshot.TotalReceived);
        Assert.Equal(100, snapshot.RetainedCount); // リングバッファ容量まで
    }

    [Fact]
    public void SequenceNumberはTotalReceivedと一致()
    {
        var store = new LogStore(capacity: 10);

        for (var i = 1; i <= 5; i++)
        {
            var snapshot = store.Append(CreateLogEnvelope("App", $"Log {i}"));
            Assert.Equal(i, snapshot.LatestRecord!.SequenceNumber);
            Assert.Equal(i, snapshot.TotalReceived);
        }
    }

    private static LogEnvelopeV1 CreateLogEnvelope(string appName, string message)
    {
        return new LogEnvelopeV1
        {
            SchemaVersion = 1,
            ApplicationName = appName,
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Category = "Test",
            LogLevel = 2, // Information
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
