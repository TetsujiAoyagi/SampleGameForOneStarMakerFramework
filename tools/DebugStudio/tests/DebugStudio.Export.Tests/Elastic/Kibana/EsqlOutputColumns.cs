#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DebugStudio.Export.Tests.Elastic.Kibana;

/// <summary>
/// ES|QL クエリが返す列名を求める。**テスト専用の、意図的に狭いパーサ。**
///
/// <para>
/// 目的は 1 つだけ: 「クエリが返す列が、パネルに 1 つ残らず現れているか」を機械的に見るため。
/// そのために必要なのは <c>KEEP</c> / <c>STATS ... BY</c> / <c>EVAL</c> の 3 つで、
/// 式の意味を理解する必要は無い（左辺の識別子しか見ない）。
/// </para>
/// <para>
/// <b>知らない構文は黙って通さず <see cref="NotSupportedException"/> を投げる。</b>
/// 列集合を取りこぼしたまま「包含できている」と報告するのが最悪の失敗なので、
/// 分からないものは分からないと言って赤くする。<c>DROP</c> / <c>RENAME</c> /
/// <c>GROK</c> / <c>DISSECT</c> / <c>ENRICH</c> / <c>MV_EXPAND</c> を正本で使い始めたら、
/// ここを足すこと。
/// </para>
/// </summary>
internal static class EsqlOutputColumns
{
    private static readonly Regex LineComment = new(@"//[^\n]*", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>列集合を変えないコマンド。</summary>
    private static readonly string[] PassThroughCommands = { "WHERE", "SORT", "LIMIT" };

    public static IReadOnlyList<string> Derive(string esql)
    {
        var normalized = Whitespace.Replace(LineComment.Replace(esql, " "), " ").Trim();
        var stages = SplitTopLevel(normalized, '|').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();

        if (stages.Length == 0 || !stages[0].StartsWith("FROM ", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"FROM で始まっていない: '{normalized}'");
        }

        // FROM だけでは列は決まらない（index の全フィールド）。KEEP か STATS が来るまで未確定。
        List<string>? columns = null;

        foreach (var stage in stages.Skip(1))
        {
            var command = stage.Split(' ')[0].ToUpperInvariant();
            var rest = stage[command.Length..].Trim();

            if (PassThroughCommands.Contains(command))
            {
                continue;
            }

            switch (command)
            {
                case "KEEP":
                    columns = SplitTopLevel(rest, ',').Select(c => c.Trim()).ToList();
                    break;

                case "STATS":
                    columns = DeriveStatsColumns(rest);
                    break;

                case "EVAL":
                    if (columns is null)
                    {
                        throw new NotSupportedException(
                            $"KEEP / STATS より前に EVAL が来ている。列集合が確定できない: '{stage}'");
                    }

                    foreach (var name in SplitTopLevel(rest, ',').Select(AssignedName))
                    {
                        if (!columns.Contains(name))
                        {
                            columns.Add(name);
                        }
                    }

                    break;

                default:
                    throw new NotSupportedException(
                        $"未対応の ES|QL コマンド '{command}'。EsqlOutputColumns に追加すること: '{stage}'");
            }
        }

        return columns
            ?? throw new NotSupportedException(
                $"KEEP も STATS も無く、返る列が確定できない: '{normalized}'");
    }

    private static List<string> DeriveStatsColumns(string rest)
    {
        var byIndex = IndexOfTopLevelKeyword(rest, "BY");
        var aggregations = byIndex < 0 ? rest : rest[..byIndex];
        var grouping = byIndex < 0 ? string.Empty : rest[(byIndex + "BY".Length)..];

        var columns = SplitTopLevel(aggregations, ',')
            .Select(AssignedName)
            .ToList();

        foreach (var group in SplitTopLevel(grouping, ',').Where(g => !string.IsNullOrWhiteSpace(g)))
        {
            // BY 側は `name = expr` でも裸の識別子でもよい。
            var name = AssignedName(group);
            if (!columns.Contains(name))
            {
                columns.Add(name);
            }
        }

        return columns;
    }

    /// <summary>
    /// <c>name = expr</c> の左辺。<c>=</c> が無ければ全体（<c>BY sessionId</c> のような裸の識別子）。
    /// <c>==</c> は代入ではないので飛ばす（<c>gc = COUNT(*) WHERE name == "GcSpike"</c>）。
    /// </summary>
    private static string AssignedName(string expression)
    {
        var trimmed = expression.Trim();
        var depth = 0;
        var inString = false;

        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c == '"')
            {
                inString = !inString;
            }
            else if (!inString && c == '(')
            {
                depth++;
            }
            else if (!inString && c == ')')
            {
                depth--;
            }
            else if (!inString && depth == 0 && c == '=')
            {
                if (i + 1 < trimmed.Length && trimmed[i + 1] == '=')
                {
                    break;
                }

                return trimmed[..i].Trim();
            }
        }

        return trimmed;
    }

    /// <summary>括弧と文字列リテラルの外にある <paramref name="separator"/> だけで分割する。</summary>
    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        var inString = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                inString = !inString;
            }
            else if (!inString && c == '(')
            {
                depth++;
            }
            else if (!inString && c == ')')
            {
                depth--;
            }
            else if (!inString && depth == 0 && c == separator)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }

    /// <summary>
    /// 括弧と文字列リテラルの外にある、独立した単語としての <paramref name="keyword"/> の位置。
    /// <c>MV_CONCAT(tags, "|")</c> や <c>LIKE "*|Bottleneck|*"</c> の中を拾わないためにこれが要る。
    /// </summary>
    private static int IndexOfTopLevelKeyword(string text, string keyword)
    {
        var depth = 0;
        var inString = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }
            else if (depth == 0
                     && i > 0
                     && char.IsWhiteSpace(text[i - 1])
                     && i + keyword.Length < text.Length
                     && char.IsWhiteSpace(text[i + keyword.Length])
                     && string.Compare(text, i, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return i;
            }
        }

        return -1;
    }
}
