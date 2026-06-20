#nullable enable


using DebugStudio.App.Core.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Schema;
using DebugStudio.Export.Models;

namespace DebugStudio.App.Features.LogViewer;

/// <summary>
/// log 表示専用の ViewModel。
///
/// <para>
/// MainWindowViewModel から log の保持・検索・export を切り離し、
/// shell 側は「接続と画面全体の調停」に集中させる。
/// ここは store/query/export を束ねる表示境界であり、socket event 自体は直接扱わない。
/// </para>
/// </summary>
public sealed class LogViewerViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly LogStore _logStore;
    private readonly LogQueryService _queryService;
    private readonly LogExportService _logExportService;
    private readonly LogFilterState _filterState;
    private readonly LogSelectionState _selectionState;
    private readonly LogExportState _exportState;
    private readonly LogTailState _tailState;
    private string _latestSummary = "No log frames yet.";
    private string _filterSummary = "0 retained / 0 total";
    private string _validationError = string.Empty;
    private int _retainedCount;
    private long _totalReceived;
    private long _queryElapsedMilliseconds;
    private bool _isCompactDensityEnabled;

    public LogViewerViewModel(
        Dispatcher dispatcher,
        LogStore logStore,
        LogQueryService queryService,
        LogExportService logExportService)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _logExportService = logExportService ?? throw new ArgumentNullException(nameof(logExportService));

        _filterState = new LogFilterState();
        _selectionState = new LogSelectionState();
        _exportState = new LogExportState();
        _tailState = new LogTailState();

        VisibleLogs = new ObservableCollection<LogViewerListItemViewModel>();
        ExportCommand = new AsyncRelayCommand(ExportAsync, CanExport);
        ClearQueryCommand = new RelayCommand(() => _filterState.ClearQuery(), () => _filterState.CanClearQuery());
        ToggleAutoScrollCommand = new RelayCommand(() => _tailState.ToggleAutoScroll());

        _filterState.FilterChanged += (_, _) =>
        {
            ClearQueryCommand.RaiseCanExecuteChanged();
            RefreshFromStore();
            ExportCommand.RaiseCanExecuteChanged();
        };
        _exportState.ExportPathChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ExportPath));
            ExportCommand.RaiseCanExecuteChanged();
        };
        _exportState.ExportFormatChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedExportFormat));
            OnPropertyChanged(nameof(ExportStatus));
            OnPropertyChanged(nameof(ExportButtonText));
        };
        _tailState.AutoScrollChanged += (_, _) =>
        {
            if (_tailState.IsAutoScrollEnabled)
            {
                SnapSelectionToLatest();
            }
        };

        _logStore.Changed += OnLogStoreChanged;
        RefreshFromStore();
    }

    public ObservableCollection<LogViewerListItemViewModel> VisibleLogs { get; }

    public ObservableCollection<LogDetailFieldViewModel> DetailFields => _selectionState.DetailFields;

    public ObservableCollection<LogDetailFieldViewModel> StructuredFields => _selectionState.StructuredFields;

    public AsyncRelayCommand ExportCommand { get; }

    public RelayCommand ClearQueryCommand { get; }

    public RelayCommand ToggleAutoScrollCommand { get; }

    public IReadOnlyList<LogKindFilterOption> KindFilters => _filterState.KindFilters;

    public ObservableCollection<LogCategoryFilterOption> CategoryFilters => _filterState.CategoryFilters;

    public IReadOnlyList<LogExportFormatOption> ExportFormats => _exportState.ExportFormats;

    public string QueryText
    {
        get => _filterState.QueryText;
        set => _filterState.QueryText = value;
    }

    public LogKindFilterOption SelectedKindFilter
    {
        get => _filterState.SelectedKindFilter;
        set => _filterState.SelectedKindFilter = value;
    }

    public LogCategoryFilterOption SelectedCategoryFilter
    {
        get => _filterState.SelectedCategoryFilter;
        set => _filterState.SelectedCategoryFilter = value;
    }

    public bool UseRegex
    {
        get => _filterState.UseRegex;
        set => _filterState.UseRegex = value;
    }

    public string ExportPath
    {
        get => _exportState.ExportPath;
        set => _exportState.ExportPath = value;
    }

    public LogExportFormatOption SelectedExportFormat
    {
        get => _exportState.SelectedExportFormat;
        set
        {
            _exportState.SelectedExportFormat = value;
            OnPropertyChanged(nameof(SelectedExportFormat));
            OnPropertyChanged(nameof(ExportButtonText));
        }
    }

    public string ExportStatus
    {
        get => _exportState.ExportStatus;
        private set
        {
            _exportState.ExportStatus = value;
            OnPropertyChanged(nameof(ExportStatus));
        }
    }

    public string LatestSummary
    {
        get => _latestSummary;
        private set => SetProperty(ref _latestSummary, value);
    }

    public string FilterSummary
    {
        get => _filterSummary;
        private set => SetProperty(ref _filterSummary, value);
    }

    public string ValidationError
    {
        get => _validationError;
        private set
        {
            if (SetProperty(ref _validationError, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
                OnPropertyChanged(nameof(EmptyStateText));
            }
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

    public string TailStateText => _tailState.TailStateText;

    public string TailToggleButtonText => _tailState.TailToggleButtonText;

    public int RetainedCount
    {
        get => _retainedCount;
        private set => SetProperty(ref _retainedCount, value);
    }

    public long TotalReceived
    {
        get => _totalReceived;
        private set => SetProperty(ref _totalReceived, value);
    }

    public long QueryElapsedMilliseconds
    {
        get => _queryElapsedMilliseconds;
        private set => SetProperty(ref _queryElapsedMilliseconds, value);
    }

    public bool IsAutoScrollEnabled
    {
        get => _tailState.IsAutoScrollEnabled;
        set => _tailState.IsAutoScrollEnabled = value;
    }

    public bool IsCompactDensityEnabled
    {
        get => _isCompactDensityEnabled;
        set => SetProperty(ref _isCompactDensityEnabled, value);
    }

    public LogViewerListItemViewModel? SelectedLog
    {
        get => _selectionState.SelectedLog;
        set => _selectionState.SelectedLog = value;
    }

    public string SelectedLogTitle => _selectionState.SelectedLogTitle;

    public string SelectedLogSummary => _selectionState.SelectedLogSummary;

    public string SelectedLogMessage => _selectionState.SelectedLogMessage;

    public string SelectedLogException => _selectionState.SelectedLogException;

    public string DetailEmptyState => _selectionState.DetailEmptyState;

    public string StructuredPayloadStatus => _selectionState.StructuredPayloadStatus;

    public bool HasStructuredFields => _selectionState.HasStructuredFields;

    public bool HasVisibleLogs => VisibleLogs.Count > 0;

    public string ExportButtonText => SelectedExportFormat.Format switch
    {
        LogExportFormat.Csv => "Export CSV",
        LogExportFormat.ElasticBulk => "Export Elastic Bulk",
        _ => "Export NDJSON",
    };

    public string EmptyStateText
    {
        get
        {
            if (HasValidationError)
            {
                return ValidationError;
            }

            if (RetainedCount == 0)
            {
                return "No log frames yet.";
            }

            return HasVisibleLogs ? string.Empty : "No logs matched the current filter.";
        }
    }

    public void Dispose()
    {
        _logStore.Changed -= OnLogStoreChanged;
    }

    private async Task ExportAsync()
    {
        try
        {
            await _logExportService.ExportAsync(
                ExportPath,
                _filterState.CreateFilterCriteria(),
                SelectedExportFormat.Format).ConfigureAwait(false);
            UpdateOnUiThread(() =>
            {
                ExportStatus = $"Exported {SelectedExportFormat.Label} to {ExportPath}";
            });
        }
        catch (Exception ex)
        {
            UpdateOnUiThread(() =>
            {
                ExportStatus = ex.Message;
            });
        }
    }

    private bool CanExport()
    {
        return _exportState.CanExport() && !HasValidationError && VisibleLogs.Count > 0;
    }

    private void OnLogStoreChanged(LogStoreSnapshot snapshot)
    {
        RefreshFromStore(snapshot);
    }

    private void RefreshFromStore()
    {
        var snapshot = _logStore.GetSnapshotState();
        RefreshFromStore(snapshot);
    }

    /// <summary>
    /// store snapshot を UI 表示用 collection へ写す。
    /// ここで ObservableCollection を更新するため allocation は発生するが、
    /// それは WPF 境界でのみ許容し、raw retention 自体は ring buffer で抑える。
    /// </summary>
    private void RefreshFromStore(LogStoreSnapshot snapshot)
    {
        var categories = _logStore.GetAvailableCategories();
        try
        {
            var searchResult = _logStore.QueryLogs(_filterState.CreateFilterCriteria());
            var nextVisibleLogs = new List<LogViewerListItemViewModel>(searchResult.MatchCount);
            for (var index = searchResult.MatchCount - 1; index >= 0; index--)
            {
                nextVisibleLogs.Add(new LogViewerListItemViewModel(searchResult.Matches[index]));
            }

            var selectedSequenceNumber = SelectedLog?.SequenceNumber;

            UpdateOnUiThread(() =>
            {
                _filterState.SetAvailableCategories(categories);
                TotalReceived = snapshot.TotalReceived;
                RetainedCount = snapshot.RetainedCount;
                QueryElapsedMilliseconds = searchResult.ElapsedMilliseconds;
                ValidationError = string.Empty;
                LatestSummary = snapshot.LatestRecord?.Summary ?? "No log frames yet.";
                FilterSummary = $"{searchResult.MatchCount} visible / {snapshot.RetainedCount} retained / {snapshot.TotalReceived} total / {searchResult.ElapsedMilliseconds} ms";

                VisibleLogs.Clear();
                LogViewerListItemViewModel? matchedSelection = null;
                foreach (var record in nextVisibleLogs)
                {
                    VisibleLogs.Add(record);
                    if (selectedSequenceNumber.HasValue && record.SequenceNumber == selectedSequenceNumber.Value)
                    {
                        matchedSelection = record;
                    }
                }

                SelectedLog = ResolveSelection(matchedSelection);
                OnPropertyChanged(nameof(HasVisibleLogs));
                OnPropertyChanged(nameof(EmptyStateText));
                ExportCommand.RaiseCanExecuteChanged();
            });
        }
        catch (ArgumentException ex)
        {
            UpdateOnUiThread(() =>
            {
                _filterState.SetAvailableCategories(categories);
                TotalReceived = snapshot.TotalReceived;
                RetainedCount = snapshot.RetainedCount;
                QueryElapsedMilliseconds = 0;
                LatestSummary = snapshot.LatestRecord?.Summary ?? "No log frames yet.";
                FilterSummary = $"Filter error / {snapshot.RetainedCount} retained / {snapshot.TotalReceived} total";
                ValidationError = ex.Message;
                OnPropertyChanged(nameof(HasVisibleLogs));
                OnPropertyChanged(nameof(EmptyStateText));
                ExportCommand.RaiseCanExecuteChanged();
            });
        }
    }

    private LogViewerListItemViewModel? ResolveSelection(LogViewerListItemViewModel? matchedSelection)
    {
        if (VisibleLogs.Count == 0)
        {
            return null;
        }

        if (IsAutoScrollEnabled)
        {
            return VisibleLogs[VisibleLogs.Count - 1];
        }

        return matchedSelection ?? VisibleLogs[VisibleLogs.Count - 1];
    }

    private void SnapSelectionToLatest()
    {
        if (VisibleLogs.Count == 0)
        {
            return;
        }

        SelectedLog = VisibleLogs[VisibleLogs.Count - 1];
    }

    private void UpdateOnUiThread(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.Invoke(action);
    }
}
