#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DebugStudio.App.Features.Shell;

/// <summary>
/// workbench shell 全体の runtime window inventory。
/// 各 window を first-class object として管理し、MainWindow の責務を軽く保つ。
/// AvalonDock 配置は MainWindow XAML で定義、将来の layout 永続化時は
/// ShellLayoutDefinitions の静的定義を用いて復元する。
/// </summary>
public sealed class ShellLayoutViewModel
{
    private readonly ReadOnlyDictionary<string, ToolWindowDescriptorViewModel> _inventoryById;

    public ShellLayoutViewModel(
        ToolWindowDescriptorViewModel sessionWindow,
        ToolWindowDescriptorViewModel logViewerWindow,
        ToolWindowDescriptorViewModel hierarchyWindow,
        ToolWindowDescriptorViewModel inspectorWindow,
        ToolWindowDescriptorViewModel telemetryWindow,
        ToolWindowDescriptorViewModel commandsWindow)
    {
        SessionWindow = sessionWindow ?? throw new ArgumentNullException(nameof(sessionWindow));
        LogVieweWindow = logViewerWindow ?? throw new ArgumentNullException(nameof(logViewerWindow));
        HierarchyWindow = hierarchyWindow ?? throw new ArgumentNullException(nameof(hierarchyWindow));
        InspectorWindow = inspectorWindow ?? throw new ArgumentNullException(nameof(inspectorWindow));
        TelemetryWindow = telemetryWindow ?? throw new ArgumentNullException(nameof(telemetryWindow));
        CommandsWindow = commandsWindow ?? throw new ArgumentNullException(nameof(commandsWindow));

        Inventory = new ReadOnlyCollection<ToolWindowDescriptorViewModel>(new[]
        {
            SessionWindow,
            LogVieweWindow,
            HierarchyWindow,
            InspectorWindow,
            TelemetryWindow,
            CommandsWindow,
        });

        _inventoryById = new ReadOnlyDictionary<string, ToolWindowDescriptorViewModel>(
            Inventory.ToDictionary(window => window.Id, StringComparer.Ordinal));
    }

    public ReadOnlyCollection<ToolWindowDescriptorViewModel> Inventory { get; }

    public ToolWindowDescriptorViewModel SessionWindow { get; }

    public ToolWindowDescriptorViewModel LogVieweWindow { get; }

    public ToolWindowDescriptorViewModel HierarchyWindow { get; }

    public ToolWindowDescriptorViewModel InspectorWindow { get; }

    public ToolWindowDescriptorViewModel TelemetryWindow { get; }

    public ToolWindowDescriptorViewModel CommandsWindow { get; }

    /// <summary>
    /// ContentId から runtime tool window を引く。
    /// layout 復元時は saved XML の pane identity と live ViewModel をここで結び直す。
    /// </summary>
    public bool TryGetWindow(string contentId, out ToolWindowDescriptorViewModel? window)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            window = null;
            return false;
        }

        return _inventoryById.TryGetValue(contentId, out window);
    }
}
