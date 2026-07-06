#nullable enable

using System;
using System.IO;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// 自動 log persistence の既定出力先を決める policy。
///
/// <para>
/// 手動 export とは用途が異なるため path 規約を分離し、
/// 常時 append 用の flat directory を LocalAppData 配下へ置く。
/// </para>
/// </summary>
public sealed class LogPersistencePathPolicy
{
    public LogPersistencePathPolicy(string? rootDirectory = null)
    {
        Directory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DebugStudio",
                "logs")
            : rootDirectory;
    }

    /// <summary>
    /// rolling writer が NDJSON を書き出す directory。
    /// </summary>
    public string Directory { get; }
}
