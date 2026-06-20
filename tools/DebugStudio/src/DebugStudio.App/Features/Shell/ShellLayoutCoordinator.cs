#nullable enable

using System;
using AvalonDock;

namespace DebugStudio.App.Features.Shell;

/// <summary>
/// DockingManager・shell inventory・layout persistence を結び付ける orchestrator。
///
/// <para>
/// MainWindow が直接 serializer や file I/O の詳細を知らないようにし、
/// 起動時 restore / 終了時 save の順序制御だけを 1 箇所へまとめる。
/// </para>
/// <para>
/// 失敗時は常に default layout へ戻す。
/// 「保存済み XML が壊れているせいで起動できない」状態を作らないことを最優先にする。
/// </para>
/// </summary>
public sealed class ShellLayoutCoordinator
{
    private readonly DockingManager _dockingManager;
    private readonly ShellLayoutViewModel _shellLayout;
    private readonly ShellLayoutPersistenceService _persistenceService;
    private readonly ShellLayoutSerializerService _serializerService;

    public ShellLayoutCoordinator(
        DockingManager dockingManager,
        ShellLayoutViewModel shellLayout,
        ShellLayoutPersistenceService persistenceService,
        ShellLayoutSerializerService serializerService)
    {
        _dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));
        _shellLayout = shellLayout ?? throw new ArgumentNullException(nameof(shellLayout));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        _serializerService = serializerService ?? throw new ArgumentNullException(nameof(serializerService));
    }

    /// <summary>
    /// 保存済み layout が有効なら復元し、そうでなければ default layout を適用する。
    /// validation と deserialize の 2 段階で安全性を確保する。
    /// </summary>
    public void RestoreLayout()
    {
        var layoutXml = _persistenceService.LoadLayoutXml();
        if (string.IsNullOrWhiteSpace(layoutXml))
        {
            ApplyDefaultLayout();
            return;
        }

        if (!_serializerService.TryValidateLayoutXml(layoutXml, _shellLayout, out _))
        {
            ApplyDefaultLayout();
            return;
        }

        if (!_serializerService.TryDeserializeLayout(_dockingManager, _shellLayout, layoutXml))
        {
            ApplyDefaultLayout();
        }
    }

    /// <summary>
    /// 現在の layout を保存する。
    /// serializer / file I/O 失敗は下位層で degrade し、終了経路は継続する。
    /// </summary>
    public void SaveLayout()
    {
        var layoutXml = _serializerService.SerializeLayout(_dockingManager);
        _persistenceService.SaveLayoutXml(layoutXml);
    }

    private void ApplyDefaultLayout()
    {
        _dockingManager.Layout = _serializerService.CreateDefaultLayout(_shellLayout);
    }
}
