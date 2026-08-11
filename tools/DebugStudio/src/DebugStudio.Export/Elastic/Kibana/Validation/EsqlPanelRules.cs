#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DebugStudio.Export.Elastic.Kibana.Validation;

/// <summary>
/// by-value ES|QL パネル（V12）の検算。IO 無しの純関数。
///
/// <para>
/// ES|QL パネルは dashboard の <c>panelsJSON[].embeddableConfig.attributes.state.query.esql</c> に
/// クエリ文字列が丸ごと埋まる形で保存され、<c>type=lens</c> の saved object にはならない。
/// このため saved object を走査する V6（deprecated 語）も V11（mapping 照合）も**届かない**。
/// V12 はその穴を埋める。
/// </para>
/// <para>
/// V11 の mapping 照合を移植していないのは、ES|QL では存在しないフィールドが
/// <b>実行時に硬いエラーになる</b>（<c>Unknown column</c>）ため。saved search の
/// columns / kuery のように「実在しないフィールドを静かに参照し続ける」状態にならない。
/// 代わりに、静かに壊れる 2 点だけを見る。
/// </para>
/// </summary>
public static class EsqlPanelRules
{
    /// <summary>
    /// <c>FROM &lt;target&gt;[, &lt;target&gt;] [METADATA ...]</c> の target 部分。
    /// </summary>
    private static readonly Regex FromClausePattern = new(
        @"(?:^|\|)\s*FROM\s+(?<targets>[^|]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LineCommentPattern = new(
        @"//[^\n]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void Validate(KibanaSavedObjectBundle bundle, List<KibanaSavedObjectValidationIssue> issues)
    {
        if (bundle is null)
        {
            throw new ArgumentNullException(nameof(bundle));
        }

        var knownIndexPatterns = CollectIndexPatternTitles(bundle);

        foreach (var obj in bundle.Objects)
        {
            if (!string.Equals(obj.Type, "dashboard", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var (panelIndex, esql) in EnumerateEsqlQueries(obj))
            {
                // §1.6: deprecated なフラット欄は新しいパネルにも禁止。
                // TryFindInQuery は照合前にダブルクォート区間を落とすので、
                // ES|QL の文字列リテラル（WHERE name == "cpuTime"）は誤検知しない。
                if (DeprecatedFieldCatalog.TryFindInQuery(esql, out var matched))
                {
                    issues.Add(CreateIssue(
                        obj,
                        $"panelsJSON[{panelIndex}]（ES|QL パネル）の query に deprecated フィールド '{matched}' が含まれている。"));
                }

                foreach (var target in EnumerateFromTargets(esql))
                {
                    if (knownIndexPatterns.Contains(target))
                    {
                        continue;
                    }

                    issues.Add(CreateIssue(
                        obj,
                        $"panelsJSON[{panelIndex}]（ES|QL パネル）の FROM '{target}' が bundle 内の index-pattern の title と一致しない。"));
                }
            }
        }
    }

    private static HashSet<string> CollectIndexPatternTitles(KibanaSavedObjectBundle bundle)
    {
        var titles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var obj in bundle.Objects)
        {
            if (!string.Equals(obj.Type, "index-pattern", StringComparison.Ordinal))
            {
                continue;
            }

            if (obj.Attributes.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String)
            {
                titles.Add(title.GetString() ?? string.Empty);
            }
        }

        return titles;
    }

    /// <summary>
    /// ES|QL パネルの (panelsJSON 上の実インデックス, query) を列挙する。
    ///
    /// <para>
    /// <b>インデックスは panelsJSON のものであって「ES|QL パネルの何枚目」ではない。</b>
    /// 両者は一致しないことがある（by-reference の saved search パネルが混ざる D1 では、
    /// panelsJSON[2] が ES|QL としては 1 枚目）。指摘を読んだ人間が panelsJSON を
    /// そのまま数えて辿れるよう、実インデックスを返す。
    /// </para>
    /// </summary>
    private static IEnumerable<(int PanelIndex, string Esql)> EnumerateEsqlQueries(KibanaSavedObject obj)
    {
        if (!obj.Attributes.TryGetProperty("panelsJSON", out var panelsJsonProp)
            || panelsJsonProp.ValueKind != JsonValueKind.String)
        {
            yield break;
        }

        JsonElement panels;
        try
        {
            using var doc = JsonDocument.Parse(panelsJsonProp.GetString() ?? string.Empty);
            panels = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            // panelsJSON が壊れている件は V3 が報告する。ここでは黙って抜ける。
            yield break;
        }

        if (panels.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var panelIndex = -1;
        foreach (var panel in panels.EnumerateArray())
        {
            panelIndex++;
            if (panel.TryGetProperty("embeddableConfig", out var embeddableConfig)
                && embeddableConfig.TryGetProperty("attributes", out var attributes)
                && attributes.TryGetProperty("state", out var state)
                && state.TryGetProperty("query", out var query)
                && query.TryGetProperty("esql", out var esql)
                && esql.ValueKind == JsonValueKind.String)
            {
                yield return (panelIndex, esql.GetString() ?? string.Empty);
            }
        }
    }

    /// <summary>
    /// <c>FROM</c> の対象 index を列挙する。
    ///
    /// <para>
    /// <b>緩い実装:</b> ES|QL の完全なパーサではない。<c>FROM</c> 句が文字列リテラルや
    /// サブクエリの中に現れるケース、<c>METADATA</c> 以外の後置句は想定していない。
    /// 目的は「bundle が宣言していない index を引くパネル」を捕まえることだけ。
    /// </para>
    /// </summary>
    private static IEnumerable<string> EnumerateFromTargets(string esql)
    {
        var stripped = LineCommentPattern.Replace(esql, " ");
        foreach (Match match in FromClausePattern.Matches(stripped))
        {
            var targets = match.Groups["targets"].Value;

            var metadata = targets.IndexOf(" METADATA ", StringComparison.OrdinalIgnoreCase);
            if (metadata >= 0)
            {
                targets = targets[..metadata];
            }

            foreach (var raw in targets.Split(','))
            {
                var target = raw.Trim().Trim('`', '"');
                if (target.Length > 0)
                {
                    yield return target;
                }
            }
        }
    }

    private static KibanaSavedObjectValidationIssue CreateIssue(KibanaSavedObject obj, string detail) =>
        new(
            "V12",
            obj.LineNumber,
            obj.Id,
            $"行 {obj.LineNumber} (id='{obj.Id}'): V12 — {detail}");
}
