#nullable enable

using System;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// log export の既定出力先を決める policy。
///
/// <para>
/// R3 では「どこへ、どんな名前で書き出すか」を UI の都合から切り離し、
/// 将来の Filebeat / Elastic 連携でも参照できる土台にする。
/// 日付ディレクトリ配下へ timestamp 付きファイルを作ることで、
/// 手動 export でも最低限の rolling しやすさを確保する。
/// </para>
/// </summary>
public sealed class LogExportPathPolicy
{
    private readonly ExportPathPolicy _innerPolicy;

    public LogExportPathPolicy(string? rootDirectory = null)
    {
        _innerPolicy = new ExportPathPolicy(rootDirectory);
    }

    /// <summary>
    /// 現在時刻を埋め込んだ既定 export path を返す。
    /// ディレクトリは日単位で分け、ファイル名は秒単位 timestamp を持たせる。
    /// </summary>
    public string CreateDefaultPath(string extension, DateTimeOffset? now = null)
    {
        return _innerPolicy.CreateDefaultPath("logs", "debugstudio-log", extension, now);
    }

    /// <summary>
    /// 現在の path を保ちながら拡張子だけを差し替える。
    /// path が空なら既定 policy に従った fallback path を返す。
    /// </summary>
    public string UpdateExtension(string currentPath, string extension, DateTimeOffset? now = null)
    {
        return _innerPolicy.UpdateExtension(currentPath, CreateDefaultPath(extension, now), extension);
    }

    public string CreateFallbackPath(string extension, DateTimeOffset? now = null)
    {
        return CreateDefaultPath(extension, now);
    }
}
