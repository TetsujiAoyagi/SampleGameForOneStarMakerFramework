using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Windows;
using DebugStudio.App.Core.Infrastructure;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.App.Core.Mvvm;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;
using DebugStudio.App.Features.Shell;
using DebugStudio.App.Features.Session;
using DebugStudio.App.Features.LogViewer;
using DebugStudio.App.Features.Hierarchy;
using DebugStudio.App.Features.Inspector;
using DebugStudio.App.Features.Telemetry;
using DebugStudio.App.Features.Commands;
using DebugStudio.Client;

namespace DebugStudio.App.Core.Composition;

public sealed class AppCompositionRoot
{
    /// <summary>
    /// MainWindow が必要とする shell persistence service 群をまとめて構築する。
    /// UI control を持たない純 service だけをここで組み立て、DockingManager との接続は view 側へ残す。
    /// </summary>
    public MainWindow CreateMainWindow(bool autoListen = false)
    {
        var composition = CreateShellComposition(autoListen);
        var viewModel = composition.ViewModel;
        var shellLayoutPersistenceService = new ShellLayoutPersistenceService(
            ShellLayoutPersistenceService.CreateDefaultLayoutFilePath());
        var shellLayoutSerializerService = new ShellLayoutSerializerService();
        var logPersistenceService = TryCreateLogPersistenceService(composition.MessageRouter);
        var telemetryPersistenceService = TryCreateTelemetryPersistenceService(
            composition.MessageRouter,
            composition.TelemetrySessionAttributesStore);
        IAsyncDisposable appLifetime = viewModel;

        var cliControlService = new DebugStudioCliControlService(composition.SessionService, composition.CommandService,
            readService: composition.CliReadService);
        try
        {
            cliControlService.StartAsync().GetAwaiter().GetResult();
            appLifetime = CreateAppLifetime(
                composition.AutoListenService,
                cliControlService,
                logPersistenceService,
                telemetryPersistenceService,
                viewModel);
        }
        catch (Exception ex)
        {
            // control plane 起動失敗で WPF 自体まで落とすと既存の Unity 接続用途を壊してしまう。
            // そのためここでは control plane だけ無効化し、本体 UI は従来どおり起動を継続する。
            Debug.WriteLine($"CLI control plane failed to start: {ex}");
            cliControlService.DisposeAsync().AsTask().GetAwaiter().GetResult();
            appLifetime = CreateAppLifetime(
                composition.AutoListenService,
                null,
                logPersistenceService,
                telemetryPersistenceService,
                viewModel);
        }

        var window = new MainWindow(
            viewModel,
            shellLayoutPersistenceService,
            shellLayoutSerializerService,
            appLifetime,
            asynchronousShutdown: autoListen);
        composition.AutoListenService?.Start(new DebugSocketClientOptions());
        return window;
    }

    /// <summary>
    /// アプリ起動時の object graph を 1 箇所で組み立てる。
    ///
    /// <para>
    /// ここでは「どの store/service/viewmodel がどれに依存するか」を明示し、
    /// MainWindow 自体は純粋な view として保つ。
    /// </para>
    /// <para>
    /// 将来 DI container を導入する場合も、このメソッドが現在の手組み composition の正本になる。
    /// </para>
    /// </summary>
    public MainWindowViewModel CreateMainWindowViewModel()
    {
        return CreateShellComposition().ViewModel;
    }

    private AppShellComposition CreateShellComposition(bool autoListen = false)
    {
        // WPF の UI スレッド dispatcher は viewmodel 側の ObservableCollection 更新で必要になる。
        // 先に取得しておくことで、下流の VM が Application.Current を直接参照しなくて済む。
        var dispatcher = Application.Current.Dispatcher;

        // retain / query / state の基盤となる store 群。
        // ここは画面ごとではなく、アプリ全体の shared app state として 1 セッション単位で束ねる。
        var logStore = new LogStore(capacity: 2048);
        var hierarchyStore = new HierarchyStore();
        var inspectorStore = new InspectorStore();
        var telemetryStore = new TelemetryStore();
        var commandStore = new CommandStore();

        // pure service は副作用の少ない順に先に組み立てる。
        // export は query service と writer を組み合わせた app service として構成する。
        var logQueryService = new LogQueryService();
        var logExportService = new LogExportService(
            logStore,
            logQueryService,
            new ILogExportWriter[]
            {
                new NdjsonLogExportWriter(),
                new CsvLogExportWriter(),
                new ElasticBulkLogExportWriter(),
            });
        var telemetrySessionAttributesStore = new TelemetrySessionAttributesStore();
        var telemetryExportService = new TelemetryExportService(
            telemetryStore,
            new ITelemetryExportWriter[]
            {
                new NdjsonTelemetryExportWriter(),
                new ElasticBulkTelemetryExportWriter(),
            },
            telemetrySessionAttributesStore);
        var elasticHttpClient = new HttpClient();
        var elasticHttpClientLifetime = new HttpClientAsyncDisposable(elasticHttpClient);
        var elasticTelemetryPushService = new ElasticTelemetryPushService(
            telemetryStore,
            telemetrySessionAttributesStore,
            new ProcessElasticEnvironmentReader(),
            elasticHttpClient);
        var hierarchyExportService = new HierarchyExportService(
            hierarchyStore,
            new NdjsonHierarchyExportWriter());
        var inspectorExportService = new InspectorExportService(
            inspectorStore,
            new NdjsonInspectorExportWriter());
        var capabilityHandshakeService = new CapabilityHandshakeService();
        var capabilityStateStore = new CapabilityStateStore(capabilityHandshakeService.LocalSupportedCapabilities);

        // transport session と、それに付随する orchestration collaborator を構築する。
        // rf2 以降は SessionService に全部を詰めず、reset / routing / capability hello を分離している。
        // 現在の WPF shell は Unity へ outbound 接続せず、server transport で着信待受を開始する。
        var session = new DebugStudioServerSessionTransport(continueOnIdleTimeout: autoListen);
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
            capabilityStateStore,
            telemetrySessionAttributesStore);
        var capabilityCoordinator = new SessionCapabilityCoordinator(
            session,
            capabilityHandshakeService,
            capabilityStateStore);

        var sessionService = new SessionService(
            session,
            resetPolicy,
            messageRouter,
            capabilityCoordinator);

        // feature 別の VM は shared core を受け取りながら、UI 境界の責務だけを持つよう組み立てる。
        var inspectorQueryService = new InspectorQueryService(sessionService, capabilityStateStore, inspectorStore);
        var logViewer = new LogViewerViewModel(dispatcher, logStore, logQueryService, logExportService);
        var hierarchyViewModel = new HierarchyViewModel(
            dispatcher,
            hierarchyStore,
            capabilityStateStore,
            inspectorQueryService,
            hierarchyExportService,
            new HierarchyExportPathPolicy());
        var inspectorViewModel = new InspectorViewModel(
            dispatcher,
            inspectorStore,
            capabilityStateStore,
            inspectorExportService,
            new InspectorExportPathPolicy());
        var autoListenService = autoListen ? new AutoListenService(sessionService) : null;
        var cliReadService = new CliReadService(sessionService, hierarchyStore, capabilityStateStore);
        var sessionViewModel = new SessionWindowViewModel(dispatcher, sessionService, capabilityStateStore, autoListenService);
        var telemetryViewModel = new TelemetryWindowViewModel(
            dispatcher,
            telemetryStore,
            capabilityStateStore,
            telemetryExportService,
            new TelemetryExportPathPolicy(),
            elasticTelemetryPushService,
            new WpfElasticPushConfirmation());
        var commandService = new CommandService(sessionService, capabilityStateStore, commandStore);
        var commandViewModel = new CommandWindowViewModel(dispatcher, commandStore, capabilityStateStore, commandService);

        // 最後に shell composition へ集約し、Window 側はこの完成済み VM を受け取るだけにする。
        return new AppShellComposition(
            new MainWindowViewModel(
                sessionViewModel,
                telemetryViewModel,
                commandViewModel,
                logViewer,
                hierarchyViewModel,
                inspectorViewModel,
                elasticHttpClientLifetime),
            sessionService,
            commandService,
            messageRouter,
            telemetrySessionAttributesStore,
            autoListenService,
            cliReadService);
    }

    private static LogPersistenceService? TryCreateLogPersistenceService(SessionMessageRouter messageRouter)
    {
        try
        {
            var pathPolicy = new LogPersistencePathPolicy();
            Directory.CreateDirectory(pathPolicy.Directory);
            var writer = new RollingLogFileWriter(pathPolicy.Directory);
            return new LogPersistenceService(messageRouter, writer);
        }
        catch (Exception ex)
        {
            // persistence 初期化失敗で shell 起動自体を止めない。
            Debug.WriteLine($"Log persistence failed to initialize: {ex}");
            return null;
        }
    }

    private static TelemetryPersistenceService? TryCreateTelemetryPersistenceService(
        SessionMessageRouter messageRouter,
        TelemetrySessionAttributesStore sessionAttributesStore)
    {
        try
        {
            var pathPolicy = new TelemetryPersistencePathPolicy();
            Directory.CreateDirectory(pathPolicy.Directory);
            var writer = new RollingTelemetryFileWriter(pathPolicy.Directory);
            return new TelemetryPersistenceService(messageRouter, writer, sessionAttributesStore);
        }
        catch (Exception ex)
        {
            // 観測追加が DebugStudio 起動を阻害してはいけないため、telemetry 永続化だけ degrade する。
            Debug.WriteLine($"Telemetry persistence failed to initialize: {ex}");
            return null;
        }
    }

    private static IAsyncDisposable CreateAppLifetime(
        AutoListenService? autoListenService,
        DebugStudioCliControlService? cliControlService,
        LogPersistenceService? logPersistenceService,
        TelemetryPersistenceService? telemetryPersistenceService,
        MainWindowViewModel viewModel)
    {
        var disposables = new List<IAsyncDisposable>(4);
        if (autoListenService != null) disposables.Add(autoListenService);
        if (cliControlService != null)
        {
            disposables.Add(cliControlService);
        }

        if (logPersistenceService != null)
        {
            disposables.Add(logPersistenceService);
        }

        if (telemetryPersistenceService != null)
        {
            disposables.Add(telemetryPersistenceService);
        }

        disposables.Add(viewModel);
        return disposables.Count == 1
            ? viewModel
            : new OrderedAsyncDisposable(disposables.ToArray());
    }

    private sealed record AppShellComposition(
        MainWindowViewModel ViewModel,
        SessionService SessionService,
        CommandService CommandService,
        SessionMessageRouter MessageRouter,
        TelemetrySessionAttributesStore TelemetrySessionAttributesStore,
        AutoListenService? AutoListenService,
        CliReadService CliReadService);
}
