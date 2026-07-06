#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.Export.Tests;

/// <summary>
/// rolling NDJSON writer の rotation / prune / resume 動作を検証する。
/// </summary>
public sealed class RollingLogFileWriterTests
{
    [Fact]
    public async Task Enqueue_最大サイズ超過で新しいファイルへrollする()
    {
        var directory = CreateTempDirectory();

        try
        {
            await using (var writer = new RollingLogFileWriter(directory, maxFileSizeBytes: 200))
            {
                for (var index = 0; index < 6; index++)
                {
                    writer.Enqueue(CreateRecord(index, $"roll-message-{index}"));
                }
            }

            var files = Directory.GetFiles(directory, "debugstudio-logs_*.ndjson");
            Assert.True(files.Length >= 2, $"Expected at least 2 rolled files, but found {files.Length}.");
        }
        finally
        {
            CleanupDirectory(directory);
        }
    }

    [Fact]
    public async Task Enqueue_最大世代数を超えた古いsequenceのファイルから順に削除される()
    {
        var directory = CreateTempDirectory();
        var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // 既存の古い世代を seed し、削除順が sequence 昇順であることを名前で確かめる。
        await File.WriteAllTextAsync(
            Path.Combine(directory, $"debugstudio-logs_{today}_001.ndjson"),
            "{\"seed\":1}\n");
        await File.WriteAllTextAsync(
            Path.Combine(directory, $"debugstudio-logs_{today}_002.ndjson"),
            "{\"seed\":2}\n");

        try
        {
            // 1 record が maxFileSizeBytes を超えるサイズ設定にして、record ごとに roll させる。
            await using (var writer = new RollingLogFileWriter(
                directory,
                maxFileSizeBytes: 120,
                maxGenerations: 2))
            {
                for (var index = 0; index < 3; index++)
                {
                    writer.Enqueue(CreateRecord(index, $"prune-message-{index:D3}"));
                }
            }

            var fileNames = Directory.GetFiles(directory, "debugstudio-logs_*.ndjson")
                .Select(Path.GetFileName)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray();

            // seed 002 の続きから roll が進み、最古 sequence から削除されて最新 2 世代だけが残る。
            Assert.Equal(
                new[]
                {
                    $"debugstudio-logs_{today}_004.ndjson",
                    $"debugstudio-logs_{today}_005.ndjson",
                },
                fileNames);
        }
        finally
        {
            CleanupDirectory(directory);
        }
    }

    [Fact]
    public async Task Enqueue_最大世代数1でも書き込み中のactiveファイルは削除されない()
    {
        var directory = CreateTempDirectory();
        var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        try
        {
            await using (var writer = new RollingLogFileWriter(
                directory,
                maxFileSizeBytes: 120,
                maxGenerations: 1))
            {
                for (var index = 0; index < 3; index++)
                {
                    writer.Enqueue(CreateRecord(index, $"active-guard-{index}"));
                }
            }

            // 各 roll 直後の prune で active ファイルが消されていれば書き込みは失敗している。
            // 最新 sequence のファイル 1 つだけが残り、最後の record を保持していること。
            var files = Directory.GetFiles(directory, "debugstudio-logs_*.ndjson");
            var fileName = Path.GetFileName(Assert.Single(files));
            Assert.Equal($"debugstudio-logs_{today}_003.ndjson", fileName);

            var lines = await File.ReadAllLinesAsync(files[0]);
            Assert.Single(lines);
            Assert.Contains("active-guard-2", lines[0], StringComparison.Ordinal);
        }
        finally
        {
            CleanupDirectory(directory);
        }
    }

    [Fact]
    public async Task Enqueue_書き出し結果は有効なNDJSONでfield値が一致する()
    {
        var directory = CreateTempDirectory();
        var record = CreateRecord(
            42,
            "ndjson-validation",
            category: "Network",
            kind: "Warning",
            logLevel: "warning");

        try
        {
            await using (var writer = new RollingLogFileWriter(directory, maxFileSizeBytes: 4096))
            {
                writer.Enqueue(record);
            }

            var filePath = Directory.GetFiles(directory, "debugstudio-logs_*.ndjson").Single();

            // BOM 付きだと行指向 consumer が 1 行目を JSON として読めないため、raw byte で先頭を検証する。
            var rawBytes = await File.ReadAllBytesAsync(filePath);
            Assert.True(rawBytes.Length >= 3);
            Assert.False(
                rawBytes[0] == 0xEF && rawBytes[1] == 0xBB && rawBytes[2] == 0xBF,
                "The rolled file must not start with a UTF-8 BOM.");

            var lines = await File.ReadAllLinesAsync(filePath);
            Assert.Single(lines);

            using var document = JsonDocument.Parse(lines[0]);
            var root = document.RootElement;

            Assert.Equal(record.Message, root.GetProperty("message").GetString());
            Assert.Equal(record.SequenceNumber, root.GetProperty("sequenceNumber").GetInt64());
            Assert.Equal(record.ApplicationName, root.GetProperty("applicationName").GetString());
            Assert.Equal(record.Category, root.GetProperty("category").GetString());
            Assert.Equal(record.Kind, root.GetProperty("kind").GetString());
            Assert.Equal(record.LogLevel, root.GetProperty("logLevel").GetString());
            Assert.Equal(record.TimestampUtc, root.GetProperty("@timestamp").GetString());
        }
        finally
        {
            CleanupDirectory(directory);
        }
    }

    [Fact]
    public async Task Enqueue_当日の既存最大sequenceの次へ続きから書き込む()
    {
        var directory = CreateTempDirectory();
        var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var existingPath = Path.Combine(directory, $"debugstudio-logs_{today}_002.ndjson");
        await File.WriteAllTextAsync(existingPath, "{\"seed\":true}\n");

        try
        {
            await using (var writer = new RollingLogFileWriter(directory, maxFileSizeBytes: 4096))
            {
                writer.Enqueue(CreateRecord(99, "continued-sequence"));
            }

            var continuedPath = Path.Combine(directory, $"debugstudio-logs_{today}_002.ndjson");
            var lines = await File.ReadAllLinesAsync(continuedPath);
            Assert.Equal(2, lines.Length);
            Assert.Contains("continued-sequence", lines[1], StringComparison.Ordinal);

            var unexpectedPath = Path.Combine(directory, $"debugstudio-logs_{today}_003.ndjson");
            Assert.False(File.Exists(unexpectedPath));
        }
        finally
        {
            CleanupDirectory(directory);
        }
    }

    [Fact]
    public async Task DisposeAsync_未処理queueをflushしてから終了する()
    {
        var directory = CreateTempDirectory();

        try
        {
            var writer = new RollingLogFileWriter(directory, maxFileSizeBytes: 4096);
            const int recordCount = 5;
            for (var index = 0; index < recordCount; index++)
            {
                writer.Enqueue(CreateRecord(index, $"flush-{index}"));
            }

            await writer.DisposeAsync();

            var lines = await ReadAllLinesAsync(directory);
            Assert.Equal(recordCount, lines.Length);
        }
        finally
        {
            CleanupDirectory(directory);
        }
    }

    private static LogExportRecord CreateRecord(
        long sequenceNumber,
        string message,
        string category = "Default",
        string kind = "Information",
        string logLevel = "info")
    {
        return new LogExportRecord
        {
            TimestampUtc = "2026-07-06T12:34:56.789Z",
            SequenceNumber = sequenceNumber,
            ApplicationName = "TestApp",
            TimestampUnixTimeMilliseconds = DateTimeOffset.Parse("2026-07-06T12:34:56.789Z").ToUnixTimeMilliseconds(),
            TimestampLocal = "2026-07-06 21:34:56.789 +09:00",
            Kind = kind,
            RawLogLevel = 2,
            Category = category,
            EventId = 0,
            Message = message,
            ThreadId = Environment.CurrentManagedThreadId,
            LineNumber = 0,
            ServiceName = "TestApp",
            LogLevel = logLevel,
        };
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task<string[]> ReadAllLinesAsync(string directory)
    {
        var filePath = Directory.GetFiles(directory, "debugstudio-logs_*.ndjson").Single();
        return await File.ReadAllLinesAsync(filePath);
    }

    private static void CleanupDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
