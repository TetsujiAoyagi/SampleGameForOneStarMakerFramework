#nullable enable

using System;
using System.Threading.Tasks;
using DebugStudio.App.Core.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// SessionMessageRouter から流れる LogRecord を NDJSON rolling file へ自動永続化する service。
/// </summary>
public sealed class LogPersistenceService : IAsyncDisposable
{
    private readonly SessionMessageRouter _messageRouter;
    private readonly RollingLogFileWriter _writer;

    public LogPersistenceService(SessionMessageRouter messageRouter, RollingLogFileWriter writer)
    {
        _messageRouter = messageRouter ?? throw new ArgumentNullException(nameof(messageRouter));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _messageRouter.LogReceived += OnLogReceived;
    }

    private void OnLogReceived(LogRecord log)
    {
        _writer.Enqueue(LogRecordExportMapper.ToExportRecord(log));
    }

    public async ValueTask DisposeAsync()
    {
        _messageRouter.LogReceived -= OnLogReceived;
        await _writer.DisposeAsync().ConfigureAwait(false);
    }
}
