#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.Export.Tests;

/// <summary>
/// telemetry rolling writer の L0 契約であるサイズ roll と世代保持を検証する。
/// </summary>
public sealed class RollingTelemetryFileWriterTests
{
    [Fact]
    public async Task Enqueue_最大サイズ超過で連番ファイルへrollしrecordを保持する()
    {
        var directory = CreateTempDirectory();
        var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        try
        {
            var firstRecord = CreateRecord("roll-first");
            var secondRecord = CreateRecord("roll-second");
            var writer = new RollingTelemetryFileWriter(directory, maxFileSizeBytes: 200);

            writer.Enqueue(firstRecord);
            writer.Enqueue(secondRecord);
            await writer.DisposeAsync();

            var files = Directory.GetFiles(directory, "debugstudio-telemetry_*.ndjson")
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(
                new[]
                {
                    Path.Combine(directory, $"debugstudio-telemetry_{today}_001.ndjson"),
                    Path.Combine(directory, $"debugstudio-telemetry_{today}_002.ndjson"),
                },
                files);

            var firstLines = await File.ReadAllLinesAsync(files[0]);
            var secondLines = await File.ReadAllLinesAsync(files[1]);
            Assert.Contains("roll-first", Assert.Single(firstLines), StringComparison.Ordinal);
            Assert.Contains("roll-second", Assert.Single(secondLines), StringComparison.Ordinal);
        }
        finally
        {
            CleanupDirectory(directory);
        }
    }

    [Fact]
    public async Task Enqueue_最大世代数を超えると古いtelemetryファイルをpruneする()
    {
        var directory = CreateTempDirectory();
        var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        try
        {
            // 既存世代を起点に、roll ごとに active 以外の最古世代が削除される契約を固定する。
            await File.WriteAllTextAsync(
                Path.Combine(directory, $"debugstudio-telemetry_{today}_001.ndjson"),
                "{\"seed\":1}\n");
            await File.WriteAllTextAsync(
                Path.Combine(directory, $"debugstudio-telemetry_{today}_002.ndjson"),
                "{\"seed\":2}\n");

            var writer = new RollingTelemetryFileWriter(
                directory,
                maxFileSizeBytes: 200,
                maxGenerations: 2);

            writer.Enqueue(CreateRecord("prune-first"));
            writer.Enqueue(CreateRecord("prune-second"));
            writer.Enqueue(CreateRecord("prune-third"));
            await writer.DisposeAsync();

            var fileNames = Directory.GetFiles(directory, "debugstudio-telemetry_*.ndjson")
                .Select(Path.GetFileName)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray();

            // active を含めて 2 世代だけを残し、古い seed 世代を retention 対象に含める。
            Assert.Equal(
                new[]
                {
                    $"debugstudio-telemetry_{today}_004.ndjson",
                    $"debugstudio-telemetry_{today}_005.ndjson",
                },
                fileNames);
        }
        finally
        {
            CleanupDirectory(directory);
        }
    }

    private static TelemetryExportRecord CreateRecord(string name)
    {
        return new TelemetryExportRecord
        {
            TimestampUtc = "2026-07-19T00:00:00.0000000Z",
            TimestampUnixTimeMilliseconds = 1_784_419_200_000,
            Stream = "telemetry",
            Name = name,
            IsSuccess = true,
            ElapsedMs = 12.5,
        };
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void CleanupDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
