#nullable enable

using System;
using System.Globalization;
using System.IO;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// export 出力先の directory / naming policy を決める共通 service。
///
/// <para>
/// R3 では log だけでなく telemetry も Elastic/Filebeat 前提で整え始めるため、
/// path 規約を feature ごとに重複定義せず、ここへ集約する。
/// 日付ディレクトリ + timestamp 付きファイル名を共通ルールにすることで、
/// 手動 export でも rolling しやすい形を保てる。
/// </para>
/// </summary>
public sealed class ExportPathPolicy
{
    private readonly string _rootDirectory;

    public ExportPathPolicy(string? rootDirectory = null)
    {
        _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DebugStudio",
                "exports")
            : rootDirectory;
    }

    public string CreateDefaultPath(string areaName, string baseFileName, string extension, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(areaName))
        {
            throw new ArgumentException("An export area name is required.", nameof(areaName));
        }

        if (string.IsNullOrWhiteSpace(baseFileName))
        {
            throw new ArgumentException("A base file name is required.", nameof(baseFileName));
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("A file extension is required.", nameof(extension));
        }

        var localNow = (now ?? DateTimeOffset.Now).ToLocalTime();
        var dayDirectory = Path.Combine(
            _rootDirectory,
            areaName,
            localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"{baseFileName}-{localNow:yyyyMMdd-HHmmss}{extension}");
        return Path.Combine(dayDirectory, fileName);
    }

    public string UpdateExtension(string currentPath, string fallbackPath, string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("A file extension is required.", nameof(extension));
        }

        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return ReplaceExtension(fallbackPath, extension);
        }

        return ReplaceExtension(currentPath, extension);
    }

    private static string ReplaceExtension(string path, string extension)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A path is required.", nameof(path));
        }

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "debugstudio-export";
        }

        return string.IsNullOrWhiteSpace(directory)
            ? fileName + extension
            : Path.Combine(directory, fileName + extension);
    }
}
