#nullable enable

using System;
using DebugStudio.App.Core.Models;
using DebugStudio.Contracts.Schema;
using DebugStudio.Export.Models;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// app 内 LogRecord を export/persistence 共通 contract へ写す mapper。
/// 手動 export と自動 persistence で同じ field mapping を共有するための正本。
/// </summary>
internal static class LogRecordExportMapper
{
    internal static LogExportRecord ToExportRecord(LogRecord log)
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
                LogEntryKind.Trace => "trace",
                LogEntryKind.Debug => "debug",
                LogEntryKind.Information => "info",
                LogEntryKind.Warning => "warning",
                LogEntryKind.Error => "error",
                LogEntryKind.Critical => "critical",
                LogEntryKind.None => "none",
                _ => "unknown",
            }
        };
    }
}
