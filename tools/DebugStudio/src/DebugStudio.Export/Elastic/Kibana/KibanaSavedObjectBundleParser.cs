#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace DebugStudio.Export.Elastic.Kibana;

/// <summary>
/// NDJSON 文字列を KibanaSavedObjectBundle へ変換する純関数。IO 無し。
/// JSON として parse できない行は例外にせず、Id/Type 空の parse 失敗オブジェクトとして返す。
/// </summary>
public static class KibanaSavedObjectBundleParser
{
    private static readonly JsonElement EmptyAttributes = JsonDocument.Parse("{}").RootElement.Clone();

    public static KibanaSavedObjectBundle Parse(string ndjson)
    {
        if (ndjson is null)
        {
            throw new ArgumentNullException(nameof(ndjson));
        }

        var objects = new List<KibanaSavedObject>();
        var lineNumber = 0;
        var start = 0;

        while (start <= ndjson.Length)
        {
            var end = ndjson.IndexOf('\n', start);
            string line;
            if (end < 0)
            {
                line = ndjson.Substring(start);
                start = ndjson.Length + 1;
            }
            else
            {
                line = ndjson.Substring(start, end - start);
                start = end + 1;
            }

            lineNumber++;

            if (line.Length > 0 && line[^1] == '\r')
            {
                line = line[..^1];
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            objects.Add(ParseLine(line, lineNumber));
        }

        return new KibanaSavedObjectBundle(objects);
    }

    private static KibanaSavedObject ParseLine(string line, int lineNumber)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            var id = root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
                ? idElement.GetString() ?? string.Empty
                : string.Empty;
            var type = root.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString() ?? string.Empty
                : string.Empty;

            var attributes = root.TryGetProperty("attributes", out var attributesElement)
                ? attributesElement.Clone()
                : EmptyAttributes;

            var references = ParseReferences(root);

            return new KibanaSavedObject(id, type, attributes, references, lineNumber);
        }
        catch (JsonException)
        {
            // parse 失敗は例外にせず空 Id/Type のオブジェクトとして返す。V1 が指摘する。
            return new KibanaSavedObject(
                string.Empty,
                string.Empty,
                EmptyAttributes,
                Array.Empty<KibanaSavedObjectReference>(),
                lineNumber,
                IsParseFailure: true);
        }
    }

    private static IReadOnlyList<KibanaSavedObjectReference> ParseReferences(JsonElement root)
    {
        if (!root.TryGetProperty("references", out var referencesElement)
            || referencesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<KibanaSavedObjectReference>();
        }

        var list = new List<KibanaSavedObjectReference>(referencesElement.GetArrayLength());
        foreach (var item in referencesElement.EnumerateArray())
        {
            var refId = item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                ? idEl.GetString() ?? string.Empty
                : string.Empty;
            var refName = item.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString() ?? string.Empty
                : string.Empty;
            var refType = item.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String
                ? typeEl.GetString() ?? string.Empty
                : string.Empty;
            list.Add(new KibanaSavedObjectReference(refId, refName, refType));
        }

        return list;
    }
}
