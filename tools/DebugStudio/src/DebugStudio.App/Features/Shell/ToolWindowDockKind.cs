#nullable enable

namespace DebugStudio.App.Features.Shell;

/// <summary>
/// AvalonDock 上での tool window の基本種別。
/// document は中央 document pane、anchorable は左右下の dockable pane を表す。
/// </summary>
public enum ToolWindowDockKind
{
    Anchorable,
    Document,
}
