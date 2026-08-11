#nullable enable

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
    /// <summary>
    /// kuery 文字列から <c>field.path:</c> 形の参照を緩く拾う。
    /// 完全な KQL パーサではない（引用符内・関数引数・スクリプト等は見ない）。
    /// </summary>
    private static readonly Regex KueryFieldPattern = new(
        @"\b([A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)*)\s*:",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static JsonElement MappingProperties(JsonElement templateRoot)
    {
        return templateRoot.GetProperty("template").GetProperty("mappings").GetProperty("properties");
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
    /// <b>検出できないこと（緩い実装の限界）:</b>
    /// <list type="bullet">
    /// <item><description>キー名が異なる参照（例: <c>fields</c> 配列、<c>accessor</c>、<c>textField</c>）</description></item>
    /// <item><description>ES|QL / 数式文字列の中に埋め込まれたフィールド名</description></item>
    /// <item><description>参照先 data view との対応付け（呼び出し側が mapping 集合を渡す）</description></item>
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

    public static IReadOnlyList<string> CollectKueryFieldReferences(string queryText)
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in KueryFieldPattern.Matches(queryText))
        {
            var name = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(name))
            {
                fields.Add(name);
            }
        }

        return fields.OrderBy(f => f, StringComparer.Ordinal).ToArray();
    }

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
                        if (!string.IsNullOrEmpty(value))
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
