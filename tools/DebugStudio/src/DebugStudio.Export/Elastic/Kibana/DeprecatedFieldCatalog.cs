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

    public static bool Contains(string fieldName) => FieldSet.Contains(fieldName);
}
