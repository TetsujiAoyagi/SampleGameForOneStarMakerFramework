using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using DebugStudio.App.Core.Mvvm;
using DebugStudio.App.Features.Shell;

namespace DebugStudio.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ShellLayoutCoordinator _shellLayoutCoordinator;
    private readonly IAsyncDisposable _lifetime;
    private readonly bool _asynchronousShutdown;
    private Task? _shutdownTask;
    private bool _cleanupComplete;

    public MainWindow(
        MainWindowViewModel viewModel,
        ShellLayoutPersistenceService shellLayoutPersistenceService,
        ShellLayoutSerializerService shellLayoutSerializerService,
        IAsyncDisposable? lifetime = null,
        bool asynchronousShutdown = false)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _lifetime = lifetime ?? _viewModel;
        _asynchronousShutdown = asynchronousShutdown;
        DataContext = _viewModel;

        // DockingManager 実体は view 側にしか存在しないため、
        // control 依存のある layout restore/save orchestration はここで束ねる。
        _shellLayoutCoordinator = new ShellLayoutCoordinator(
            DockingManagerRoot,
            _viewModel.Shell,
            shellLayoutPersistenceService ?? throw new ArgumentNullException(nameof(shellLayoutPersistenceService)),
            shellLayoutSerializerService ?? throw new ArgumentNullException(nameof(shellLayoutSerializerService)));

        _shellLayoutCoordinator.RestoreLayout();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_asynchronousShutdown && !_cleanupComplete)
        {
            // Keep the Dispatcher pumping while cancellation and background event handlers finish.
            // Additional Close calls share this one cleanup task and cannot dispose twice.
            e.Cancel = true;
            _shutdownTask ??= ShutdownAndCloseAsync();
            base.OnClosing(e);
            return;
        }
        // 終了直前の pane 位置・サイズ・選択タブを保存する。
        // 保存失敗は coordinator/persistence 側で degrade し、close 自体は止めない。
        if (!_cleanupComplete) _shellLayoutCoordinator.SaveLayout();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // OnClosed を sync 化し、async void 由来の unhandled exception を避ける。
        if (!_asynchronousShutdown)
        {
            try { _lifetime.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch (Exception ex) { Trace.TraceError($"Window cleanup failed: {ex}"); }
        }
    }

    private async Task ShutdownAndCloseAsync()
    {
        // Publish _shutdownTask before any synchronous disposal can re-enter Close.
        await Task.Yield();
        try
        {
            try { _shellLayoutCoordinator.SaveLayout(); }
            catch (Exception ex) { Trace.TraceError($"Saving window layout failed: {ex}"); }
            try { await _lifetime.DisposeAsync(); }
            catch (Exception ex) { Trace.TraceError($"Window cleanup failed: {ex}"); }
        }
        finally
        {
            _cleanupComplete = true;
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }
}
