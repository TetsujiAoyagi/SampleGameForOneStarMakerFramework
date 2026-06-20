#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using System.Xml;
using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;

namespace DebugStudio.App.Features.Shell;

/// <summary>
/// AvalonDock layout の default 生成・互換性検証・serialize/deserialize を担当する。
///
/// <para>
/// persistence service が file system の責務を持つのに対し、
/// この型は「saved XML が現在の shell inventory と結び直せるか」を判断する。
/// </para>
/// <para>
/// 重要なのは ContentId と live ViewModel の再接続であり、
/// 復元時は shell inventory を正本として unknown/stale pane を通さない。
/// </para>
/// </summary>
public sealed class ShellLayoutSerializerService
{
    /// <summary>
    /// 現在の static shell 定義から default layout を再構築する。
    /// saved XML が読めない時は必ずこの形へ戻るため、fallback UX の正本になる。
    /// </summary>
    public LayoutRoot CreateDefaultLayout(ShellLayoutViewModel shellLayout)
    {
        ArgumentNullException.ThrowIfNull(shellLayout);

        var rootPanel = new LayoutPanel
        {
            Orientation = Orientation.Horizontal,
        };

        rootPanel.Children.Add(CreateAnchorableGroup(shellLayout, "left-stack", new GridLength(360), isVertical: true));
        rootPanel.Children.Add(CreateCenterPanel(shellLayout));
        rootPanel.Children.Add(CreateAnchorableGroup(shellLayout, "right-stack", new GridLength(360), isVertical: false));

        return new LayoutRoot
        {
            RootPanel = rootPanel,
        };
    }

    /// <summary>
    /// 保存済み XML が現在の shell inventory と一致するかを検証する。
    /// この wave では strict に検証し、1 つでも不一致があれば default fallback へ戻す。
    /// </summary>
    public bool TryValidateLayoutXml(
        string layoutXml,
        ShellLayoutViewModel shellLayout,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(layoutXml);
        ArgumentNullException.ThrowIfNull(shellLayout);

        try
        {
            var document = XDocument.Parse(layoutXml, LoadOptions.None);
            if (!string.Equals(document.Root?.Name.LocalName, "LayoutRoot", StringComparison.Ordinal))
            {
                failureReason = "Root element is not LayoutRoot.";
                return false;
            }

            var actualContentIds = document
                .Descendants()
                .Select(element => element.Attribute("ContentId")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal);

            var expectedContentIds = shellLayout.Inventory
                .Select(window => window.Id)
                .ToHashSet(StringComparer.Ordinal);

            var missingIds = expectedContentIds.Except(actualContentIds, StringComparer.Ordinal).ToArray();
            if (missingIds.Length > 0)
            {
                failureReason = $"Missing ContentId: {string.Join(", ", missingIds)}";
                return false;
            }

            var unknownIds = actualContentIds.Except(expectedContentIds, StringComparer.Ordinal).ToArray();
            if (unknownIds.Length > 0)
            {
                failureReason = $"Unknown ContentId: {string.Join(", ", unknownIds)}";
                return false;
            }

            failureReason = null;
            return true;
        }
        catch (XmlException ex)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 現在の DockingManager layout を XML 化する。
    /// 保存直前の 1 箇所に集約し、serializer API 依存を view 層へ漏らさない。
    /// </summary>
    public string SerializeLayout(DockingManager dockingManager)
    {
        ArgumentNullException.ThrowIfNull(dockingManager);

        var serializer = new XmlLayoutSerializer(dockingManager);
        using var writer = new StringWriter();
        serializer.Serialize(writer);
        return writer.ToString();
    }

    /// <summary>
    /// 保存済み XML から layout を復元する。
    /// callback で ContentId と live ViewModel を結び直し、saved XML 単体では UI を生成させない。
    /// </summary>
    public bool TryDeserializeLayout(
        DockingManager dockingManager,
        ShellLayoutViewModel shellLayout,
        string layoutXml)
    {
        ArgumentNullException.ThrowIfNull(dockingManager);
        ArgumentNullException.ThrowIfNull(shellLayout);
        ArgumentNullException.ThrowIfNull(layoutXml);

        try
        {
            var serializer = new XmlLayoutSerializer(dockingManager);
            serializer.LayoutSerializationCallback += (_, args) => BindContent(shellLayout, args);

            using var reader = new StringReader(layoutXml);
            serializer.Deserialize(reader);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void BindContent(
        ShellLayoutViewModel shellLayout,
        LayoutSerializationCallbackEventArgs args)
    {
        if (!shellLayout.TryGetWindow(args.Model.ContentId, out var descriptor) || descriptor == null)
        {
            args.Cancel = true;
            return;
        }

        args.Content = descriptor.ContentViewModel;
        args.Model.Title = descriptor.Title;

        if (args.Model is LayoutAnchorable anchorable)
        {
            anchorable.CanClose = descriptor.Definition.CanClose;
            anchorable.CanHide = descriptor.Definition.CanHide;
        }
        else if (args.Model is LayoutDocument document)
        {
            document.CanClose = descriptor.Definition.CanClose;
        }
    }

    private static LayoutPanel CreateCenterPanel(ShellLayoutViewModel shellLayout)
    {
        var centerPanel = new LayoutPanel
        {
            Orientation = Orientation.Vertical,
        };

        var documentPaneGroup = new LayoutDocumentPaneGroup();
        var documentPane = new LayoutDocumentPane();
        foreach (var window in GetGroupWindows(shellLayout, "center-documents", ToolWindowDockKind.Document))
        {
            documentPane.Children.Add(new LayoutDocument
            {
                Title = window.Title,
                ContentId = window.Id,
                CanClose = window.Definition.CanClose,
                Content = window.ContentViewModel,
            });
        }

        documentPaneGroup.Children.Add(documentPane);
        centerPanel.Children.Add(documentPaneGroup);
        centerPanel.Children.Add(CreateAnchorableGroup(shellLayout, "bottom-stack", new GridLength(250), isVertical: false, useHeight: true));
        return centerPanel;
    }

    private static LayoutAnchorablePaneGroup CreateAnchorableGroup(
        ShellLayoutViewModel shellLayout,
        string groupKey,
        GridLength dockLength,
        bool isVertical,
        bool useHeight = false)
    {
        var paneGroup = new LayoutAnchorablePaneGroup
        {
            Orientation = isVertical ? Orientation.Vertical : Orientation.Horizontal,
        };

        if (useHeight)
        {
            paneGroup.DockHeight = dockLength;
        }
        else
        {
            paneGroup.DockWidth = dockLength;
        }

        var pane = new LayoutAnchorablePane();
        foreach (var window in GetGroupWindows(shellLayout, groupKey, ToolWindowDockKind.Anchorable))
        {
            pane.Children.Add(new LayoutAnchorable
            {
                Title = window.Title,
                ContentId = window.Id,
                CanClose = window.Definition.CanClose,
                CanHide = window.Definition.CanHide,
                Content = window.ContentViewModel,
            });
        }

        paneGroup.Children.Add(pane);
        return paneGroup;
    }

    private static ToolWindowDescriptorViewModel[] GetGroupWindows(
        ShellLayoutViewModel shellLayout,
        string groupKey,
        ToolWindowDockKind dockKind)
    {
        return shellLayout.Inventory
            .Where(window =>
                window.Definition.DefaultDockKind == dockKind &&
                string.Equals(window.Definition.DefaultGroupKey, groupKey, StringComparison.Ordinal))
            .OrderBy(window => window.Definition.DefaultOrder)
            .ToArray();
    }
}
