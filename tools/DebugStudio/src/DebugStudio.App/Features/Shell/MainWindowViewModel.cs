#nullable enable

using System;
using System.Threading;
using DebugStudio.App.Features.Session;
using DebugStudio.App.Features.LogViewer;
using DebugStudio.App.Features.Hierarchy;
using DebugStudio.App.Features.Inspector;
using DebugStudio.App.Features.Telemetry;
using DebugStudio.App.Features.Commands;

namespace DebugStudio.App.Features.Shell;

/// <summary>
/// shell 全体の composition root。
///
/// <para>
/// 具体的な接続制御やデータ保持は各 window/panel 用 ViewModel に委譲し、
/// この型は shell inventory と tool window 構成だけを束ねる。
/// </para>
/// </summary>
public sealed class MainWindowViewModel : IAsyncDisposable
{
    private IAsyncDisposable? _ownedLifetime;

    public MainWindowViewModel(
        SessionWindowViewModel session,
        TelemetryWindowViewModel telemetry,
        CommandWindowViewModel commands,
        LogViewerViewModel logViewer,
        HierarchyViewModel hierarchy,
        InspectorViewModel inspector,
        IAsyncDisposable? ownedLifetime = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        LogViewer = logViewer ?? throw new ArgumentNullException(nameof(logViewer));
        Hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));
        Inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _ownedLifetime = ownedLifetime;

        Shell = new ShellLayoutViewModel(
            new ToolWindowDescriptorViewModel(ShellLayoutDefinitions.Session, Session),
            new ToolWindowDescriptorViewModel(ShellLayoutDefinitions.LogViewer, LogViewer),
            new ToolWindowDescriptorViewModel(ShellLayoutDefinitions.Hierarchy, Hierarchy),
            new ToolWindowDescriptorViewModel(ShellLayoutDefinitions.Inspector, Inspector),
            new ToolWindowDescriptorViewModel(ShellLayoutDefinitions.Telemetry, Telemetry),
            new ToolWindowDescriptorViewModel(ShellLayoutDefinitions.Commands, Commands));
    }

    public SessionWindowViewModel Session { get; }

    public TelemetryWindowViewModel Telemetry { get; }

    public CommandWindowViewModel Commands { get; }

    public LogViewerViewModel LogViewer { get; }

    public HierarchyViewModel Hierarchy { get; }

    public InspectorViewModel Inspector { get; }

    public ShellLayoutViewModel Shell { get; }

    public async ValueTask DisposeAsync()
    {
        Commands.Dispose();
        Telemetry.Dispose();
        LogViewer.Dispose();
        Hierarchy.Dispose();
        Inspector.Dispose();
        try
        {
            await Session.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            // Session 停止が失敗しても composition 所有 HttpClient を残さない。
            var ownedLifetime = Interlocked.Exchange(ref _ownedLifetime, null);
            if (ownedLifetime != null)
            {
                await ownedLifetime.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
