#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Writers;

/// <summary>
/// 受信 log を NDJSON へ逐次 append し、サイズ上限で rolling する writer。
/// enqueue は非ブロッキングで、実際の file I/O は単一 background reader が担う。
/// </summary>
public sealed class RollingLogFileWriter : IAsyncDisposable
{
    private const long DefaultMaxFileSizeBytes = 10 * 1024 * 1024;
    private const int DefaultMaxGenerations = 10;

    private readonly string _outputDirectory;
    private readonly string _baseName;
    private readonly long _maxFileSizeBytes;
    private readonly int _maxGenerations;
    private readonly Action<string>? _onError;
    private readonly Channel<LogExportRecord> _channel;
    private readonly Task _readerTask;
    private int _disposed;

    public RollingLogFileWriter(
        string outputDirectory,
        string baseName = "debugstudio-logs",
        long maxFileSizeBytes = DefaultMaxFileSizeBytes,
        int maxGenerations = DefaultMaxGenerations,
        Action<string>? onError = null)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
        }

        if (string.IsNullOrWhiteSpace(baseName))
        {
            throw new ArgumentException("A base file name is required.", nameof(baseName));
        }

        if (maxFileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes), "Max file size must be positive.");
        }

        if (maxGenerations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxGenerations), "Max generations must be positive.");
        }

        _outputDirectory = outputDirectory;
        _baseName = baseName;
        _maxFileSizeBytes = maxFileSizeBytes;
        _maxGenerations = maxGenerations;
        _onError = onError;

        Directory.CreateDirectory(_outputDirectory);

        _channel = Channel.CreateUnbounded<LogExportRecord>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        _readerTask = Task.Run(ProcessQueueAsync);
    }

    /// <summary>
    /// 1 件を書き込み queue へ積む。呼び出し元を block しない。
    /// </summary>
    public void Enqueue(LogExportRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (!_channel.Writer.TryWrite(record))
        {
            ReportError("Log persistence channel is closed; record was dropped.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _channel.Writer.TryComplete();

        try
        {
            await _readerTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ReportError($"Log persistence reader failed during shutdown: {ex}");
        }
    }

    private async Task ProcessQueueAsync()
    {
        StreamWriter? writer = null;
        FileStream? stream = null;
        string currentDate = string.Empty;
        int currentSequence = 1;
        long currentSize = 0;

        try
        {
            await foreach (var record in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    if (writer == null || !string.Equals(today, currentDate, StringComparison.Ordinal))
                    {
                        await CloseWriterAsync(writer, stream).ConfigureAwait(false);
                        writer = null;
                        stream = null;
                        currentDate = today;
                        (stream, writer, currentSize, currentSequence) = await OpenWriterForTodayAsync(today).ConfigureAwait(false);
                    }

                    var line = NdjsonLogRecordSerializer.Serialize(record);
                    var lineSize = Encoding.UTF8.GetByteCount(line) + Encoding.UTF8.GetByteCount(Environment.NewLine);

                    if (currentSize > 0 && currentSize + lineSize > _maxFileSizeBytes)
                    {
                        await CloseWriterAsync(writer, stream).ConfigureAwait(false);
                        writer = null;
                        stream = null;
                        currentSequence++;
                        (stream, writer, currentSize) = await CreateWriterAsync(currentDate, currentSequence).ConfigureAwait(false);
                        PruneOldFiles(activeFilePath: BuildFilePath(currentDate, currentSequence));
                    }

                    await writer.WriteLineAsync(line).ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                    currentSize += lineSize;
                }
                catch (Exception ex)
                {
                    ReportError($"Failed to persist log record: {ex}");
                }
            }
        }
        finally
        {
            await CloseWriterAsync(writer, stream).ConfigureAwait(false);
        }
    }

    private async Task<(FileStream Stream, StreamWriter Writer, long Size, int Sequence)> OpenWriterForTodayAsync(string date)
    {
        var highestSequence = FindHighestSequenceForDate(date);
        if (highestSequence <= 0)
        {
            var created = await CreateWriterAsync(date, 1).ConfigureAwait(false);
            return (created.Stream, created.Writer, created.Size, 1);
        }

        var existingPath = BuildFilePath(date, highestSequence);
        var existingSize = File.Exists(existingPath) ? new FileInfo(existingPath).Length : 0L;
        if (existingSize < _maxFileSizeBytes)
        {
            var stream = new FileStream(
                existingPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);
            var writer = new StreamWriter(stream, NdjsonLogRecordSerializer.Utf8WithoutBom);
            return (stream, writer, existingSize, highestSequence);
        }

        var nextSequence = highestSequence + 1;
        var rolled = await CreateWriterAsync(date, nextSequence).ConfigureAwait(false);
        return (rolled.Stream, rolled.Writer, rolled.Size, nextSequence);
    }

    private async Task<(FileStream Stream, StreamWriter Writer, long Size)> CreateWriterAsync(string date, int sequence)
    {
        var path = BuildFilePath(date, sequence);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        var writer = new StreamWriter(stream, NdjsonLogRecordSerializer.Utf8WithoutBom);
        await writer.FlushAsync().ConfigureAwait(false);
        return (stream, writer, 0L);
    }

    private int FindHighestSequenceForDate(string date)
    {
        var searchPattern = $"{_baseName}_{date}_*.ndjson";
        var files = Directory.Exists(_outputDirectory)
            ? Directory.GetFiles(_outputDirectory, searchPattern)
            : Array.Empty<string>();

        var highest = 0;
        foreach (var file in files)
        {
            if (TryParseSequence(Path.GetFileNameWithoutExtension(file), date, out var sequence))
            {
                highest = Math.Max(highest, sequence);
            }
        }

        return highest;
    }

    private bool TryParseSequence(string fileNameWithoutExtension, string date, out int sequence)
    {
        sequence = 0;
        var expectedPrefix = $"{_baseName}_{date}_";
        if (!fileNameWithoutExtension.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = fileNameWithoutExtension[expectedPrefix.Length..];
        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out sequence);
    }

    private string BuildFilePath(string date, int sequence)
    {
        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"{_baseName}_{date}_{sequence:D3}.ndjson");
        return Path.Combine(_outputDirectory, fileName);
    }

    private void PruneOldFiles(string activeFilePath)
    {
        try
        {
            if (!Directory.Exists(_outputDirectory))
            {
                return;
            }

            // LastWriteTime は高速 roll で同一値になり削除順が非決定になるため、
            // ファイル名に埋め込んだ date + sequence を正としてソートする。
            // 現在書き込み中のファイルは Windows では削除できず prune 全体を壊すので候補から外す。
            var candidates = new List<(string Path, string Date, int Sequence)>();
            foreach (var path in Directory.GetFiles(_outputDirectory, $"{_baseName}_*.ndjson"))
            {
                if (string.Equals(path, activeFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryParseDateAndSequence(Path.GetFileNameWithoutExtension(path), out var date, out var sequence))
                {
                    candidates.Add((path, date, sequence));
                }
            }

            // active file も世代数に含めて数えるため、候補側の許容数は 1 減らす。
            var allowedCandidates = _maxGenerations - 1;
            if (candidates.Count <= allowedCandidates)
            {
                return;
            }

            candidates.Sort(static (left, right) =>
            {
                var dateComparison = string.CompareOrdinal(left.Date, right.Date);
                return dateComparison != 0 ? dateComparison : left.Sequence.CompareTo(right.Sequence);
            });

            var deleteCount = candidates.Count - allowedCandidates;
            for (var index = 0; index < deleteCount; index++)
            {
                try
                {
                    File.Delete(candidates[index].Path);
                }
                catch (Exception ex)
                {
                    ReportError($"Failed to delete old log file '{candidates[index].Path}': {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            ReportError($"Failed to prune old log files: {ex}");
        }
    }

    /// <summary>
    /// `{baseName}_{yyyy-MM-dd}_{NNN}` 形式のファイル名から date と sequence を取り出す。
    /// 規約外の名前は prune 対象にしない(手動で置かれたファイルを誤削除しないため)。
    /// </summary>
    private bool TryParseDateAndSequence(string fileNameWithoutExtension, out string date, out int sequence)
    {
        date = string.Empty;
        sequence = 0;

        var expectedPrefix = $"{_baseName}_";
        if (!fileNameWithoutExtension.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remainder = fileNameWithoutExtension[expectedPrefix.Length..];
        var separatorIndex = remainder.LastIndexOf('_');
        if (separatorIndex <= 0)
        {
            return false;
        }

        var datePart = remainder[..separatorIndex];
        var sequencePart = remainder[(separatorIndex + 1)..];
        if (!DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return false;
        }

        if (!int.TryParse(sequencePart, NumberStyles.None, CultureInfo.InvariantCulture, out sequence))
        {
            return false;
        }

        date = datePart;
        return true;
    }

    private static async Task CloseWriterAsync(StreamWriter? writer, FileStream? stream)
    {
        if (writer != null)
        {
            try
            {
                await writer.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to flush log writer: {ex}");
            }

            await writer.DisposeAsync().ConfigureAwait(false);
        }
        else if (stream != null)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void ReportError(string message)
    {
        Debug.WriteLine(message);

        // 通知先 callback の例外が background loop や DisposeAsync へ波及しないよう隔離する。
        try
        {
            _onError?.Invoke(message);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Log persistence error callback threw: {ex}");
        }
    }
}
