#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DebugStudio.Export.Elastic.Kibana;

/// <summary>
/// Kibana saved object bundle の構造検算（V1〜V6）。IO 無しの純関数。
/// </summary>
public static class KibanaSavedObjectBundleValidator
{
    private static readonly HashSet<string> DeprecatedFields = new(StringComparer.Ordinal)
    {
        "cpuTime",
        "gpuTime",
        "managedMem",
        "nativeMem",
        "cameraTotalViewCount",
        "cameraAdditionalViewCount",
        "cameraBlendingViewCount",
        "cameraMaxStackDepthTotal",
    };

    private static readonly Regex DeprecatedFieldInQuery = new(
        @"(?<![.\w])(cpuTime|gpuTime|managedMem|nativeMem|cameraTotalViewCount|cameraAdditionalViewCount|cameraBlendingViewCount|cameraMaxStackDepthTotal)(?![\w])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<KibanaSavedObjectValidationIssue> Validate(KibanaSavedObjectBundle bundle)
    {
        if (bundle is null)
        {
            throw new ArgumentNullException(nameof(bundle));
        }

        var issues = new List<KibanaSavedObjectValidationIssue>();
        ValidateV1(bundle, issues);
        ValidateV2(bundle, issues);
        ValidateV3AndV4(bundle, issues);
        ValidateV5(bundle, issues);
        ValidateV6(bundle, issues);
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

    private static void ValidateV3AndV4(KibanaSavedObjectBundle bundle, List<KibanaSavedObjectValidationIssue> issues)
    {
        foreach (var obj in bundle.Objects)
        {
            if (!string.Equals(obj.Type, "dashboard", StringComparison.Ordinal))
            {
                continue;
            }

            if (!obj.Attributes.TryGetProperty("panelsJSON", out var panelsJsonProp)
                || panelsJsonProp.ValueKind != JsonValueKind.String)
            {
                issues.Add(new KibanaSavedObjectValidationIssue(
                    "V3",
                    obj.LineNumber,
                    obj.Id,
                    $"行 {obj.LineNumber} (id='{obj.Id}'): V3 — attributes.panelsJSON が文字列として存在しない。"));
                continue;
            }

            var panelsJsonText = panelsJsonProp.GetString() ?? string.Empty;
            JsonElement panelsArray;
            try
            {
                using var panelsDoc = JsonDocument.Parse(panelsJsonText);
                panelsArray = panelsDoc.RootElement.Clone();
            }
            catch (JsonException)
            {
                issues.Add(new KibanaSavedObjectValidationIssue(
                    "V3",
                    obj.LineNumber,
                    obj.Id,
                    $"行 {obj.LineNumber} (id='{obj.Id}'): V3 — panelsJSON が JSON として parse できない。"));
                continue;
            }

            if (panelsArray.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new KibanaSavedObjectValidationIssue(
                    "V3",
                    obj.LineNumber,
                    obj.Id,
                    $"行 {obj.LineNumber} (id='{obj.Id}'): V3 — panelsJSON が JSON 配列ではない。"));
                continue;
            }

            if (panelsArray.GetArrayLength() < 1)
            {
                issues.Add(new KibanaSavedObjectValidationIssue(
                    "V3",
                    obj.LineNumber,
                    obj.Id,
                    $"行 {obj.LineNumber} (id='{obj.Id}'): V3 — panelsJSON の要素数が 0。パネルが 1 枚以上必要。"));
            }

            var panelRefNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var panel in panelsArray.EnumerateArray())
            {
                if (panel.TryGetProperty("panelRefName", out var panelRefNameProp)
                    && panelRefNameProp.ValueKind == JsonValueKind.String)
                {
                    var name = panelRefNameProp.GetString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        panelRefNames.Add(name);
                    }
                }
            }

            var referencePanelNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reference in obj.References)
            {
                if (reference.Name.StartsWith("panel_", StringComparison.Ordinal))
                {
                    referencePanelNames.Add(reference.Name);
                }
            }

            foreach (var name in panelRefNames.Where(n => !referencePanelNames.Contains(n)))
            {
                issues.Add(new KibanaSavedObjectValidationIssue(
                    "V4",
                    obj.LineNumber,
                    obj.Id,
                    $"行 {obj.LineNumber} (id='{obj.Id}'): V4 — panelsJSON の panelRefName '{name}' に対応する references が無い。"));
            }

            foreach (var name in referencePanelNames.Where(n => !panelRefNames.Contains(n)))
            {
                issues.Add(new KibanaSavedObjectValidationIssue(
                    "V4",
                    obj.LineNumber,
                    obj.Id,
                    $"行 {obj.LineNumber} (id='{obj.Id}'): V4 — references の '{name}' が panelsJSON から参照されていない。"));
            }
        }
    }

    private static void ValidateV5(KibanaSavedObjectBundle bundle, List<KibanaSavedObjectValidationIssue> issues)
    {
        foreach (var obj in bundle.Objects)
        {
            foreach (var reference in obj.References)
            {
                if (string.IsNullOrEmpty(reference.Id))
                {
                    continue;
                }

                if (!bundle.TryGetById(reference.Id, out _))
                {
                    issues.Add(new KibanaSavedObjectValidationIssue(
                        "V5",
                        obj.LineNumber,
                        obj.Id,
                        $"行 {obj.LineNumber} (id='{obj.Id}'): V5 — references の id '{reference.Id}' (name='{reference.Name}') が bundle 内に存在しない。"));
                }
            }
        }
    }

    private static void ValidateV6(KibanaSavedObjectBundle bundle, List<KibanaSavedObjectValidationIssue> issues)
    {
        foreach (var obj in bundle.Objects)
        {
            if (!string.Equals(obj.Type, "search", StringComparison.Ordinal))
            {
                continue;
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
                    if (DeprecatedFields.Contains(columnName))
                    {
                        issues.Add(CreateV6Issue(obj, $"columns に deprecated フィールド '{columnName}' が含まれている。"));
                    }
                }
            }

            if (obj.Attributes.TryGetProperty("sort", out var sortProp)
                && sortProp.ValueKind == JsonValueKind.Array)
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
                    if (DeprecatedFields.Contains(fieldName))
                    {
                        issues.Add(CreateV6Issue(obj, $"sort に deprecated フィールド '{fieldName}' が含まれている。"));
                    }
                }
            }

            var queryText = TryGetSearchSourceQuery(obj.Attributes);
            if (queryText is not null && DeprecatedFieldInQuery.IsMatch(queryText))
            {
                var match = DeprecatedFieldInQuery.Match(queryText);
                issues.Add(CreateV6Issue(
                    obj,
                    $"searchSourceJSON の query に deprecated フィールド '{match.Value}' が含まれている。"));
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

    private static KibanaSavedObjectValidationIssue CreateV6Issue(KibanaSavedObject obj, string detail)
    {
        return new KibanaSavedObjectValidationIssue(
            "V6",
            obj.LineNumber,
            obj.Id,
            $"行 {obj.LineNumber} (id='{obj.Id}'): V6 — {detail}");
    }
}
