#nullable enable

using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DebugStudio.App.Core.Mvvm;

namespace DebugStudio.App.Features.LogViewer;

/// <summary>
/// LogViewer を MainWindow から切り離し、独立した作業面として扱うための view。
/// 自動追従スクロールだけは WPF の visual 操作なので code-behind へ閉じ込める。
/// </summary>
public partial class LogViewerView : UserControl
{
    private LogViewerViewModel? _viewModel;

    public LogViewerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.VisibleLogs.CollectionChanged -= OnVisibleLogsChanged;
        }

        _viewModel = e.NewValue as LogViewerViewModel;
        if (_viewModel == null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.VisibleLogs.CollectionChanged += OnVisibleLogsChanged;
        ScrollToLatestIfNeeded();
    }

    private void OnVisibleLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollToLatestIfNeeded();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LogViewerViewModel.IsAutoScrollEnabled) or nameof(LogViewerViewModel.SelectedLog))
        {
            ScrollToLatestIfNeeded();
        }
    }

    private void ScrollToLatestIfNeeded()
    {
        if (_viewModel == null || !_viewModel.IsAutoScrollEnabled || _viewModel.VisibleLogs.Count == 0)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            var target = _viewModel.SelectedLog ?? _viewModel.VisibleLogs[_viewModel.VisibleLogs.Count - 1];
            LogEntriesList.ScrollIntoView(target);
        }, DispatcherPriority.Background);
    }
}
