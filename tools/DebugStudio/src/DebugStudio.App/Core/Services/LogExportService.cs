#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Stores;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// retain 済み log を query 条件つきで export する app service。
///
/// <para>
/// 「どの log を export するか」は app ルール、
/// 「どう書き出すか」は infrastructure 実装という分担にしている。
/// これで WPF command から直接 file I/O を叩かずに済む。
/// </para>
/// </summary>
public sealed class LogExportService
{
    private readonly LogStore _logStore;
    private readonly LogQueryService _queryService;
    private readonly IReadOnlyDictionary<LogExportFormat, ILogExportWriter> _writers;

    public LogExportService(LogStore logStore, LogQueryService queryService, ILogExportWriter writer)
        : this(logStore, queryService, [writer])
    {
    }

    public LogExportService(LogStore logStore, LogQueryService queryService, IEnumerable<ILogExportWriter> writers)
    {
        _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));

        ArgumentNullException.ThrowIfNull(writers);
        _writers = writers.ToDictionary(static writer => writer.Format);
        if (_writers.Count == 0)
        {
            throw new ArgumentException("At least one export writer is required.", nameof(writers));
        }
    }

    public Task ExportAsync(string outputPath, LogQueryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var logs = _queryService.Query(_logStore, options).Select(ToExportRecord).ToArray();
        return ResolveWriter(LogExportFormat.Ndjson).WriteAsync(logs, outputPath, cancellationToken);
    }

    /// <summary>
    /// 現在の filter criteria に一致するログを既定形式で export する。
    /// 既定形式は互換性維持のため NDJSON とする。
    /// </summary>
    public Task ExportAsync(string outputPath, LogFilterCriteria criteria, CancellationToken cancellationToken = default)
    {
        return ExportAsync(outputPath, criteria, LogExportFormat.Ndjson, cancellationToken);
    }

    /// <summary>
    /// 現在の filter criteria に一致するログを指定形式で export する。
    /// 表示中のログと同じ条件を使うことで、UI と export の結果差異を防ぐ。
    /// </summary>
    public Task ExportAsync(string outputPath, LogFilterCriteria criteria, LogExportFormat format, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var logs = _logStore.QueryLogs(criteria).Matches.Select(ToExportRecord).ToArray();
        return ResolveWriter(format).WriteAsync(logs, outputPath, cancellationToken);
    }

    private ILogExportWriter ResolveWriter(LogExportFormat format)
    {
        if (_writers.TryGetValue(format, out var writer))
        {
            return writer;
        }

        throw new InvalidOperationException($"Export writer for format '{format}' is not registered.");
    }

    private static LogExportRecord ToExportRecord(LogRecord log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return new LogExportRecord
        {
            TimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(log.TimestampUnixTimeMilliseconds).UtcDateTime.ToString("O"),
            SequenceNumber = log.SequenceNumber,
            ApplicationName = log.ApplicationName,
            TimestampUnixTimeMilliseconds = log.TimestampUnixTimeMilliseconds,
            TimestampLocal = log.TimestampText,
            Kind = log.Kind.ToString(),
            RawLogLevel = log.RawLogLevel,
            Category = log.Category,
            EventId = log.EventId,
            EventName = log.EventName,
            Message = log.Message,
            Exception = log.Exception,
            ThreadId = log.ThreadId,
            ThreadName = log.ThreadName,
            MemberName = log.MemberName,
            FilePath = log.FilePath,
            LineNumber = log.LineNumber,
            ServiceName = log.ApplicationName,
            LogLevel = log.Kind switch
            {
                DebugStudio.Contracts.Schema.LogEntryKind.Trace => "trace",
                DebugStudio.Contracts.Schema.LogEntryKind.Debug => "debug",
                DebugStudio.Contracts.Schema.LogEntryKind.Information => "info",
                DebugStudio.Contracts.Schema.LogEntryKind.Warning => "warning",
                DebugStudio.Contracts.Schema.LogEntryKind.Error => "error",
                DebugStudio.Contracts.Schema.LogEntryKind.Critical => "critical",
                DebugStudio.Contracts.Schema.LogEntryKind.None => "none",
                _ => "unknown",
            }
        };
    }
}
