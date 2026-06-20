#nullable enable

using System;
using System.IO;
using System.Text;

namespace DebugStudio.App.Features.Shell;

/// <summary>
/// shell layout XML の保存先解決と file I/O を担当する。
///
/// <para>
/// layout persistence の責務を Window や ViewModel から外し、
/// 「どこへ保存するか」「読めない時にどう degrade するか」をここへ閉じ込める。
/// </para>
/// <para>
/// ここでは I/O 失敗を上位へ投げ直さず、null / no-op へ正規化する。
/// 理由は、layout 破損や保存失敗でアプリ本体の起動・終了経路を壊さないため。
/// </para>
/// </summary>
public sealed class ShellLayoutPersistenceService
{
    public ShellLayoutPersistenceService(string layoutFilePath)
    {
        LayoutFilePath = string.IsNullOrWhiteSpace(layoutFilePath)
            ? throw new ArgumentException("Value cannot be null or whitespace.", nameof(layoutFilePath))
            : layoutFilePath;
    }

    public string LayoutFilePath { get; }

    /// <summary>
    /// user ごとの local app data 配下に保存先を解決する。
    /// roaming ではなく local を使うのは、machine 固有の画面構成を前提にするため。
    /// </summary>
    public static string CreateDefaultLayoutFilePath()
    {
        var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppDataPath, "DebugStudio", "shell-layout.xml");
    }

    /// <summary>
    /// 保存済み layout XML を読む。
    /// 初回起動・アクセス拒否・破損ファイルなどでは null を返し、上位で default fallback させる。
    /// </summary>
    public string? LoadLayoutXml()
    {
        try
        {
            if (!File.Exists(LayoutFilePath))
            {
                return null;
            }

            return File.ReadAllText(LayoutFilePath, Encoding.UTF8);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// 現在の layout XML を保存する。
    /// 保存失敗は user 操作の継続性を壊さないために no-op とし、
    /// 次回起動時に default layout へ戻るだけに留める。
    /// </summary>
    public void SaveLayoutXml(string layoutXml)
    {
        ArgumentNullException.ThrowIfNull(layoutXml);

        try
        {
            var directoryPath = Path.GetDirectoryName(LayoutFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(LayoutFilePath, layoutXml, Encoding.UTF8);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
