#nullable enable

namespace DebugStudio.Export.Models;

/// <summary>
/// log export の出力形式。
/// UI 選択と writer 解決の共有列挙体。
/// </summary>
public enum LogExportFormat
{
    Ndjson = 0,
    Csv = 1,
    ElasticBulk = 2,
}
