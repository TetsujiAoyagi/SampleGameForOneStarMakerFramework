#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DebugStudio.App.Core.Composition;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.App.Features.Shell;
using DebugStudio.App.Features.Session;
using DebugStudio.App.Features.LogViewer;
using DebugStudio.App.Features.Hierarchy;
using DebugStudio.App.Features.Inspector;
using DebugStudio.App.Features.Telemetry;
using DebugStudio.App.Features.Commands;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Tests;

/// <summary>
/// shell composition の不変条件と組み立て安全性を検証する。
/// AvalonDock drag/drop や pixel layout に依存せず、
/// ViewModelレベルでの契約を守っているかを確認。
/// </summary>
public sealed class ShellCompositionTests
{
    #region ToolWindowDefinition Tests

    [Fact]
    public void ToolWindowDefinition_正常なパラメータで構築できる()
    {
        // Arrange
        var id = "test-window";
        var title = "Test Window";
        var description = "Test description";
        var placement = ToolWindowPlacement.Left;

        // Act
        var definition = new ToolWindowDefinition(id, title, description, placement);

        // Assert
        Assert.Equal(id, definition.Id);
        Assert.Equal(title, definition.Title);
        Assert.Equal(description, definition.Description);
        Assert.Equal(placement, definition.DefaultPlacement);
        Assert.Equal(ToolWindowDockKind.Anchorable, definition.DefaultDockKind);
        Assert.Equal("left", definition.DefaultGroupKey);
        Assert.Equal(0, definition.DefaultOrder);
        Assert.False(definition.CanClose);
        Assert.False(definition.CanHide);
    }

    [Fact]
    public void ToolWindowDefinition_空文字列のIdで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new ToolWindowDefinition("", "Title", "Description", ToolWindowPlacement.Center));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolWindowDefinition_ホワイトスペースのIdで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new ToolWindowDefinition("   ", "Title", "Description", ToolWindowPlacement.Center));

        Assert.Contains("id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolWindowDefinition_空文字列のTitleで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new ToolWindowDefinition("test-id", "", "Description", ToolWindowPlacement.Center));

        Assert.Contains("title", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolWindowDefinition_ホワイトスペースのTitleで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new ToolWindowDefinition("test-id", "  \t  ", "Description", ToolWindowPlacement.Center));

        Assert.Contains("title", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolWindowDefinition_nullのDescriptionを空文字列に正規化する()
    {
        // Act
        var definition = new ToolWindowDefinition("test-id", "Title", null!, ToolWindowPlacement.Right);

        // Assert
        Assert.Equal(string.Empty, definition.Description);
    }

    [Theory]
    [InlineData(ToolWindowPlacement.Left)]
    [InlineData(ToolWindowPlacement.Center)]
    [InlineData(ToolWindowPlacement.Right)]
    [InlineData(ToolWindowPlacement.Bottom)]
    public void ToolWindowDefinition_すべてのPlacement値をサポートする(ToolWindowPlacement placement)
    {
        // Act
        var definition = new ToolWindowDefinition("test-id", "Title", "Description", placement);

        // Assert
        Assert.Equal(placement, definition.DefaultPlacement);
    }

    #endregion

    #region ShellLayoutDefinitions Tests

    [Fact]
    public void ShellLayoutDefinitions_すべての窓定義が存在する()
    {
        // Assert - 6窓が定義されている
        Assert.NotNull(ShellLayoutDefinitions.Session);
        Assert.NotNull(ShellLayoutDefinitions.LogViewer);
        Assert.NotNull(ShellLayoutDefinitions.Hierarchy);
        Assert.NotNull(ShellLayoutDefinitions.Inspector);
        Assert.NotNull(ShellLayoutDefinitions.Telemetry);
        Assert.NotNull(ShellLayoutDefinitions.Commands);
        Assert.Equal(6, ShellLayoutDefinitions.All.Count);
    }

    [Fact]
    public void ShellLayoutDefinitions_各窓が期待されるIDを持つ()
    {
        // Assert
        Assert.Equal("session", ShellLayoutDefinitions.Session.Id);
        Assert.Equal("logcat", ShellLayoutDefinitions.LogViewer.Id);
        Assert.Equal("hierarchy", ShellLayoutDefinitions.Hierarchy.Id);
        Assert.Equal("inspector", ShellLayoutDefinitions.Inspector.Id);
        Assert.Equal("telemetry", ShellLayoutDefinitions.Telemetry.Id);
        Assert.Equal("commands", ShellLayoutDefinitions.Commands.Id);
    }

    [Fact]
    public void ShellLayoutDefinitions_各窓が適切な初期配置を持つ()
    {
        // Assert
        Assert.Equal(ToolWindowPlacement.Left, ShellLayoutDefinitions.Session.DefaultPlacement);
        Assert.Equal(ToolWindowPlacement.Center, ShellLayoutDefinitions.LogViewer.DefaultPlacement);
        Assert.Equal(ToolWindowPlacement.Left, ShellLayoutDefinitions.Hierarchy.DefaultPlacement);
        Assert.Equal(ToolWindowPlacement.Right, ShellLayoutDefinitions.Inspector.DefaultPlacement);
        Assert.Equal(ToolWindowPlacement.Bottom, ShellLayoutDefinitions.Telemetry.DefaultPlacement);
        Assert.Equal(ToolWindowPlacement.Bottom, ShellLayoutDefinitions.Commands.DefaultPlacement);
    }

    [Fact]
    public void ShellLayoutDefinitions_各窓が現在のDock種別とgroup情報を持つ()
    {
        Assert.Equal(ToolWindowDockKind.Anchorable, ShellLayoutDefinitions.Session.DefaultDockKind);
        Assert.Equal(ToolWindowDockKind.Document, ShellLayoutDefinitions.LogViewer.DefaultDockKind);
        Assert.Equal("left-stack", ShellLayoutDefinitions.Session.DefaultGroupKey);
        Assert.Equal("left-stack", ShellLayoutDefinitions.Hierarchy.DefaultGroupKey);
        Assert.Equal("right-stack", ShellLayoutDefinitions.Inspector.DefaultGroupKey);
        Assert.Equal("bottom-stack", ShellLayoutDefinitions.Telemetry.DefaultGroupKey);
        Assert.Equal("bottom-stack", ShellLayoutDefinitions.Commands.DefaultGroupKey);
    }

    [Fact]
    public void ShellLayoutDefinitions_すべてのIDがユニークである()
    {
        // Arrange
        var definitions = new[]
        {
            ShellLayoutDefinitions.Session,
            ShellLayoutDefinitions.LogViewer,
            ShellLayoutDefinitions.Hierarchy,
            ShellLayoutDefinitions.Inspector,
            ShellLayoutDefinitions.Telemetry,
            ShellLayoutDefinitions.Commands,
        };

        // Act
        var ids = definitions.Select(d => d.Id).ToList();
        var uniqueIds = ids.Distinct().ToList();

        // Assert
        Assert.Equal(ids.Count, uniqueIds.Count);
    }

    #endregion

    #region ToolWindowDescriptorViewModel Tests

    [Fact]
    public void ToolWindowDescriptor_定義とContentで構築できる()
    {
        // Arrange
        var definition = new ToolWindowDefinition("test-id", "Test Window", "Description", ToolWindowPlacement.Left);
        var contentViewModel = new object();

        // Act
        var descriptor = new ToolWindowDescriptorViewModel(definition, contentViewModel);

        // Assert
        Assert.Same(definition, descriptor.Definition);
        Assert.Same(contentViewModel, descriptor.ContentViewModel);
        Assert.Equal(definition.Id, descriptor.Id);
        Assert.Equal(definition.Title, descriptor.Title);
        Assert.Equal(definition.Description, descriptor.Description);
        Assert.Equal(definition.DefaultPlacement, descriptor.Placement);
    }

    [Fact]
    public void ToolWindowDescriptor_nullの定義で例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ToolWindowDescriptorViewModel(null!, new object()));

        Assert.Contains("definition", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolWindowDescriptor_nullのContentViewModelで例外を投げる()
    {
        // Act & Assert
        var definition = new ToolWindowDefinition("test-id", "Title", "Description", ToolWindowPlacement.Left);
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ToolWindowDescriptorViewModel(definition, null!));

        Assert.Contains("contentViewModel", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region ShellLayoutViewModel Tests

    [Fact]
    public void ShellLayout_正常な6窓構成で構築できる()
    {
        // Arrange
        var session = CreateDescriptor("session", ToolWindowPlacement.Left);
        var logcat = CreateDescriptor("logcat", ToolWindowPlacement.Center);
        var hierarchy = CreateDescriptor("hierarchy", ToolWindowPlacement.Left);
        var inspector = CreateDescriptor("inspector", ToolWindowPlacement.Right);
        var telemetry = CreateDescriptor("telemetry", ToolWindowPlacement.Bottom);
        var commands = CreateDescriptor("commands", ToolWindowPlacement.Bottom);

        // Act
        var shell = new ShellLayoutViewModel(
            session,
            logcat,
            hierarchy,
            inspector,
            telemetry,
            commands);

        // Assert
        Assert.Same(session, shell.SessionWindow);
        Assert.Same(logcat, shell.LogVieweWindow);
        Assert.Same(hierarchy, shell.HierarchyWindow);
        Assert.Same(inspector, shell.InspectorWindow);
        Assert.Same(telemetry, shell.TelemetryWindow);
        Assert.Same(commands, shell.CommandsWindow);
    }

    [Fact]
    public void ShellLayout_Inventoryは厳密に6個の窓を含む()
    {
        // Arrange & Act
        var shell = CreateShellLayout();

        // Assert - 6窓の契約を守る
        Assert.NotNull(shell.Inventory);
        Assert.Equal(6, shell.Inventory.Count);
    }

    [Fact]
    public void ShellLayout_Inventoryの順序が定義された順番を維持する()
    {
        // Arrange & Act
        var shell = CreateShellLayout();

        // Assert - 順序: session, logcat, hierarchy, inspector, telemetry, commands
        Assert.Same(shell.SessionWindow, shell.Inventory[0]);
        Assert.Same(shell.LogVieweWindow, shell.Inventory[1]);
        Assert.Same(shell.HierarchyWindow, shell.Inventory[2]);
        Assert.Same(shell.InspectorWindow, shell.Inventory[3]);
        Assert.Same(shell.TelemetryWindow, shell.Inventory[4]);
        Assert.Same(shell.CommandsWindow, shell.Inventory[5]);
    }

    [Fact]
    public void ShellLayout_各窓プロパティがInventoryに含まれる()
    {
        // Arrange & Act
        var shell = CreateShellLayout();

        // Assert
        Assert.Contains(shell.SessionWindow, shell.Inventory);
        Assert.Contains(shell.LogVieweWindow, shell.Inventory);
        Assert.Contains(shell.HierarchyWindow, shell.Inventory);
        Assert.Contains(shell.InspectorWindow, shell.Inventory);
        Assert.Contains(shell.TelemetryWindow, shell.Inventory);
        Assert.Contains(shell.CommandsWindow, shell.Inventory);
    }

    [Fact]
    public void ShellLayout_ContentIdから各窓を逆引きできる()
    {
        var shell = CreateShellLayout();

        Assert.True(shell.TryGetWindow("session", out var session));
        Assert.Same(shell.SessionWindow, session);
        Assert.False(shell.TryGetWindow("missing-window", out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void ShellLayout_nullのSessionWindowで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ShellLayoutViewModel(
                null!,
                CreateDescriptor("logcat", ToolWindowPlacement.Center),
                CreateDescriptor("hierarchy", ToolWindowPlacement.Left),
                CreateDescriptor("inspector", ToolWindowPlacement.Right),
                CreateDescriptor("telemetry", ToolWindowPlacement.Bottom),
                CreateDescriptor("commands", ToolWindowPlacement.Bottom)));

        Assert.Contains("sessionWindow", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellLayout_nullのLogViewerWindowで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ShellLayoutViewModel(
                CreateDescriptor("session", ToolWindowPlacement.Left),
                null!,
                CreateDescriptor("hierarchy", ToolWindowPlacement.Left),
                CreateDescriptor("inspector", ToolWindowPlacement.Right),
                CreateDescriptor("telemetry", ToolWindowPlacement.Bottom),
                CreateDescriptor("commands", ToolWindowPlacement.Bottom)));

        Assert.Contains("logcatWindow", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellLayout_nullのHierarchyWindowで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ShellLayoutViewModel(
                CreateDescriptor("session", ToolWindowPlacement.Left),
                CreateDescriptor("logviewer", ToolWindowPlacement.Center),
                null!,
                CreateDescriptor("inspector", ToolWindowPlacement.Right),
                CreateDescriptor("telemetry", ToolWindowPlacement.Bottom),
                CreateDescriptor("commands", ToolWindowPlacement.Bottom)));

        Assert.Contains("hierarchyWindow", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellLayout_nullのInspectorWindowで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ShellLayoutViewModel(
                CreateDescriptor("session", ToolWindowPlacement.Left),
                CreateDescriptor("logviewer", ToolWindowPlacement.Center),
                CreateDescriptor("hierarchy", ToolWindowPlacement.Left),
                null!,
                CreateDescriptor("telemetry", ToolWindowPlacement.Bottom),
                CreateDescriptor("commands", ToolWindowPlacement.Bottom)));

        Assert.Contains("inspectorWindow", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellLayout_nullのTelemetryWindowで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ShellLayoutViewModel(
                CreateDescriptor("session", ToolWindowPlacement.Left),
                CreateDescriptor("logviewer", ToolWindowPlacement.Center),
                CreateDescriptor("hierarchy", ToolWindowPlacement.Left),
                CreateDescriptor("inspector", ToolWindowPlacement.Right),
                null!,
                CreateDescriptor("commands", ToolWindowPlacement.Bottom)));

        Assert.Contains("telemetryWindow", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellLayout_nullのCommandsWindowで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ShellLayoutViewModel(
                CreateDescriptor("session", ToolWindowPlacement.Left),
                CreateDescriptor("logviewer", ToolWindowPlacement.Center),
                CreateDescriptor("hierarchy", ToolWindowPlacement.Left),
                CreateDescriptor("inspector", ToolWindowPlacement.Right),
                CreateDescriptor("telemetry", ToolWindowPlacement.Bottom),
                null!));

        Assert.Contains("commandsWindow", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region MainWindowViewModel Tests

    [Fact]
    public void MainWindow_正常な依存関係で構築できる()
    {
        // Arrange
        var session = CreateStubSessionViewModel();
        var telemetry = CreateStubTelemetryViewModel();
        var commands = CreateStubCommandViewModel();
        var logViewer = CreateStubLogViewerViewModel();
        var hierarchy = CreateStubHierarchyViewModel();
        var inspector = CreateStubInspectorViewModel();

        // Act
        var mainWindow = new MainWindowViewModel(
            session,
            telemetry,
            commands,
            logViewer,
            hierarchy,
            inspector);

        // Assert
        Assert.Same(session, mainWindow.Session);
        Assert.Same(telemetry, mainWindow.Telemetry);
        Assert.Same(commands, mainWindow.Commands);
        Assert.Same(logViewer, mainWindow.LogViewer);
        Assert.Same(hierarchy, mainWindow.Hierarchy);
        Assert.Same(inspector, mainWindow.Inspector);
    }

    [Fact]
    public void MainWindow_Shellプロパティが自動的に構築される()
    {
        // Arrange & Act
        var mainWindow = CreateStubMainWindow();

        // Assert
        Assert.NotNull(mainWindow.Shell);
        Assert.IsType<ShellLayoutViewModel>(mainWindow.Shell);
    }

    [Fact]
    public void MainWindow_Shellが6個の窓定義を持つ()
    {
        // Arrange & Act
        var mainWindow = CreateStubMainWindow();

        // Assert - 6窓の契約を守る
        Assert.Equal(6, mainWindow.Shell.Inventory.Count);
    }

    [Fact]
    public void MainWindow_Shell内の各窓がContentViewModelに正しいVMを参照する()
    {
        // Arrange & Act
        var mainWindow = CreateStubMainWindow();

        // Assert - 各windowのContentViewModelが対応するVMを指している
        Assert.Same(mainWindow.Session, mainWindow.Shell.SessionWindow.ContentViewModel);
        Assert.Same(mainWindow.LogViewer, mainWindow.Shell.LogVieweWindow.ContentViewModel);
        Assert.Same(mainWindow.Hierarchy, mainWindow.Shell.HierarchyWindow.ContentViewModel);
        Assert.Same(mainWindow.Inspector, mainWindow.Shell.InspectorWindow.ContentViewModel);
        Assert.Same(mainWindow.Telemetry, mainWindow.Shell.TelemetryWindow.ContentViewModel);
        Assert.Same(mainWindow.Commands, mainWindow.Shell.CommandsWindow.ContentViewModel);
    }

    [Fact]
    public void MainWindow_Shell内の窓IDが期待される値()
    {
        // Arrange & Act
        var mainWindow = CreateStubMainWindow();

        // Assert - 定義されたIDを持つ
        Assert.Equal("session", mainWindow.Shell.SessionWindow.Id);
        Assert.Equal("logviewer", mainWindow.Shell.LogVieweWindow.Id);
        Assert.Equal("hierarchy", mainWindow.Shell.HierarchyWindow.Id);
        Assert.Equal("inspector", mainWindow.Shell.InspectorWindow.Id);
        Assert.Equal("telemetry", mainWindow.Shell.TelemetryWindow.Id);
        Assert.Equal("commands", mainWindow.Shell.CommandsWindow.Id);
    }

    [Fact]
    public void MainWindow_Shell内の窓Placementが適切な配置()
    {
        // Arrange & Act
        var mainWindow = CreateStubMainWindow();

        // Assert - 配置定義が意図通り
        Assert.Equal(ToolWindowPlacement.Left, mainWindow.Shell.SessionWindow.Placement);
        Assert.Equal(ToolWindowPlacement.Center, mainWindow.Shell.LogVieweWindow.Placement);
        Assert.Equal(ToolWindowPlacement.Left, mainWindow.Shell.HierarchyWindow.Placement);
        Assert.Equal(ToolWindowPlacement.Right, mainWindow.Shell.InspectorWindow.Placement);
        Assert.Equal(ToolWindowPlacement.Bottom, mainWindow.Shell.TelemetryWindow.Placement);
        Assert.Equal(ToolWindowPlacement.Bottom, mainWindow.Shell.CommandsWindow.Placement);
    }

    [Fact]
    public void MainWindow_Shell内の窓が重複しないユニークなID()
    {
        // Arrange & Act
        var mainWindow = CreateStubMainWindow();

        // Assert
        var ids = mainWindow.Shell.Inventory.Select(w => w.Id).ToList();
        var uniqueIds = ids.Distinct().ToList();
        Assert.Equal(ids.Count, uniqueIds.Count);
    }

    [Fact]
    public void MainWindow_nullのSessionで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(
                null!,
                CreateStubTelemetryViewModel(),
                CreateStubCommandViewModel(),
                CreateStubLogViewerViewModel(),
                CreateStubHierarchyViewModel(),
                CreateStubInspectorViewModel()));

        Assert.Contains("session", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindow_nullのTelemetryで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(
                CreateStubSessionViewModel(),
                null!,
                CreateStubCommandViewModel(),
                CreateStubLogViewerViewModel(),
                CreateStubHierarchyViewModel(),
                CreateStubInspectorViewModel()));

        Assert.Contains("telemetry", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindow_nullのCommandsで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(
                CreateStubSessionViewModel(),
                CreateStubTelemetryViewModel(),
                null!,
                CreateStubLogViewerViewModel(),
                CreateStubHierarchyViewModel(),
                CreateStubInspectorViewModel()));

        Assert.Contains("commands", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindow_nullのLogViewerで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(
                CreateStubSessionViewModel(),
                CreateStubTelemetryViewModel(),
                CreateStubCommandViewModel(),
                null!,
                CreateStubHierarchyViewModel(),
                CreateStubInspectorViewModel()));

        Assert.Contains("logViewer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindow_nullのHierarchyで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(
                CreateStubSessionViewModel(),
                CreateStubTelemetryViewModel(),
                CreateStubCommandViewModel(),
                CreateStubLogViewerViewModel(),
                null!,
                CreateStubInspectorViewModel()));

        Assert.Contains("hierarchy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindow_nullのInspectorで例外を投げる()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new MainWindowViewModel(
                CreateStubSessionViewModel(),
                CreateStubTelemetryViewModel(),
                CreateStubCommandViewModel(),
                CreateStubLogViewerViewModel(),
                CreateStubHierarchyViewModel(),
                null!));

        Assert.Contains("inspector", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Session Listener UI Tests

    [Fact]
    public async Task SessionWindowViewModel_初期表示が待受UI向けになる()
    {
        var (_, _, _, _, _, _, session) = CreateSessionHarness();

        await using var viewModel = session;

        Assert.Equal("Stopped", viewModel.ConnectionState);
        Assert.Equal("Ready to listen for a Unity connection.", viewModel.ConnectionDetail);
        Assert.Equal("Idle", viewModel.CapabilityStatus);
        Assert.Equal("Start listening to negotiate capabilities when Unity connects.", viewModel.CapabilityDetail);
        Assert.Equal("Unity: Unknown / Session: n/a", viewModel.SessionIdentityText);
    }

    [Fact]
    public async Task SessionWindowViewModel_接続状態を待受UI表現へ変換する()
    {
        var (transport, _, _, _, _, _, session) = CreateSessionHarness();
        var listenUri = new Uri("ws://127.0.0.1:5010/debugsocket/");

        await using var viewModel = session;

        transport.RaiseConnectionStateChanged(new DebugSocketConnectionSnapshot(
            DebugSocketConnectionState.Connecting,
            listenUri,
            "Connecting...",
            DateTimeOffset.UtcNow));

        Assert.Equal("Listening", viewModel.ConnectionState);
        Assert.Equal($"Listening for an inbound Unity WebSocket on {listenUri}.", viewModel.ConnectionDetail);

        transport.RaiseConnectionStateChanged(new DebugSocketConnectionSnapshot(
            DebugSocketConnectionState.Connected,
            listenUri,
            "Connected.",
            DateTimeOffset.UtcNow));

        Assert.Equal("Attached", viewModel.ConnectionState);
        Assert.Equal($"Unity is connected on {listenUri}.", viewModel.ConnectionDetail);
        Assert.Contains("[listener] Unity is connected", viewModel.RecentActivity[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionWindowViewModel_能力交渉詳細を待受UI向けへ変換する()
    {
        var (_, capabilityStateStore, _, _, _, _, session) = CreateSessionHarness();

        await using var viewModel = session;

        capabilityStateStore.ResetForConnect(new Uri("ws://127.0.0.1:5010/debugsocket/"));

        Assert.Equal("Negotiating", viewModel.CapabilityStatus);
        Assert.Equal("Negotiating capabilities with the connected Unity session.", viewModel.CapabilityDetail);
    }

    [Fact]
    public async Task AppCompositionRoot_既定でServerSessionTransportを組み込む()
    {
        _ = Application.Current ?? new Application();
        var root = new AppCompositionRoot();
        await using var mainWindow = root.CreateMainWindowViewModel();

        var sessionServiceField = typeof(SessionWindowViewModel).GetField("_sessionService", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(sessionServiceField);
        var sessionService = Assert.IsType<SessionService>(sessionServiceField!.GetValue(mainWindow.Session));

        var sessionTransportField = typeof(SessionService).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(sessionTransportField);
        Assert.IsType<DebugStudioServerSessionTransport>(sessionTransportField!.GetValue(sessionService));
    }

    #endregion

    #region Helper Methods

    private static ToolWindowDescriptorViewModel CreateDescriptor(string id, ToolWindowPlacement placement)
    {
        var definition = new ToolWindowDefinition(
            id,
            $"Title-{id}",
            $"Description for {id}",
            placement);
        return new ToolWindowDescriptorViewModel(definition, new object());
    }

    private static ShellLayoutViewModel CreateShellLayout()
    {
        return new ShellLayoutViewModel(
            CreateDescriptor("session", ToolWindowPlacement.Left),
            CreateDescriptor("logviewer", ToolWindowPlacement.Center),
            CreateDescriptor("hierarchy", ToolWindowPlacement.Left),
            CreateDescriptor("inspector", ToolWindowPlacement.Right),
            CreateDescriptor("telemetry", ToolWindowPlacement.Bottom),
            CreateDescriptor("commands", ToolWindowPlacement.Bottom));
    }

    private static (SessionWindowViewModel, TelemetryWindowViewModel, CommandWindowViewModel,
        LogViewerViewModel, HierarchyViewModel, InspectorViewModel) CreateViewModels()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        
        // ストアの初期化
        var telemetryStore = new TelemetryStore();
        var logStore = new LogStore(capacity: 10000);
        var hierarchyStore = new HierarchyStore();
        var inspectorStore = new InspectorStore();
        var commandStore = new CommandStore();
        
        // サービスの初期化
        var capabilityHandshakeService = new CapabilityHandshakeService();
        var capabilityStateStore = new CapabilityStateStore(capabilityHandshakeService.LocalSupportedCapabilities);
        var debugSession = new DebugStudioClientSessionTransport(new DebugStudio.Client.DebugStudioSession());
        
        var resetPolicy = new SessionResetPolicy(
            logStore,
            hierarchyStore,
            inspectorStore,
            telemetryStore,
            commandStore,
            capabilityStateStore);
        
        var messageRouter = new SessionMessageRouter(
            logStore,
            hierarchyStore,
            inspectorStore,
            telemetryStore,
            commandStore,
            capabilityStateStore);
        
        var capabilityCoordinator = new SessionCapabilityCoordinator(
            debugSession,
            capabilityHandshakeService,
            capabilityStateStore);
        
        var sessionService = new SessionService(
            debugSession,
            resetPolicy,
            messageRouter,
            capabilityCoordinator);
        var commandService = new CommandService(sessionService, capabilityStateStore, commandStore);
        var logQueryService = new LogQueryService();
        var logExportService = new LogExportService(
            logStore,
            logQueryService,
            new DebugStudio.Export.Writers.ILogExportWriter[]
            {
                new DebugStudio.Export.Writers.NdjsonLogExportWriter(),
                new DebugStudio.Export.Writers.CsvLogExportWriter(),
                new DebugStudio.Export.Writers.ElasticBulkLogExportWriter(),
            });
        var inspectorQueryService = new InspectorQueryService(sessionService, capabilityStateStore, inspectorStore);

        var session = new SessionWindowViewModel(dispatcher, sessionService, capabilityStateStore);
        var telemetry = new TelemetryWindowViewModel(dispatcher, telemetryStore, capabilityStateStore);
        var commands = new CommandWindowViewModel(dispatcher, commandStore, capabilityStateStore, commandService);
        var logViewer = new LogViewerViewModel(dispatcher, logStore, logQueryService, logExportService);
        var hierarchy = new HierarchyViewModel(dispatcher, hierarchyStore, capabilityStateStore, inspectorQueryService);
        var inspector = new InspectorViewModel(dispatcher, inspectorStore, capabilityStateStore);

        return (session, telemetry, commands, logViewer, hierarchy, inspector);
    }

    private static (FakeSessionTransport Transport, CapabilityStateStore CapabilityStateStore, CommandStore CommandStore,
        TelemetryWindowViewModel Telemetry, CommandWindowViewModel Commands, LogViewerViewModel LogViewer, SessionWindowViewModel Session)
        CreateSessionHarness()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var logStore = new LogStore(capacity: 256);
        var hierarchyStore = new HierarchyStore();
        var inspectorStore = new InspectorStore();
        var telemetryStore = new TelemetryStore();
        var commandStore = new CommandStore();
        var capabilityHandshakeService = new CapabilityHandshakeService();
        var capabilityStateStore = new CapabilityStateStore(capabilityHandshakeService.LocalSupportedCapabilities);
        var transport = new FakeSessionTransport();
        var resetPolicy = new SessionResetPolicy(
            logStore,
            hierarchyStore,
            inspectorStore,
            telemetryStore,
            commandStore,
            capabilityStateStore);
        var messageRouter = new SessionMessageRouter(
            logStore,
            hierarchyStore,
            inspectorStore,
            telemetryStore,
            commandStore,
            capabilityStateStore);
        var capabilityCoordinator = new SessionCapabilityCoordinator(
            transport,
            capabilityHandshakeService,
            capabilityStateStore);
        var sessionService = new SessionService(
            transport,
            resetPolicy,
            messageRouter,
            capabilityCoordinator);
        var commandService = new CommandService(sessionService, capabilityStateStore, commandStore);
        var logQueryService = new LogQueryService();
        var logExportService = new LogExportService(
            logStore,
            logQueryService,
            new DebugStudio.Export.Writers.ILogExportWriter[]
            {
                new DebugStudio.Export.Writers.NdjsonLogExportWriter(),
                new DebugStudio.Export.Writers.CsvLogExportWriter(),
                new DebugStudio.Export.Writers.ElasticBulkLogExportWriter(),
            });

        return (
            transport,
            capabilityStateStore,
            commandStore,
            new TelemetryWindowViewModel(dispatcher, telemetryStore, capabilityStateStore),
            new CommandWindowViewModel(dispatcher, commandStore, capabilityStateStore, commandService),
            new LogViewerViewModel(dispatcher, logStore, logQueryService, logExportService),
            new SessionWindowViewModel(dispatcher, sessionService, capabilityStateStore));
    }

    private static MainWindowViewModel CreateMainWindow()
    {
        var (session, telemetry, commands, logViewer, hierarchy, inspector) = CreateViewModels();
        return new MainWindowViewModel(session, telemetry, commands, logViewer, hierarchy, inspector);
    }

    // Stub helpers for simplified MainWindow composition tests
    private static SessionWindowViewModel CreateStubSessionViewModel()
    {
        var (session, _, _, _, _, _) = CreateViewModels();
        return session;
    }

    private static TelemetryWindowViewModel CreateStubTelemetryViewModel()
    {
        var (_, telemetry, _, _, _, _) = CreateViewModels();
        return telemetry;
    }

    private static CommandWindowViewModel CreateStubCommandViewModel()
    {
        var (_, _, commands, _, _, _) = CreateViewModels();
        return commands;
    }

    private static LogViewerViewModel CreateStubLogViewerViewModel()
    {
        var (_, _, _, logViewer, _, _) = CreateViewModels();
        return logViewer;
    }

    private static HierarchyViewModel CreateStubHierarchyViewModel()
    {
        var (_, _, _, _, hierarchy, _) = CreateViewModels();
        return hierarchy;
    }

    private static InspectorViewModel CreateStubInspectorViewModel()
    {
        var (_, _, _, _, _, inspector) = CreateViewModels();
        return inspector;
    }

    private static MainWindowViewModel CreateStubMainWindow()
    {
        return CreateMainWindow();
    }

#pragma warning disable CS0067
    private sealed class FakeSessionTransport : ISessionTransport
    {
        public DebugSocketConnectionState State { get; private set; } = DebugSocketConnectionState.Disconnected;

        public event Action<DebugSocketConnectionSnapshot>? ConnectionStateChanged;
        public event Action<LogEnvelopeV1>? LogReceived;
        public event Action<DebugTelemetryEnvelopeV1>? TelemetryReceived;
        public event Action<DebugSocketServiceStatusEnvelopeV1>? ServiceStatusReceived;
        public event Action<DebugCommandResultEnvelopeV1>? CommandResultReceived;
        public event Action<CapabilityHandshakeWelcomeEnvelopeV1>? CapabilityWelcomeReceived;
        public event Action<HierarchySnapshotEnvelopeV1>? HierarchySnapshotReceived;
        public event Action<HierarchyDeltaEnvelopeV1>? HierarchyDeltaReceived;
        public event Action<InspectorDetailEnvelopeV1>? InspectorDetailReceived;

        public Task ConnectAsync(DebugSocketClientOptions options, CancellationToken cancellationToken = default)
        {
            State = DebugSocketConnectionState.Connecting;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            State = DebugSocketConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        public Task SendCommandAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendMessageAsync<TPayload>(
            DebugSocketMessageType messageType,
            TPayload payload,
            string? requestId = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void RaiseConnectionStateChanged(DebugSocketConnectionSnapshot snapshot)
        {
            State = snapshot.State;
            ConnectionStateChanged?.Invoke(snapshot);
        }
    }
#pragma warning restore CS0067

    #endregion
}
