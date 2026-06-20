#nullable enable

using System;

namespace DebugStudio.App.Features.Shell;

/// <summary>
/// shell が扱う 1 つの tool window の runtime wrapper。
/// 静的定義と実 ViewModel インスタンスを束ねて AvalonDock へ渡す。
/// </summary>
public sealed class ToolWindowDescriptorViewModel
{
    public ToolWindowDescriptorViewModel(
        ToolWindowDefinition definition,
        object contentViewModel)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        ContentViewModel = contentViewModel ?? throw new ArgumentNullException(nameof(contentViewModel));
    }

    [Obsolete("Use constructor with ToolWindowDefinition. This constructor is maintained for backward compatibility in tests.")]
    public ToolWindowDescriptorViewModel(
        string id,
        string title,
        string description,
        ToolWindowPlacement placement,
        object contentViewModel)
        : this(new ToolWindowDefinition(id, title, description, placement), contentViewModel)
    {
    }

    public ToolWindowDefinition Definition { get; }

    public string Id => Definition.Id;

    public string Title => Definition.Title;

    public string Description => Definition.Description;

    public ToolWindowPlacement Placement => Definition.DefaultPlacement;

    public object ContentViewModel { get; }
}
