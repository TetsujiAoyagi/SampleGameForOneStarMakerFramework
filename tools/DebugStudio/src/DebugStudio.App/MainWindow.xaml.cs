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
            // loop 停止やイベント購読解除の完了中も Dispatcher を動かし続ける。
            // 同期 Close 待ちにすると UI dispatcher を必要とする終了処理と相互待ちになる。
            // 連続する Close は同じ cleanup task に合流し、所有物を二度解放しない。
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
        // 旧経路では OnClosed を同期的な最終境界にして、async void の例外を Dispatcher へ漏らさない。
        // --listen 経路では先に OnClosing で async cleanup を終えており、ここでの再解放はしない。
        if (!_asynchronousShutdown)
        {
            try { _lifetime.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch (Exception ex) { Trace.TraceError($"Window cleanup failed: {ex}"); }
        }
    }

    private async Task ShutdownAndCloseAsync()
    {
        // 次の await まで同期実行して shutdown task を公開する。保存や解放から Close が再入しても、
        // 呼出元へ戻る前に _shutdownTask が設定済みとなり、cleanup を重ねて開始しない。
        await Task.Yield();
        try
        {
            // window 状態の保存と lifetime 解放は一度だけ行い、保存失敗で transport 停止を飛ばさない。
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
