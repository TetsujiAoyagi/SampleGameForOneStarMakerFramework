#nullable enable

namespace DebugStudio.App.Features.Shell;

/// <summary>
/// shell 上での tool window のおおまかな停泊位置。
/// 本 wave では固定レーンだが、後続で docking へ進むときの基準値にする。
/// </summary>
public enum ToolWindowPlacement
{
    Left,
    Center,
    Right,
    Bottom,
}
