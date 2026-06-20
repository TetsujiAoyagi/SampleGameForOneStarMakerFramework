#nullable enable

using DebugStudio.Contracts.Schema;

namespace DebugStudio.App.Features.LogViewer;

/// <summary>
/// query bar の kind フィルター候補。
/// transport schema そのものではなく UI 向け選択肢なので ViewModel 側へ置く。
/// </summary>
public sealed class LogKindFilterOption
{
    public LogKindFilterOption(string label, LogEntryKind? kind)
    {
        Label = label;
        Kind = kind;
    }

    public string Label { get; }

    public LogEntryKind? Kind { get; }
}
