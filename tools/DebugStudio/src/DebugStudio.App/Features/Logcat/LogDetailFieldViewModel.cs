#nullable enable

namespace DebugStudio.App.Features.LogViewer;

/// <summary>
/// 選択中 log の詳細ペインで使う、将来拡張しやすい key-value 表現。
/// structured payload が入った場合も、まずは同じ形へ射影すれば UI を保ったまま増築できる。
/// </summary>
public sealed class LogDetailFieldViewModel
{
    public LogDetailFieldViewModel(string label, string value, string hint)
    {
        Label = label;
        Value = value;
        Hint = hint;
    }

    public string Label { get; }

    public string Value { get; }

    public string Hint { get; }
}
