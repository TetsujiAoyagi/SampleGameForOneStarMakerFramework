#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DebugStudio.Export.Elastic.Kibana;

namespace DebugStudio.Export.Tests.Elastic.Kibana;

/// <summary>
/// **ES|QL パネルが、クエリの返す列を落としていないこと**の検算（PR #17 フォローアップ #1）。
///
/// <para>
/// Lens の ES|QL パネルは、クエリが何列返しても<b>既定で先頭 5 列しか拾わない</b>。
/// 保存後のダッシュボードには警告が出ないため、K3-4 では D2-1 が 9 列中 5 列、
/// D2-7 が 11 列中 5 列で「完成」として commit された。**D2-7 は「異常発生率」を
/// 名乗りながら率の列（<c>gcPerMin</c> 等）を 1 つも表示していなかった。**
/// </para>
/// <para>
/// この事故は「描画された」を確認しても見つからない。**クエリの列と、パネルが
/// 保持している列を数えて突き合わせるまで分からない。** ここがそれを機械的に止める。
/// 落とすなら <see cref="IntentionallyHiddenColumns"/> に理由付きで宣言させ、
/// 「黙って消える」を「宣言して消す」に変える。
/// </para>
/// <para>
/// クエリ文字列と <c>.esql</c> 正本が一致していることは
/// <see cref="KibanaEsqlQuerySourceOfTruthTests"/> が別に強制しているので、
/// ここはパネルに埋まっているクエリだけを見ればよい。
/// </para>
/// </summary>
public sealed class KibanaEsqlPanelColumnCoverageTests
{
    /// <summary>
    /// **意図的にパネルに載せていない列。** 空でない値を書くこと自体が設計判断の記録になる。
    ///
    /// <para>
    /// D1-4（重い span）の <c>sessionId</c>: §2 の D1-4 仕様は
    /// <c>name</c> / <c>payload.targetIdentity</c> / <c>elapsedMs</c> /
    /// <c>payload.managedDeltaBytes</c> しか要求していない。<c>sessionId</c> を
    /// <c>KEEP</c> しているのは**ダッシュボードの run コントロールが効くようにするため**で、
    /// 表に出す必要は無い。
    /// </para>
    /// </summary>
    private static readonly Dictionary<(string DashboardId, int PanelIndex), string[]> IntentionallyHiddenColumns = new()
    {
        [("debugstudio-overview-dashboard", 2)] = new[] { "sessionId" },
    };

    [Fact]
    public void ESQLパネルはクエリが返す列を落としていない()
    {
        var failures = new List<string>();

        foreach (var panel in ReadEsqlPanels())
        {
            var expected = EsqlOutputColumns.Derive(panel.Esql).ToList();
            IntentionallyHiddenColumns.TryGetValue((panel.DashboardId, panel.PanelIndex), out var hidden);
            hidden ??= Array.Empty<string>();

            var shouldShow = expected.Where(c => !hidden.Contains(c)).ToArray();
            var missing = shouldShow.Where(c => !panel.DatasourceFieldNames.Contains(c)).ToArray();

            // 「宣言したのにクエリに無い」も赤にする。クエリを直したあと宣言が残ると、
            // その列が本当は表示されているのか落ちているのか分からなくなる。
            var staleHidden = hidden.Where(c => !expected.Contains(c)).ToArray();

            // 「クエリに無い列がパネルにある」= 対応が壊れている。
            var unexpected = panel.DatasourceFieldNames.Where(c => !expected.Contains(c)).ToArray();

            if (missing.Length > 0)
            {
                failures.Add(
                    $"{panel.DashboardId}[{panel.PanelIndex}]: クエリが返す列がパネルに無い: {string.Join(", ", missing)}"
                    + "（Lens の 5 列既定に切り詰められていないか確認すること。"
                    + "意図的に落とすなら IntentionallyHiddenColumns に理由付きで宣言する）");
            }

            if (staleHidden.Length > 0)
            {
                failures.Add(
                    $"{panel.DashboardId}[{panel.PanelIndex}]: IntentionallyHiddenColumns の宣言がクエリに存在しない: "
                    + string.Join(", ", staleHidden));
            }

            if (unexpected.Length > 0)
            {
                failures.Add(
                    $"{panel.DashboardId}[{panel.PanelIndex}]: クエリが返さない列がパネルにある: "
                    + string.Join(", ", unexpected));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// datatable では、datasource が持っている列が <c>visualization.columns</c> にも
    /// 出ていないと**表に描かれない**。両者の枚数がずれていないことを見る。
    ///
    /// <para>
    /// <c>columnId</c> は手で足した列だと UUID になる（Lens が自動生成した列は fieldName と同名）ので、
    /// 名前ではなく<b>枚数</b>で突き合わせる。名前の対応は datasource 側の
    /// <c>columnId → fieldName</c> が正であり、そちらは上のテストが見ている。
    /// </para>
    /// </summary>
    [Fact]
    public void datatableパネルはdatasourceの列を全て表に出している()
    {
        foreach (var panel in ReadEsqlPanels().Where(p => p.VisualizationType == "lnsDatatable"))
        {
            Assert.True(
                panel.VisualizationColumnCount == panel.DatasourceFieldNames.Count,
                $"{panel.DashboardId}[{panel.PanelIndex}]: datasource が {panel.DatasourceFieldNames.Count} 列 "
                + $"持っているのに visualization.columns は {panel.VisualizationColumnCount} 列。表から落ちている列がある。");
        }
    }

    /// <summary>
    /// パーサが正本の 6 本を実際に解釈できていること。**列数 0 で通ってしまう空振りを防ぐ。**
    /// </summary>
    [Fact]
    public void 正本の全ESQLパネルで列が導出できている()
    {
        var panels = ReadEsqlPanels();

        Assert.Equal(6, panels.Count);
        foreach (var panel in panels)
        {
            Assert.NotEmpty(EsqlOutputColumns.Derive(panel.Esql));
        }
    }

    private sealed record EsqlPanel(
        string DashboardId,
        int PanelIndex,
        string Esql,
        string? VisualizationType,
        IReadOnlyList<string> DatasourceFieldNames,
        int VisualizationColumnCount);

    private static List<EsqlPanel> ReadEsqlPanels()
    {
        var ndjson = ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson();
        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);

        var panels = new List<EsqlPanel>();
        foreach (var dashboard in bundle.Objects.Where(o => o.Type == "dashboard"))
        {
            var panelsJson = dashboard.Attributes.GetProperty("panelsJSON").GetString()
                ?? throw new InvalidOperationException($"panelsJSON missing on '{dashboard.Id}'");

            using var doc = JsonDocument.Parse(panelsJson);
            var panelIndex = -1;
            foreach (var panel in doc.RootElement.EnumerateArray())
            {
                panelIndex++;
                if (!panel.TryGetProperty("embeddableConfig", out var embeddableConfig)
                    || !embeddableConfig.TryGetProperty("attributes", out var attributes)
                    || !attributes.TryGetProperty("state", out var state)
                    || !state.TryGetProperty("query", out var query)
                    || !query.TryGetProperty("esql", out var esql)
                    || esql.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                panels.Add(new EsqlPanel(
                    dashboard.Id,
                    panelIndex,
                    esql.GetString()!,
                    attributes.TryGetProperty("visualizationType", out var visType) ? visType.GetString() : null,
                    ReadDatasourceFieldNames(state),
                    ReadVisualizationColumnCount(state)));
            }
        }

        return panels;
    }

    private static IReadOnlyList<string> ReadDatasourceFieldNames(JsonElement state)
    {
        var names = new List<string>();
        if (!state.TryGetProperty("datasourceStates", out var datasourceStates)
            || !datasourceStates.TryGetProperty("textBased", out var textBased)
            || !textBased.TryGetProperty("layers", out var layers))
        {
            return names;
        }

        foreach (var layer in layers.EnumerateObject())
        {
            if (!layer.Value.TryGetProperty("columns", out var columns))
            {
                continue;
            }

            foreach (var column in columns.EnumerateArray())
            {
                if (column.TryGetProperty("fieldName", out var fieldName)
                    && fieldName.ValueKind == JsonValueKind.String)
                {
                    names.Add(fieldName.GetString()!);
                }
            }
        }

        return names;
    }

    private static int ReadVisualizationColumnCount(JsonElement state) =>
        state.TryGetProperty("visualization", out var visualization)
        && visualization.TryGetProperty("columns", out var columns)
        && columns.ValueKind == JsonValueKind.Array
            ? columns.GetArrayLength()
            : -1;
}
