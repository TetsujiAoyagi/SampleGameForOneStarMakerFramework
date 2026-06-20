#nullable enable

using System.Collections.ObjectModel;

namespace DebugStudio.App.Features.Shell;

/// <summary>
/// shell 全体の静的な tool window 定義のレジストリ。
/// runtime composition とは分離し、将来の layout 永続化で使用する定義を保持。
/// </summary>
public static class ShellLayoutDefinitions
{
    public static ToolWindowDefinition Session { get; } = new ToolWindowDefinition(
        "session",
        "Session",
        "接続状態 / capability / recent activity をまとめる session tool window。",
        ToolWindowPlacement.Left,
        defaultDockKind: ToolWindowDockKind.Anchorable,
        defaultGroupKey: "left-stack",
        defaultOrder: 0,
        canClose: false,
        canHide: false);

    public static ToolWindowDefinition LogViewer { get; } = new ToolWindowDefinition(
        "logviewer",
        "LogViewer",
        "受信ログを検索・tail・detail 調査する中央の作業面。",
        ToolWindowPlacement.Center,
        defaultDockKind: ToolWindowDockKind.Document,
        defaultGroupKey: "center-documents",
        defaultOrder: 0,
        canClose: false,
        canHide: false);

    public static ToolWindowDefinition Hierarchy { get; } = new ToolWindowDefinition(
        "hierarchy",
        "Hierarchy",
        "Unity hierarchy snapshot を扱う navigation rail。",
        ToolWindowPlacement.Left,
        defaultDockKind: ToolWindowDockKind.Anchorable,
        defaultGroupKey: "left-stack",
        defaultOrder: 1,
        canClose: false,
        canHide: false);

    public static ToolWindowDefinition Inspector { get; } = new ToolWindowDefinition(
        "inspector",
        "Inspector",
        "選択 node の detail を読む inspector rail。",
        ToolWindowPlacement.Right,
        defaultDockKind: ToolWindowDockKind.Anchorable,
        defaultGroupKey: "right-stack",
        defaultOrder: 0,
        canClose: false,
        canHide: false);

    public static ToolWindowDefinition Telemetry { get; } = new ToolWindowDefinition(
        "telemetry",
        "Telemetry",
        "telemetry / service status の現在地を表示する support window。",
        ToolWindowPlacement.Bottom,
        defaultDockKind: ToolWindowDockKind.Anchorable,
        defaultGroupKey: "bottom-stack",
        defaultOrder: 0,
        canClose: false,
        canHide: false);

    public static ToolWindowDefinition Commands { get; } = new ToolWindowDefinition(
        "commands",
        "Commands",
        "command dispatch foundation と result history を育てる support window。",
        ToolWindowPlacement.Bottom,
        defaultDockKind: ToolWindowDockKind.Anchorable,
        defaultGroupKey: "bottom-stack",
        defaultOrder: 1,
        canClose: false,
        canHide: false);

    /// <summary>
    /// shell が必ず持つ core tool window 定義の一覧。
    /// layout XML 検証ではこの集合と saved layout の ContentId 集合が一致することを要求する。
    /// </summary>
    public static ReadOnlyCollection<ToolWindowDefinition> All { get; } = new(
        new[]
        {
            Session,
            LogViewer,
            Hierarchy,
            Inspector,
            Telemetry,
            Commands,
        });
}
