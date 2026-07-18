#nullable enable

using System.Text;
using System.Text.Json;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Writers;

/// <summary>
/// telemetry export record の NDJSON 1 行 serialization を共通化する。
///
/// <para>
/// 手動 Export と rolling 自動永続で同一の JSON options / field shape を保つ正本。
/// mapper 側の contract 変更だけでは不十分で、serializer まで分岐すると
/// Elastic-ready schema が時間とともにドリフトするため、ここへ集約する。
/// </para>
/// </summary>
internal static class NdjsonTelemetryRecordSerializer
{
    /// <summary>
    /// NDJSON 出力用 encoding。BOM を先頭へ埋め込むと行指向 consumer が
    /// 1 行目を JSON として parse できなくなるため、BOM なし UTF-8 に固定する。
    /// </summary>
    internal static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    internal static string Serialize(TelemetryExportRecord record)
    {
        return JsonSerializer.Serialize(record, SerializerOptions);
    }
}
