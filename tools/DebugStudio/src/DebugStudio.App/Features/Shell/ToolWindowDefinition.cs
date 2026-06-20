#nullable enable

using System;

namespace DebugStudio.App.Features.Shell;

/// <summary>
/// 静的な tool window の定義。
/// runtime の ViewModel instance とは独立し、layout 永続化時の識別子と初期配置を保持。
/// </summary>
public sealed class ToolWindowDefinition
{
    public ToolWindowDefinition(
        string id,
        string title,
        string description,
        ToolWindowPlacement defaultPlacement,
        ToolWindowDockKind defaultDockKind = ToolWindowDockKind.Anchorable,
        string? defaultGroupKey = null,
        int defaultOrder = 0,
        bool canClose = false,
        bool canHide = false)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Value cannot be null or whitespace.", nameof(id)) : id;
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Value cannot be null or whitespace.", nameof(title)) : title;
        Description = description ?? string.Empty;
        DefaultPlacement = defaultPlacement;
        DefaultDockKind = defaultDockKind;
        DefaultGroupKey = string.IsNullOrWhiteSpace(defaultGroupKey)
            ? defaultPlacement.ToString().ToLowerInvariant()
            : defaultGroupKey;
        DefaultOrder = defaultOrder;
        CanClose = canClose;
        CanHide = canHide;
    }

    public string Id { get; }

    public string Title { get; }

    public string Description { get; }

    public ToolWindowPlacement DefaultPlacement { get; }

    /// <summary>
    /// default layout fallback で document と anchorable を切り分ける。
    /// </summary>
    public ToolWindowDockKind DefaultDockKind { get; }

    /// <summary>
    /// 同じ placement 内でもどの pane/group に属するかを識別する。
    /// layout 復元失敗時に現在の UX へ戻すための静的キー。
    /// </summary>
    public string DefaultGroupKey { get; }

    /// <summary>
    /// 同一 group 内での並び順。
    /// </summary>
    public int DefaultOrder { get; }

    /// <summary>
    /// tool window を閉じて inventory から消せるかどうか。
    /// 現状は layout persistence 実装中のため原則 false を維持する。
    /// </summary>
    public bool CanClose { get; }

    /// <summary>
    /// anchorable を hide 可能にするかどうか。
    /// 初期 wave では pane 消失事故を避けるため false を採用する。
    /// </summary>
    public bool CanHide { get; }
}
