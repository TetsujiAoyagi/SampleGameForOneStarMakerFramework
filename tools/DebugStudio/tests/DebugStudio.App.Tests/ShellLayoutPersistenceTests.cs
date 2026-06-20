#nullable enable

using System;
using System.IO;
using System.Linq;
using AvalonDock.Layout;
using DebugStudio.App.Features.Shell;

namespace DebugStudio.App.Tests;

/// <summary>
/// layout persistence の保存先・互換性判定・default fallback 形状を検証する。
/// AvalonDock の drag/drop 自動化ではなく、保存復元の土台契約だけを固定する。
/// </summary>
public sealed class ShellLayoutPersistenceTests : IDisposable
{
    private readonly string _temporaryDirectoryPath;

    public ShellLayoutPersistenceTests()
    {
        _temporaryDirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "DebugStudio.App.Tests",
            Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void ShellLayoutPersistenceService_既定保存先がLocalAppData配下になる()
    {
        var path = ShellLayoutPersistenceService.CreateDefaultLayoutFilePath();

        Assert.Contains(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            path,
            StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("shell-layout.xml", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellLayoutPersistenceService_保存したXMLを再読込できる()
    {
        var service = new ShellLayoutPersistenceService(GetLayoutFilePath());

        service.SaveLayoutXml("<LayoutRoot />");
        var loaded = service.LoadLayoutXml();

        Assert.Equal("<LayoutRoot />", loaded);
    }

    [Fact]
    public void ShellLayoutPersistenceService_ファイルが無いとnullを返す()
    {
        var service = new ShellLayoutPersistenceService(GetLayoutFilePath());

        Assert.Null(service.LoadLayoutXml());
    }

    [Fact]
    public void ShellLayoutSerializerService_互換レイアウトを受理する()
    {
        var serializerService = new ShellLayoutSerializerService();
        var shellLayout = CreateShellLayout();

        var layoutXml = """
            <LayoutRoot>
              <LayoutPanel>
                <LayoutAnchorablePane>
                  <LayoutAnchorable ContentId="session" />
                  <LayoutAnchorable ContentId="hierarchy" />
                  <LayoutAnchorable ContentId="telemetry" />
                  <LayoutAnchorable ContentId="commands" />
                  <LayoutAnchorable ContentId="inspector" />
                </LayoutAnchorablePane>
                <LayoutDocumentPane>
                  <LayoutDocument ContentId="logviewer" />
                </LayoutDocumentPane>
              </LayoutPanel>
            </LayoutRoot>
            """;

        var result = serializerService.TryValidateLayoutXml(layoutXml, shellLayout, out var reason);

        Assert.True(result);
        Assert.Null(reason);
    }

    [Fact]
    public void ShellLayoutSerializerService_必須ContentId欠落を拒否する()
    {
        var serializerService = new ShellLayoutSerializerService();
        var shellLayout = CreateShellLayout();

        var layoutXml = """
            <LayoutRoot>
              <LayoutPanel>
                <LayoutAnchorablePane>
                  <LayoutAnchorable ContentId="session" />
                  <LayoutAnchorable ContentId="hierarchy" />
                  <LayoutAnchorable ContentId="telemetry" />
                  <LayoutAnchorable ContentId="commands" />
                </LayoutAnchorablePane>
                <LayoutDocumentPane>
                  <LayoutDocument ContentId="logviewer" />
                </LayoutDocumentPane>
              </LayoutPanel>
            </LayoutRoot>
            """;

        var result = serializerService.TryValidateLayoutXml(layoutXml, shellLayout, out var reason);

        Assert.False(result);
        Assert.Contains("inspector", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellLayoutSerializerService_未知ContentIdを拒否する()
    {
        var serializerService = new ShellLayoutSerializerService();
        var shellLayout = CreateShellLayout();

        var layoutXml = """
            <LayoutRoot>
              <LayoutPanel>
                <LayoutAnchorablePane>
                  <LayoutAnchorable ContentId="session" />
                  <LayoutAnchorable ContentId="hierarchy" />
                  <LayoutAnchorable ContentId="telemetry" />
                  <LayoutAnchorable ContentId="commands" />
                  <LayoutAnchorable ContentId="inspector" />
                  <LayoutAnchorable ContentId="ghost" />
                </LayoutAnchorablePane>
                <LayoutDocumentPane>
                  <LayoutDocument ContentId="logviewer" />
                </LayoutDocumentPane>
              </LayoutPanel>
            </LayoutRoot>
            """;

        var result = serializerService.TryValidateLayoutXml(layoutXml, shellLayout, out var reason);

        Assert.False(result);
        Assert.Contains("ghost", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellLayoutSerializerService_defaultLayoutが現在の6窓を再構築する()
    {
        var serializerService = new ShellLayoutSerializerService();
        var shellLayout = CreateShellLayout();

        var layout = serializerService.CreateDefaultLayout(shellLayout);
        var contentIds = layout.Descendents()
            .OfType<LayoutContent>()
            .Select(content => content.ContentId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "commands", "hierarchy", "inspector", "logviewer", "session", "telemetry" },
            contentIds);
    }

    [Fact]
    public void ShellLayoutSerializerService_defaultLayoutでLogviewerだけがDocumentになる()
    {
        var serializerService = new ShellLayoutSerializerService();
        var shellLayout = CreateShellLayout();

        var layout = serializerService.CreateDefaultLayout(shellLayout);
        var documents = layout.Descendents().OfType<LayoutDocument>().Select(document => document.ContentId).ToArray();
        var anchorables = layout.Descendents().OfType<LayoutAnchorable>().Select(anchorable => anchorable.ContentId).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        Assert.Equal(new[] { "logviewer" }, documents);
        Assert.Equal(new[] { "commands", "hierarchy", "inspector", "session", "telemetry" }, anchorables);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectoryPath))
        {
            Directory.Delete(_temporaryDirectoryPath, recursive: true);
        }
    }

    private string GetLayoutFilePath()
    {
        return Path.Combine(_temporaryDirectoryPath, "shell-layout.xml");
    }

    private static ShellLayoutViewModel CreateShellLayout()
    {
        return new ShellLayoutViewModel(
            CreateDescriptor(ShellLayoutDefinitions.Session),
            CreateDescriptor(ShellLayoutDefinitions.LogViewer),
            CreateDescriptor(ShellLayoutDefinitions.Hierarchy),
            CreateDescriptor(ShellLayoutDefinitions.Inspector),
            CreateDescriptor(ShellLayoutDefinitions.Telemetry),
            CreateDescriptor(ShellLayoutDefinitions.Commands));
    }

    private static ToolWindowDescriptorViewModel CreateDescriptor(ToolWindowDefinition definition)
    {
        return new ToolWindowDescriptorViewModel(definition, new object());
    }
}
