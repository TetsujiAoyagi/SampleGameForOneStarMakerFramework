#nullable enable

using System;
using System.IO;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// telemetry 自動永続化の既定出力先を決める policy。
///
/// <para>
/// 手動 Export の Documents 配下パスとは用途が異なるため規約を分離し、
/// 常時 append 用の flat directory を LocalAppData 配下へ置く。
/// Log 永続化と同じ運用契約(rolling NDJSON)に揃える。
/// </para>
/// </summary>
public sealed class TelemetryPersistencePathPolicy
{
    public TelemetryPersistencePathPolicy(string? rootDirectory = null)
    {
        Directory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DebugStudio",
                "telemetry")
            : rootDirectory;
    }

    /// <summary>
    /// rolling writer が NDJSON を書き出す directory。
    /// </summary>
    public string Directory { get; }
}
