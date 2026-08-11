#nullable enable

using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DebugStudio.Export.Elastic.Kibana;

namespace DebugStudio.Export.Tests.Elastic.Kibana;

/// <summary>
/// index template の mapping パス収集と、saved object 側のフィールド参照抽出。
/// production に昇格させない（HANDOFF §3.5 P-5）。テスト内の共有ヘルパ。
/// </summary>
internal static class IndexTemplateFieldMappingHelper
{
    public const string TelemetryDataViewId = "debugstudio-telemetry-dataview";
    public const string LogDataViewId = "debugstudio-log-dataview";

    /// <summary>
    /// kuery 文字列から <c>field.path:</c> 形の参照を緩く拾う。
    /// 完全な KQL パーサではない（関数引数・スクリプト等は見ない）。
    /// 引用符内は <see cref="StripDoubleQuotedSegments"/> で照合前に除去する。
    /// </summary>
    private static readonly Regex KueryFieldPattern = new(
        @"\b([A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)*)\s*:",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// ダブルクォート文字列を空白に置換する（簡易。エスケープされた <c>\"</c> は考慮する）。
    /// </summary>
    private static readonly Regex DoubleQuotedSegmentPattern = new(
        "\"(?:\\\\.|[^\"\\\\])*\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Lens の <c>sourceField</c> / <c>field</c> に現れるが、index template には mapping されない
    /// Kibana 内部 sentinel。列挙したものだけ除外する（「mapping に無いものを全部見逃す」はしない）。
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <c>___records___</c> — Lens の count（Count of records）が使う擬似フィールド
    /// （Kibana <c>DOCUMENT_FIELD_NAME</c>）。ドキュメント数を数えるための値で、
    /// どの index mapping にも存在しない。除外しないと正当な count metric が赤になる。
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    private static readonly HashSet<string> LensMappingExcludedSentinels = new(StringComparer.Ordinal)
    {
        "___records___",
    };

    public static JsonElement MappingProperties(JsonElement templateRoot)
    {
        return templateRoot.GetProperty("template").GetProperty("mappings").GetProperty("properties");
    }

    /// <summary>
    /// saved object の <c>references</c> にある index-pattern id から、照合する mapping 集合を選ぶ。
    /// <list type="bullet">
    /// <item><description><c>debugstudio-telemetry-dataview</c> → telemetry template</description></item>
    /// <item><description><c>debugstudio-log-dataview</c> → log template</description></item>
    /// </list>
    /// どちらでもない / index-pattern 参照が無い / 複数の異なる data view を指す場合は
    /// <c>false</c>（呼び出し側は赤にする）。黙って通すと log 側フィールドを telemetry mapping と
    /// 照合して検算が嘘になるため、不明は失敗とする。
    /// </summary>
    public static bool TryResolveMappedFieldPaths(
        KibanaSavedObject obj,
        HashSet<string> telemetryMapped,
        HashSet<string> logMapped,
        out HashSet<string>? mapped,
        out string failureReason)
    {
        mapped = null;
        failureReason = string.Empty;

        var dataViewIds = obj.References
            .Where(r => string.Equals(r.Type, "index-pattern", StringComparison.Ordinal))
            .Select(r => r.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (dataViewIds.Length == 0)
        {
            failureReason =
                $"{obj.Type} '{obj.Id}' に index-pattern 参照が無いため、照合先 mapping を選べない。";
            return false;
        }

        if (dataViewIds.Length > 1)
        {
            failureReason =
                $"{obj.Type} '{obj.Id}' が複数の index-pattern を参照している"
                + $" ({string.Join(", ", dataViewIds)})。照合先を一意に決められない。";
            return false;
        }

        var dataViewId = dataViewIds[0];
        if (string.Equals(dataViewId, TelemetryDataViewId, StringComparison.Ordinal))
        {
            mapped = telemetryMapped;
            return true;
        }

        if (string.Equals(dataViewId, LogDataViewId, StringComparison.Ordinal))
        {
            mapped = logMapped;
            return true;
        }

        failureReason =
            $"{obj.Type} '{obj.Id}' の index-pattern 参照 '{dataViewId}' は既知の data view ではない"
            + $"（{TelemetryDataViewId} / {LogDataViewId}）。";
        return false;
    }

    /// <summary>
    /// mappings.properties を再帰的に辿り、葉フィールドをドット区切りのパスとして集める。
    /// </summary>
    public static HashSet<string> CollectMappedFieldPaths(JsonElement properties, string prefix = "")
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in properties.EnumerateObject())
        {
            var path = prefix.Length == 0 ? property.Name : prefix + "." + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object
                && property.Value.TryGetProperty("properties", out var nested))
            {
                paths.UnionWith(CollectMappedFieldPaths(nested, path));
            }
            else
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    /// <summary>
    /// lens の <c>attributes.state</c> からフィールド参照を緩く集める（V11）。
    ///
    /// <para>
    /// <b>検出できること:</b> JSON を再帰走査し、キー名が <c>sourceField</c> / <c>field</c> の
    /// 文字列値を集める。state が JSON 文字列の場合も一度 parse してから走査する。
    /// </para>
    /// <para>
    /// <b>除外する sentinel（<see cref="LensMappingExcludedSentinels"/>）:</b>
    /// <c>___records___</c> のみ。Lens count metric の擬似フィールドで mapping に存在しないため、
    /// 照合対象から外す。列挙以外の未 mapping 値は引き続き赤にする。
    /// </para>
    /// <para>
    /// <b>検出できないこと（緩い実装の限界）:</b>
    /// <list type="bullet">
    /// <item><description>キー名が異なる参照（例: <c>fields</c> 配列、<c>accessor</c>、<c>textField</c>）</description></item>
    /// <item><description>ES|QL / 数式文字列の中に埋め込まれたフィールド名</description></item>
    /// <item><description>Kibana バージョン固有の state スキーマの妥当性そのもの</description></item>
    /// </list>
    /// したがって「これで lens の全フィールド参照を見ている」とは言えない。
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> CollectLensReferencedFields(KibanaSavedObject lens)
    {
        if (!string.Equals(lens.Type, "lens", StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        if (!lens.Attributes.TryGetProperty("state", out var stateProp))
        {
            return Array.Empty<string>();
        }

        var fields = new HashSet<string>(StringComparer.Ordinal);
        if (stateProp.ValueKind == JsonValueKind.String)
        {
            var stateText = stateProp.GetString();
            if (string.IsNullOrEmpty(stateText))
            {
                return Array.Empty<string>();
            }

            try
            {
                using var doc = JsonDocument.Parse(stateText);
                CollectFieldKeyedStrings(doc.RootElement, fields);
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }
        else if (stateProp.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            CollectFieldKeyedStrings(stateProp, fields);
        }

        return fields.OrderBy(f => f, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// searchSourceJSON の kuery 文字列からフィールド参照を緩く集める。
    /// columns 検算が query を見ていなかった穴（V11 拡張）を塞ぐ。
    /// </summary>
    public static IReadOnlyList<string> CollectSearchSourceQueryFields(KibanaSavedObject search)
    {
        var queryText = TryGetSearchSourceQuery(search.Attributes);
        if (string.IsNullOrEmpty(queryText))
        {
            return Array.Empty<string>();
        }

        return CollectKueryFieldReferences(queryText);
    }

    /// <summary>
    /// kuery から <c>field:</c> 形の参照を緩く集める。
    /// 照合前にダブルクォート文字列を除去し、値内の <c>"a:b"</c> をフィールド名と誤認しない
    /// （doc どおり「引用符内は見ない」）。単一引用符・ネストした複雑なエスケープは対象外。
    /// </summary>
    public static IReadOnlyList<string> CollectKueryFieldReferences(string queryText)
    {
        var scrubbed = StripDoubleQuotedSegments(queryText);
        var fields = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in KueryFieldPattern.Matches(scrubbed))
        {
            var name = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(name))
            {
                fields.Add(name);
            }
        }

        return fields.OrderBy(f => f, StringComparer.Ordinal).ToArray();
    }

    private static string StripDoubleQuotedSegments(string queryText) =>
        DoubleQuotedSegmentPattern.Replace(queryText, " ");

    public static string? TryGetSearchSourceQuery(JsonElement attributes)
    {
        if (!attributes.TryGetProperty("kibanaSavedObjectMeta", out var meta)
            || !meta.TryGetProperty("searchSourceJSON", out var searchSourceProp)
            || searchSourceProp.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var searchSourceText = searchSourceProp.GetString();
        if (string.IsNullOrEmpty(searchSourceText))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(searchSourceText);
            if (!doc.RootElement.TryGetProperty("query", out var queryObj)
                || !queryObj.TryGetProperty("query", out var queryTextProp)
                || queryTextProp.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return queryTextProp.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void CollectFieldKeyedStrings(JsonElement element, HashSet<string> fields)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if ((property.NameEquals("sourceField") || property.NameEquals("field"))
                        && property.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = property.Value.GetString();
                        if (!string.IsNullOrEmpty(value)
                            && !LensMappingExcludedSentinels.Contains(value))
                        {
                            fields.Add(value);
                        }
                    }

                    CollectFieldKeyedStrings(property.Value, fields);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectFieldKeyedStrings(item, fields);
                }

                break;
        }
    }
}
