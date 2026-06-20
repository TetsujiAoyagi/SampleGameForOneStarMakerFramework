#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.App.Features.Telemetry;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.App.Tests.Features.Telemetry;

/// <summary>
/// TelemetryWindowViewModel の R2 拡張を検証する。
/// 直近履歴が store と同じ順序で panel へ出ること、および capability 状態に応じた文言を固定する。
/// </summary>
public sealed class TelemetryWindowViewModelTests
{
    [Fact]
    public void RecentHistory_最新順でTelemetryとServiceStatusを表示する()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var telemetryStore = new TelemetryStore(historyCapacity: 3);
        var capabilityHandshakeService = new CapabilityHandshakeService();
        var capabilityStateStore = new CapabilityStateStore(capabilityHandshakeService.LocalSupportedCapabilities);
        capabilityStateStore.ApplyWelcome(new CapabilityHandshakeWelcomeEnvelopeV1
        {
            ServerName = "Unity",
            SessionId = "session-1",
            SelectedSchemaVersion = 1,
            ServerCapabilities = DebugStudioCapability.TelemetryStream | DebugStudioCapability.ServiceStatusStream,
            NegotiatedCapabilities = DebugStudioCapability.TelemetryStream | DebugStudioCapability.ServiceStatusStream,
        });

        var viewModel = new TelemetryWindowViewModel(dispatcher, telemetryStore, capabilityStateStore);

        telemetryStore.AppendTelemetry(CreateTelemetry("boot", 10));
        telemetryStore.AppendTelemetry(CreateTelemetry("tick", 20));
        telemetryStore.AppendTelemetry(CreateTelemetry("tick", 30));
        telemetryStore.AppendTelemetry(CreateTelemetry("flush", 40, tagBits: 1 << 6));

        telemetryStore.AppendServiceStatus(CreateServiceStatus("starting", "boot"));
        telemetryStore.AppendServiceStatus(CreateServiceStatus("running", "warm"));
        telemetryStore.AppendServiceStatus(CreateServiceStatus("running", "steady"));

        Assert.Equal(4, viewModel.TelemetryCount);
        Assert.Equal(3, viewModel.ServiceStatusCount);
        Assert.Equal(3, viewModel.RecentTelemetry.Count);
        Assert.Equal(3, viewModel.RecentServiceStatuses.Count);
        Assert.Contains("flush", viewModel.RecentTelemetry[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AllocSpike", viewModel.RecentTelemetry[0], StringComparison.Ordinal);
        Assert.Contains("tick", viewModel.RecentTelemetry[1], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(viewModel.RecentTelemetry, item => item.Contains("boot", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("steady", viewModel.RecentServiceStatuses[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Telemetry frames are arriving.", viewModel.TelemetryStatus, StringComparison.Ordinal);
        Assert.Contains("Service status frames are arriving.", viewModel.ServiceStatusState, StringComparison.Ordinal);
    }

    [Fact]
    public void Capability未対応時_待機文言ではなくunsupported文言を出す()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var telemetryStore = new TelemetryStore();
        var capabilityHandshakeService = new CapabilityHandshakeService();
        var capabilityStateStore = new CapabilityStateStore(capabilityHandshakeService.LocalSupportedCapabilities);
        capabilityStateStore.ApplyWelcome(new CapabilityHandshakeWelcomeEnvelopeV1
        {
            ServerName = "Unity",
            SessionId = "session-1",
            SelectedSchemaVersion = 1,
            ServerCapabilities = DebugStudioCapability.None,
            NegotiatedCapabilities = DebugStudioCapability.None,
        });

        var viewModel = new TelemetryWindowViewModel(dispatcher, telemetryStore, capabilityStateStore);

        Assert.Contains("telemetry stream support", viewModel.TelemetryStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("service status stream support", viewModel.ServiceStatusState, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(viewModel.RecentTelemetry);
        Assert.Empty(viewModel.RecentServiceStatuses);
    }

    [Fact]
    public async Task ExportCommand_保持済みtelemetryをNDJSONエクスポートへ流せる()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var telemetryStore = new TelemetryStore();
        telemetryStore.AppendTelemetry(CreateTelemetry("boot", 10));

        var capabilityHandshakeService = new CapabilityHandshakeService();
        var capabilityStateStore = new CapabilityStateStore(capabilityHandshakeService.LocalSupportedCapabilities);
        var writer = new RecordingTelemetryExportWriter();
        var exportService = new TelemetryExportService(telemetryStore, writer);
        var viewModel = new TelemetryWindowViewModel(
            dispatcher,
            telemetryStore,
            capabilityStateStore,
            exportService,
            new TelemetryExportPathPolicy(@"C:\TelemetryRoot"));

        Assert.True(viewModel.ExportCommand.CanExecute(null));

        viewModel.ExportCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal(viewModel.ExportPath, writer.LastOutputPath);
        Assert.Single(writer.LastRecords);
        Assert.Equal("telemetry", writer.LastRecords[0].Stream);
        Assert.Contains("Exported telemetry NDJSON", viewModel.ExportStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportFormatをElasticBulkへ切り替えると拡張子と文言が追従する()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var telemetryStore = new TelemetryStore();
        telemetryStore.AppendTelemetry(CreateTelemetry("boot", 10));

        var capabilityHandshakeService = new CapabilityHandshakeService();
        var capabilityStateStore = new CapabilityStateStore(capabilityHandshakeService.LocalSupportedCapabilities);
        var ndjsonWriter = new RecordingTelemetryExportWriter(TelemetryExportFormat.Ndjson);
        var bulkWriter = new RecordingTelemetryExportWriter(TelemetryExportFormat.ElasticBulk);
        var exportService = new TelemetryExportService(telemetryStore, new ITelemetryExportWriter[] { ndjsonWriter, bulkWriter });
        var viewModel = new TelemetryWindowViewModel(
            dispatcher,
            telemetryStore,
            capabilityStateStore,
            exportService,
            new TelemetryExportPathPolicy(@"C:\TelemetryRoot"));

        viewModel.SelectedExportFormat = viewModel.ExportFormats[1];
        viewModel.ExportCommand.Execute(null);
        await Task.Delay(50);

        Assert.EndsWith(".bulk.ndjson", viewModel.ExportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Export Telemetry Elastic Bulk", viewModel.ExportButtonLabel);
        Assert.Equal(viewModel.ExportPath, bulkWriter.LastOutputPath);
        Assert.Single(bulkWriter.LastRecords);
        Assert.Empty(ndjsonWriter.LastRecords);
        Assert.Contains("Exported telemetry Elastic bulk", viewModel.ExportStatus, StringComparison.Ordinal);
    }

    private static DebugTelemetryEnvelopeV1 CreateTelemetry(string name, double elapsedMs, int? tagBits = null)
    {
        return new DebugTelemetryEnvelopeV1
        {
            Name = name,
            ElapsedMs = elapsedMs,
            IsSuccess = true,
            TraceId = DateTime.UtcNow.Ticks,
            SpanId = DateTime.UtcNow.Ticks + 1,
            EndTimestampUtcTicks = DateTime.UtcNow.Ticks,
            TagBits = tagBits,
        };
    }

    private static DebugSocketServiceStatusEnvelopeV1 CreateServiceStatus(string status, string message)
    {
        return new DebugSocketServiceStatusEnvelopeV1
        {
            Status = status,
            Message = message,
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }

    private sealed class RecordingTelemetryExportWriter : ITelemetryExportWriter
    {
        public RecordingTelemetryExportWriter(TelemetryExportFormat format = TelemetryExportFormat.Ndjson)
        {
            Format = format;
        }

        public TelemetryExportFormat Format { get; }

        public string LastOutputPath { get; private set; } = string.Empty;

        public IReadOnlyList<TelemetryExportRecord> LastRecords { get; private set; } = Array.Empty<TelemetryExportRecord>();

        public Task WriteAsync(IReadOnlyList<TelemetryExportRecord> records, string outputPath, CancellationToken cancellationToken = default)
        {
            LastOutputPath = outputPath;
            LastRecords = records;
            return Task.CompletedTask;
        }
    }
}
