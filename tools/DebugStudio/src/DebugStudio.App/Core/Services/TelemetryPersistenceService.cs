#nullable enable

using System;
using System.Threading.Tasks;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Export.Writers;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// <see cref="SessionMessageRouter.TelemetryReceived"/> から流れる telemetry を
/// NDJSON rolling file へ自動永続化する service。
///
/// <para>
/// DebugSocket 受信 callback 内では enqueue のみ行い、ファイル I/O は
/// <see cref="RollingTelemetryFileWriter"/> の単一 background reader に委譲する。
/// これにより高頻度 telemetry でも受信経路を block しない。
/// </para>
/// </summary>
public sealed class TelemetryPersistenceService : IAsyncDisposable
{
    private readonly SessionMessageRouter _messageRouter;
    private readonly RollingTelemetryFileWriter _writer;
    private readonly TelemetrySessionAttributesStore _sessionAttributesStore;

    public TelemetryPersistenceService(
        SessionMessageRouter messageRouter,
        RollingTelemetryFileWriter writer,
        TelemetrySessionAttributesStore sessionAttributesStore)
    {
        _messageRouter = messageRouter ?? throw new ArgumentNullException(nameof(messageRouter));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _sessionAttributesStore = sessionAttributesStore
            ?? throw new ArgumentNullException(nameof(sessionAttributesStore));
        _messageRouter.TelemetryReceived += OnTelemetryReceived;
    }

    /// <summary>
    /// 受信 callback は mapper → enqueue のみ。await や I/O をここへ持ち込まない。
    /// </summary>
    private void OnTelemetryReceived(DebugTelemetryEnvelopeV1 telemetry)
    {
        _writer.Enqueue(TelemetryRecordExportMapper.ToExportRecord(
            telemetry,
            _sessionAttributesStore.TryGet(telemetry.SessionId)));
    }

    /// <summary>
    /// 購読解除後に writer queue を flush し、終了直前の観測データを失わない。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _messageRouter.TelemetryReceived -= OnTelemetryReceived;
        await _writer.DisposeAsync().ConfigureAwait(false);
    }
}
