#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DebugStudio.Export.Elastic.Kibana;

/// <summary>
/// Telemetry Contract v3 で deprecated なフラット欄の単一正本。
/// columns / sort の完全一致と query 内の語境界判定の双方がここを参照する。
/// <c>payload.</c> 接頭辞付きの同名は正本であり、Regex の lookbehind で除外する。
/// </summary>
public static class DeprecatedFieldCatalog
{
    public static IReadOnlyList<string> Fields { get; } =
    [
        "cpuTime",
        "gpuTime",
        "managedMem",
        "nativeMem",
        "cameraTotalViewCount",
        "cameraAdditionalViewCount",
        "cameraBlendingViewCount",
        "cameraMaxStackDepthTotal",
    ];

    private static readonly HashSet<string> FieldSet = new(Fields, StringComparer.Ordinal);

    public static Regex QueryPattern { get; } = new(
        $@"(?<![.\w])({string.Join("|", Fields.Select(Regex.Escape))})(?![\w])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// ダブルクォート区間。直後が <c>:</c> ならフィールド名、そうでなければ値とみなす。
    /// </summary>
    private static readonly Regex DoubleQuotedSegmentPattern = new(
        "\"((?:\\\\.|[^\"\\\\])*)\"(\\s*:)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool Contains(string fieldName) => FieldSet.Contains(fieldName);

    /// <summary>
    /// kuery から deprecated フィールドの<b>参照</b>を探す。
    ///
    /// <para>
    /// 照合前に <see cref="ScrubQuotedValues"/> でダブルクォート区間を処理する。
    /// 直後が <c>:</c> の区間はフィールド名として中身を残し、それ以外は値として空白に落とす。
    /// これにより <c>message: "cpuTime is high"</c>（値に語が入っているだけ）は誤検知せず、
    /// <c>"cpuTime": 5</c>（引用符付きフィールド名）は検出できる。
    /// </para>
    /// <para>
    /// <b>緩い実装の限界:</b> 単一引用符・ネストした複雑なエスケープ・スクリプトや関数の引数内は
    /// 見ていない。KQL の完全なパーサではない。
    /// </para>
    /// </summary>
    public static bool TryFindInQuery(string queryText, out string matched)
    {
        var match = QueryPattern.Match(ScrubQuotedValues(queryText));
        matched = match.Success ? match.Value : string.Empty;
        return match.Success;
    }

    private static string ScrubQuotedValues(string queryText) =>
        DoubleQuotedSegmentPattern.Replace(
            queryText,
            m => m.Groups[2].Success ? m.Groups[1].Value + m.Groups[2].Value : " ");
}
