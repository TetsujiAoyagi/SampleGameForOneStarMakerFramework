#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.App.Features.LogViewer;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.App.Tests.Features.LogViewer;

/// <summary>
/// LogViewerViewModel の NX3 結線を検証する。
/// category / regex / export 可否が store 側の新しい検索基盤と一致することを確認する。
/// </summary>
public sealed class LogViewerViewModelTests
{
    [Fact]
    public void CategoryFilter_VisibleLogsをカテゴリで絞り込める()
    {
        var viewModel = CreateViewModel(out var store, out _);

        store.Append(CreateLogEnvelope("Network connected", category: "Network"));
        store.Append(CreateLogEnvelope("UI clicked", category: "UI"));
        store.Append(CreateLogEnvelope("Network timeout", category: "Network"));

        var networkOption = Assert.Single(viewModel.CategoryFilters.Where(option => option.Category == "Network"));
        viewModel.SelectedCategoryFilter = networkOption;

        Assert.Equal(2, viewModel.VisibleLogs.Count);
        Assert.All(viewModel.VisibleLogs, log => Assert.Equal("Network", log.Category));
        Assert.Contains("2 visible", viewModel.FilterSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidRegex_ValidationErrorを表示して既存結果を保持する()
    {
        var viewModel = CreateViewModel(out var store, out _);

        store.Append(CreateLogEnvelope("Device Connected"));
        store.Append(CreateLogEnvelope("Device disconnected"));

        Assert.Equal(2, viewModel.VisibleLogs.Count);

        viewModel.UseRegex = true;
        viewModel.QueryText = "[";

        Assert.True(viewModel.HasValidationError);
        Assert.Contains("正規表現", viewModel.ValidationError, StringComparison.Ordinal);
        Assert.Equal(2, viewModel.VisibleLogs.Count);
    }

    [Fact]
    public void ExportCommand_可視ログがない場合は無効になる()
    {
        var viewModel = CreateViewModel(out var store, out _);

        store.Append(CreateLogEnvelope("Only one entry"));
        Assert.True(viewModel.ExportCommand.CanExecute(null));

        viewModel.QueryText = "missing";
        Assert.False(viewModel.ExportCommand.CanExecute(null));

        viewModel.UseRegex = true;
        viewModel.QueryText = "[";
        Assert.False(viewModel.ExportCommand.CanExecute(null));
    }

    [Fact]
    public void ExportFormat_変更時にパス拡張子とボタン文言が切り替わる()
    {
        var viewModel = CreateViewModel(out _, out _);

        var csvOption = Assert.Single(viewModel.ExportFormats.Where(option => option.Format == LogExportFormat.Csv));
        viewModel.SelectedExportFormat = csvOption;

        Assert.EndsWith(".csv", viewModel.ExportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Export CSV", viewModel.ExportButtonText);
        Assert.Equal("CSV export is ready.", viewModel.ExportStatus);
    }

    [Fact]
    public void ExportFormat_ElasticBulk選択時にbulk拡張子と文言へ切り替わる()
    {
        var viewModel = CreateViewModel(out _, out _);

        var bulkOption = Assert.Single(viewModel.ExportFormats.Where(option => option.Format == LogExportFormat.ElasticBulk));
        viewModel.SelectedExportFormat = bulkOption;

        Assert.EndsWith(".bulk.ndjson", viewModel.ExportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Export Elastic Bulk", viewModel.ExportButtonText);
        Assert.Equal("Elastic Bulk export is ready.", viewModel.ExportStatus);
    }

    [Fact]
    public void LayoutDefaults_CompactとDetailPaneが既定で有効()
    {
        var viewModel = CreateViewModel(out _, out _);

        Assert.True(viewModel.IsCompactDensityEnabled);
        Assert.True(viewModel.IsDetailPaneVisible);
        Assert.Equal(320, viewModel.DetailPaneColumnWidth.Value);
        Assert.Equal(5, viewModel.DetailSplitterColumnWidth.Value);
        Assert.Equal(240, viewModel.DetailPaneMinWidth);
    }

    [Fact]
    public void ToggleDetailPane_非表示時は列幅をゼロにする()
    {
        var viewModel = CreateViewModel(out _, out _);

        viewModel.ToggleDetailPaneCommand.Execute(null);

        Assert.False(viewModel.IsDetailPaneVisible);
        Assert.Equal("Show Detail", viewModel.DetailToggleButtonText);
        Assert.Equal(0, viewModel.DetailPaneColumnWidth.Value);
        Assert.Equal(0, viewModel.DetailSplitterColumnWidth.Value);
        Assert.Equal(0, viewModel.DetailPaneMinWidth);

        viewModel.ToggleDetailPaneCommand.Execute(null);

        Assert.True(viewModel.IsDetailPaneVisible);
        Assert.Equal("Hide Detail", viewModel.DetailToggleButtonText);
        Assert.Equal(320, viewModel.DetailPaneColumnWidth.Value);
    }

    [Fact]
    public async Task EndToEnd_FilterSearchExportWorkflow_表示条件と同じ結果をCSVへ流せる()
    {
        var viewModel = CreateViewModel(out var store, out var writers);

        store.Append(CreateLogEnvelope("Network timeout", category: "Network"));
        store.Append(CreateLogEnvelope("UI timeout", category: "UI"));
        store.Append(CreateLogEnvelope("Network ok", category: "Network"));

        var networkOption = Assert.Single(viewModel.CategoryFilters.Where(option => option.Category == "Network"));
        var csvOption = Assert.Single(viewModel.ExportFormats.Where(option => option.Format == LogExportFormat.Csv));

        viewModel.SelectedCategoryFilter = networkOption;
        viewModel.QueryText = "timeout";
        viewModel.SelectedExportFormat = csvOption;

        Assert.Single(viewModel.VisibleLogs);
        Assert.Equal("Network timeout", viewModel.VisibleLogs[0].Message);

        viewModel.ExportCommand.Execute(null);
        await Task.Delay(50);

        Assert.Empty(writers.NdjsonWriter.LastLogs);
        Assert.Single(writers.CsvWriter.LastLogs);
        Assert.Equal("Network timeout", writers.CsvWriter.LastLogs[0].Message);
        Assert.Contains("Exported CSV", viewModel.ExportStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void VisibleLogs_時系列順で古いものが先頭になり最新が末尾になる()
    {
        var viewModel = CreateViewModel(out var store, out _);

        store.Append(CreateLogEnvelope("first"));
        store.Append(CreateLogEnvelope("second"));
        store.Append(CreateLogEnvelope("third"));

        Assert.Equal(3, viewModel.VisibleLogs.Count);
        Assert.Equal("first", viewModel.VisibleLogs[0].Message);
        Assert.Equal("second", viewModel.VisibleLogs[1].Message);
        Assert.Equal("third", viewModel.VisibleLogs[2].Message);
        Assert.Equal("third", viewModel.SelectedLog?.Message);
    }

    [Fact]
    public void AutoScroll有効時は新規ログで最新行を選択する()
    {
        var viewModel = CreateViewModel(out var store, out _);
        Assert.True(viewModel.IsAutoScrollEnabled);

        store.Append(CreateLogEnvelope("older"));
        store.Append(CreateLogEnvelope("newer"));

        Assert.Equal("newer", viewModel.SelectedLog?.Message);

        viewModel.IsAutoScrollEnabled = false;
        viewModel.SelectedLog = viewModel.VisibleLogs[0];
        store.Append(CreateLogEnvelope("latest-while-paused"));

        Assert.Equal("older", viewModel.SelectedLog?.Message);
    }

    [Fact]
    public void SelectedLog変更時にMessageとExceptionのPropertyChangedが飛ぶ()
    {
        var viewModel = CreateViewModel(out var store, out _);
        store.Append(CreateLogEnvelope("plain"));
        store.Append(new LogEnvelopeV1
        {
            SchemaVersion = 1,
            ApplicationName = "TestApp",
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Category = "Default",
            LogLevel = 4,
            EventId = 1,
            Message = "boom",
            Exception = "System.TimeoutException: timed out\n  at Net.Receive()",
            ThreadId = Environment.CurrentManagedThreadId,
        });

        viewModel.IsAutoScrollEnabled = false;
        var notifications = new HashSet<string>(StringComparer.Ordinal);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
            {
                notifications.Add(e.PropertyName);
            }
        };

        viewModel.SelectedLog = viewModel.VisibleLogs[1];

        Assert.Contains(nameof(LogViewerViewModel.SelectedLog), notifications);
        Assert.Contains(nameof(LogViewerViewModel.SelectedLogMessage), notifications);
        Assert.Contains(nameof(LogViewerViewModel.SelectedLogException), notifications);
        Assert.Contains(nameof(LogViewerViewModel.HasSelectedLogException), notifications);
        Assert.Equal("boom", viewModel.SelectedLogMessage);
        Assert.Contains("TimeoutException", viewModel.SelectedLogException, StringComparison.Ordinal);
        Assert.True(viewModel.HasSelectedLogException);

        notifications.Clear();
        viewModel.SelectedLog = viewModel.VisibleLogs[0];

        Assert.Equal("plain", viewModel.SelectedLogMessage);
        Assert.False(viewModel.HasSelectedLogException);
        Assert.Equal(string.Empty, viewModel.SelectedLogException);
        Assert.Contains(nameof(LogViewerViewModel.HasSelectedLogException), notifications);
    }

    [Fact]
    public void ToggleAutoScroll_Tail文言のPropertyChangedが飛ぶ()
    {
        var viewModel = CreateViewModel(out _, out _);
        var notifications = new HashSet<string>(StringComparer.Ordinal);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
            {
                notifications.Add(e.PropertyName);
            }
        };

        viewModel.ToggleAutoScrollCommand.Execute(null);

        Assert.False(viewModel.IsAutoScrollEnabled);
        Assert.Equal("Resume", viewModel.TailToggleButtonText);
        Assert.Contains(nameof(LogViewerViewModel.IsAutoScrollEnabled), notifications);
        Assert.Contains(nameof(LogViewerViewModel.TailToggleButtonText), notifications);
        Assert.Contains(nameof(LogViewerViewModel.TailStateText), notifications);
    }

    [Fact]
    public void SelectedLog_V1ScalarPayloadをStructuredFieldsへ正規化する()
    {
        var viewModel = CreateViewModel(out var store, out _);
        store.Append(new LogEnvelopeV1
        {
            SchemaVersion = 3,
            ApplicationName = "GameClient",
            TimestampUnixTimeMilliseconds = 123456789,
            Category = "Network.Sync",
            LogLevel = 3,
            EventId = 42,
            EventName = "PacketTimeout",
            Message = "sync timeout",
            Exception = "TimeoutException",
            ThreadId = 9,
            ThreadName = "MainThread",
            MemberName = "ReceiveLoop",
            FilePath = "C:\\src\\Net.cs",
            LineNumber = 88,
        });

        viewModel.SelectedLog = Assert.Single(viewModel.VisibleLogs);

        Assert.True(viewModel.HasStructuredFields);
        Assert.True(viewModel.HasSelectedLogException);
        Assert.Equal("TimeoutException", viewModel.SelectedLogException);
        Assert.Contains("field path", viewModel.StructuredPayloadStatus, StringComparison.Ordinal);
        Assert.Contains(viewModel.StructuredFields, field => field.Label == "schema.version" && field.Value == "3");
        Assert.Contains(viewModel.StructuredFields, field => field.Label == "event.name" && field.Value == "PacketTimeout");
        Assert.Contains(viewModel.StructuredFields, field => field.Label == "payload.exception" && field.Value == "TimeoutException");
        Assert.Contains(viewModel.StructuredFields, field => field.Label == "source.file" && field.Value == "C:\\src\\Net.cs");
    }

    [Fact]
    public void SelectedLog_OptionalFieldが空ならStructuredFieldsへ出さない()
    {
        var viewModel = CreateViewModel(out var store, out _);
        store.Append(new LogEnvelopeV1
        {
            SchemaVersion = 1,
            ApplicationName = "GameClient",
            TimestampUnixTimeMilliseconds = 111,
            Category = "Gameplay",
            LogLevel = 2,
            EventId = 7,
            Message = "ready",
            ThreadId = 3,
            EventName = "",
            ThreadName = "",
            MemberName = "",
            FilePath = "",
            Exception = "",
            LineNumber = 0,
        });

        viewModel.SelectedLog = Assert.Single(viewModel.VisibleLogs);

        Assert.True(viewModel.HasStructuredFields);
        Assert.Equal(9, viewModel.StructuredFields.Count);
        Assert.DoesNotContain(viewModel.StructuredFields, field => field.Label == "event.name");
        Assert.DoesNotContain(viewModel.StructuredFields, field => field.Label == "thread.name");
        Assert.DoesNotContain(viewModel.StructuredFields, field => field.Label == "source.member");
        Assert.DoesNotContain(viewModel.StructuredFields, field => field.Label == "source.file");
        Assert.DoesNotContain(viewModel.StructuredFields, field => field.Label == "payload.exception");
    }

    [Fact]
    public void SelectedLogを解除するとStructuredStateも初期表示へ戻る()
    {
        var viewModel = CreateViewModel(out var store, out _);
        store.Append(CreateLogEnvelope("first"));

        viewModel.SelectedLog = Assert.Single(viewModel.VisibleLogs);
        Assert.True(viewModel.HasStructuredFields);

        viewModel.SelectedLog = null;

        Assert.False(viewModel.HasStructuredFields);
        Assert.False(viewModel.HasSelectedLogException);
        Assert.Empty(viewModel.StructuredFields);
        Assert.Equal("No row selected.", viewModel.SelectedLogTitle);
        Assert.Equal(string.Empty, viewModel.SelectedLogException);
        Assert.Contains("scalar payload", viewModel.StructuredPayloadStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void 選択カテゴリがretentionから消えたらAllへ戻り可視ログを空にしない()
    {
        var viewModel = CreateViewModel(out var store, out _, capacity: 2);

        store.Append(CreateLogEnvelope("keep-me", category: "Keep"));
        store.Append(CreateLogEnvelope("evict-me", category: "Evict"));

        var evictOption = Assert.Single(viewModel.CategoryFilters.Where(option => option.Category == "Evict"));
        viewModel.SelectedCategoryFilter = evictOption;
        Assert.Single(viewModel.VisibleLogs);
        Assert.Equal("evict-me", viewModel.VisibleLogs[0].Message);

        // capacity=2 のため Keep が落ち、続けて New を入れると Evict も落ちる
        store.Append(CreateLogEnvelope("new-1", category: "New"));
        store.Append(CreateLogEnvelope("new-2", category: "New"));

        Assert.Null(viewModel.SelectedCategoryFilter.Category);
        Assert.Equal(2, viewModel.VisibleLogs.Count);
        Assert.All(viewModel.VisibleLogs, log => Assert.Equal("New", log.Category));
        Assert.DoesNotContain(viewModel.CategoryFilters, option => option.Category == "Evict");
    }

    private static LogViewerViewModel CreateViewModel(out LogStore store, out RecordingExportWriters writers, int capacity = 128)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        store = new LogStore(capacity: capacity);
        writers = new RecordingExportWriters();
        var queryService = new LogQueryService();
        var exportService = new LogExportService(store, queryService, new ILogExportWriter[] { writers.NdjsonWriter, writers.CsvWriter, writers.BulkWriter });
        return new LogViewerViewModel(dispatcher, store, queryService, exportService);
    }

    private static LogEnvelopeV1 CreateLogEnvelope(string message, string category = "Default", int level = 2)
    {
        return new LogEnvelopeV1
        {
            SchemaVersion = 1,
            ApplicationName = "TestApp",
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Category = category,
            LogLevel = level,
            EventId = 0,
            Message = message,
            ThreadId = Environment.CurrentManagedThreadId,
        };
    }

    private sealed class RecordingLogExportWriter : ILogExportWriter
    {
        public LogExportFormat Format => LogExportFormat.Ndjson;

        public IReadOnlyList<LogExportRecord> LastLogs { get; private set; } = Array.Empty<LogExportRecord>();

        public Task WriteAsync(IReadOnlyList<LogExportRecord> logs, string outputPath, CancellationToken cancellationToken = default)
        {
            LastLogs = logs;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCsvExportWriter : ILogExportWriter
    {
        public LogExportFormat Format => LogExportFormat.Csv;

        public IReadOnlyList<LogExportRecord> LastLogs { get; private set; } = Array.Empty<LogExportRecord>();

        public Task WriteAsync(IReadOnlyList<LogExportRecord> logs, string outputPath, CancellationToken cancellationToken = default)
        {
            LastLogs = logs;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingBulkExportWriter : ILogExportWriter
    {
        public LogExportFormat Format => LogExportFormat.ElasticBulk;

        public IReadOnlyList<LogExportRecord> LastLogs { get; private set; } = Array.Empty<LogExportRecord>();

        public Task WriteAsync(IReadOnlyList<LogExportRecord> logs, string outputPath, CancellationToken cancellationToken = default)
        {
            LastLogs = logs;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingExportWriters
    {
        public RecordingLogExportWriter NdjsonWriter { get; } = new();

        public RecordingCsvExportWriter CsvWriter { get; } = new();

        public RecordingBulkExportWriter BulkWriter { get; } = new();
    }
}
