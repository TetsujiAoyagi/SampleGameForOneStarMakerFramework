#nullable enable

using System.Text;
using System.Text.Json;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Writers;

/// <summary>
/// log export record の NDJSON 1 行 serialization を共通化する。
/// 手動 export と rolling persistence で同じ JSON shape を保つための単一正本。
/// </summary>
internal static class NdjsonLogRecordSerializer
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

    internal static string Serialize(LogExportRecord record)
    {
        return JsonSerializer.Serialize(record, SerializerOptions);
    }
}
