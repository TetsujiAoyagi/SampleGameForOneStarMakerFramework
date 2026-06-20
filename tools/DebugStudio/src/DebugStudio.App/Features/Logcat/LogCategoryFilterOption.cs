#nullable enable

namespace DebugStudio.App.Features.LogViewer;

/// <summary>
/// query bar のカテゴリフィルター候補。
/// kind フィルターと同様に、UI 向けの選択肢として feature 側へ置く。
/// </summary>
public sealed class LogCategoryFilterOption
{
    public LogCategoryFilterOption(string label, string? category)
    {
        Label = label;
        Category = category;
    }

    public string Label { get; }

    public string? Category { get; }
}
