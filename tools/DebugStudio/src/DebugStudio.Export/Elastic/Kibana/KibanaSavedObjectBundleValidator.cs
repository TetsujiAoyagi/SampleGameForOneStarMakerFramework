#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DebugStudio.Export.Elastic.Kibana;

/// <summary>
/// Kibana saved object bundle の構造検算（V1〜V10, V12）。IO 無しの純関数。
/// V11（index template との mapping 照合）は template を要するためテスト側にある。
/// </summary>
public static class KibanaSavedObjectBundleValidator
{
    public static IReadOnlyList<KibanaSavedObjectValidationIssue> Validate(KibanaSavedObjectBundle bundle)
    {
        if (bundle is null)
        {
            throw new ArgumentNullException(nameof(bundle));
        }

        var issues = new List<KibanaSavedObjectValidationIssue>();
        ValidateV1(bundle, issues);
        ValidateV2(bundle, issues);
        Validation.PanelReferenceRules.Validate(bundle, issues);
        ValidateV5AndV8(bundle, issues);
        ValidateV6V9AndV10(bundle, issues);
        Validation.EsqlPanelRules.Validate(bundle, issues);
        return issues;
    }

    private static void ValidateV1(KibanaSavedObjectBundle bundle, List<KibanaSavedObjectValidationIssue> issues)
    {
        foreach (var obj in bundle.Objects)
        {
            if (!string.IsNullOrEmpty(obj.Id) && !string.IsNullOrEmpty(obj.Type))
            {
                continue;
            }

            var reason = obj.IsParseFailure
                ? "JSON として parse できない行"
                : "type または id が空";
            issues.Add(new KibanaSavedObjectValidationIssue(
                "V1",
                obj.LineNumber,
                obj.Id,
                $"行 {obj.LineNumber} (id='{obj.Id}'): V1 — {reason}。全行は空でない type と id を持つ必要がある。"));
        }
    }

    private static void ValidateV2(KibanaSavedObjectBundle bundle, List<KibanaSavedObjectValidationIssue> issues)
    {
        var seen = new Dictionary<string, KibanaSavedObject>(StringComparer.Ordinal);
        foreach (var obj in bundle.Objects)
        {
            if (string.IsNullOrEmpty(obj.Id))
            {
                continue;
            }

            if (seen.TryGetValue(obj.Id, out var first))
            {
                issues.Add(new KibanaSavedObjectValidationIssue(
                    "V2",
                    obj.LineNumber,
                    obj.Id,
                    $"行 {obj.LineNumber} (id='{obj.Id}'): V2 — id が行 {first.LineNumber} と重複している。"));
            }
            else
            {
                seen[obj.Id] = obj;
            }
        }
    }

    private static void ValidateV5AndV8(KibanaSavedObjectBundle bundle, List<KibanaSavedObjectValidationIssue> issues)
    {
        foreach (var obj in bundle.Objects)
        {
            foreach (var reference in obj.References)
            {
                if (string.IsNullOrEmpty(reference.Id))
                {
                    continue;
                }

                if (!bundle.TryGetById(reference.Id, out var target))
                {
                    issues.Add(CreateIssue(
                        "V5",
                        obj,
                        $"references の id '{reference.Id}' (name='{reference.Name}') が bundle 内に存在しない。"));
                    continue;
                }

                // V8: id は見つかったが type が食い違う場合。V5 とは別ルール。
                if (!string.IsNullOrEmpty(reference.Type)
                    && !string.Equals(reference.Type, target.Type, StringComparison.Ordinal))
                {
                    issues.Add(CreateIssue(
                        "V8",
                        obj,
                        $"references の id '{reference.Id}' (name='{reference.Name}') の type '{reference.Type}' が参照先の type '{target.Type}' と一致しない。"));
                }
            }
        }
    }

    private static void ValidateV6V9AndV10(KibanaSavedObjectBundle bundle, List<KibanaSavedObjectValidationIssue> issues)
    {
        foreach (var obj in bundle.Objects)
        {
            if (!string.Equals(obj.Type, "search", StringComparison.Ordinal))
            {
                continue;
            }

            if (!obj.Attributes.TryGetProperty("kibanaSavedObjectMeta", out var meta)
                || !meta.TryGetProperty("searchSourceJSON", out var searchSourceProp)
                || searchSourceProp.ValueKind != JsonValueKind.String)
            {
                issues.Add(CreateIssue(
                    "V9",
                    obj,
                    "attributes.kibanaSavedObjectMeta.searchSourceJSON が文字列として存在しない。"));
            }

            // V10: sort は必須かつ配列。欠如も赤（文字列に戻ると V6 の sort 走査が消える穴を塞ぐ）。
            if (!obj.Attributes.TryGetProperty("sort", out var sortProp))
            {
                issues.Add(CreateIssue("V10", obj, "attributes.sort が無い。"));
            }
            else if (sortProp.ValueKind != JsonValueKind.Array)
            {
                issues.Add(CreateIssue("V10", obj, "attributes.sort が配列ではない。"));
            }
            else
            {
                foreach (var sortEntry in sortProp.EnumerateArray())
                {
                    if (sortEntry.ValueKind != JsonValueKind.Array || sortEntry.GetArrayLength() < 1)
                    {
                        continue;
                    }

                    var field = sortEntry[0];
                    if (field.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var fieldName = field.GetString() ?? string.Empty;
                    if (DeprecatedFieldCatalog.Contains(fieldName))
                    {
                        issues.Add(CreateIssue("V6", obj, $"sort に deprecated フィールド '{fieldName}' が含まれている。"));
                    }
                }
            }

            if (obj.Attributes.TryGetProperty("columns", out var columnsProp)
                && columnsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var column in columnsProp.EnumerateArray())
                {
                    if (column.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var columnName = column.GetString() ?? string.Empty;
                    if (DeprecatedFieldCatalog.Contains(columnName))
                    {
                        issues.Add(CreateIssue("V6", obj, $"columns に deprecated フィールド '{columnName}' が含まれている。"));
                    }
                }
            }

            var queryText = TryGetSearchSourceQuery(obj.Attributes);
            if (queryText is not null && DeprecatedFieldCatalog.TryFindInQuery(queryText, out var matched))
            {
                issues.Add(CreateIssue(
                    "V6",
                    obj,
                    $"searchSourceJSON の query に deprecated フィールド '{matched}' が含まれている。"));
            }
        }
    }

    private static string? TryGetSearchSourceQuery(JsonElement attributes)
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

    private static KibanaSavedObjectValidationIssue CreateIssue(
        string ruleId,
        KibanaSavedObject obj,
        string detail)
    {
        return new KibanaSavedObjectValidationIssue(
            ruleId,
            obj.LineNumber,
            obj.Id,
            $"行 {obj.LineNumber} (id='{obj.Id}'): {ruleId} — {detail}");
    }
}
