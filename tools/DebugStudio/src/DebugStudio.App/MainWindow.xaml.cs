using System;
using System.ComponentModel;
using System.Windows;
using DebugStudio.App.Core.Mvvm;
using DebugStudio.App.Features.Shell;

namespace DebugStudio.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ShellLayoutCoordinator _shellLayoutCoordinator;
    private readonly IAsyncDisposable _lifetime;

    public MainWindow(
        MainWindowViewModel viewModel,
        ShellLayoutPersistenceService shellLayoutPersistenceService,
        ShellLayoutSerializerService shellLayoutSerializerService,
        IAsyncDisposable? lifetime = null)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _lifetime = lifetime ?? _viewModel;
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
        // 終了直前の pane 位置・サイズ・選択タブを保存する。
        // 保存失敗は coordinator/persistence 側で degrade し、close 自体は止めない。
        _shellLayoutCoordinator.SaveLayout();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // OnClosed を sync 化し、async void 由来の unhandled exception を避ける。
        _lifetime.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
