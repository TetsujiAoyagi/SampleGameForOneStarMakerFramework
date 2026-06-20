#nullable enable

namespace DebugStudio.Export.Models;

/// <summary>
/// telemetry export の出力形式。
/// NDJSON は Filebeat 取り込み向け、ElasticBulk は `_bulk` API 投入向けの薄い基盤として使う。
/// </summary>
public enum TelemetryExportFormat
{
    Ndjson = 0,
    ElasticBulk = 1,
}
