#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DebugStudio.Export.Elastic.Kibana;

namespace DebugStudio.Export.Tests.Elastic.Kibana;

/// <summary>
/// `elastic/queries/*.esql`（クエリ正本）と、正本 NDJSON の by-value ES|QL パネルに
/// 埋まっているクエリが**ずれていない**ことの検算。
///
/// <para>
/// §1.3 は「パネルの意図を Kibana のバージョンから独立させる」ために両者を分けているが、
/// 分けた瞬間に**片方だけ直す**事故が可能になる。再 <c>_export</c> でパネル側が変わっても、
/// <c>.esql</c> を手で直し忘れても、どちらも静かに通ってしまう。ここはそれを機械的に止める。
/// </para>
/// <para>
/// 突き合わせは「行コメント除去 → 空白正規化」後の完全一致。<c>.esql</c> は人間が読むために
/// 改行とコメントを持ち、パネル側は 1 行に潰れているので、そこだけ吸収する。
/// <b>緩さ:</b> 行コメントの除去は文字列リテラル内の <c>//</c> を区別しない。正本には
/// 該当箇所が無く、増えたらこのテストが赤くなるので気づける。
/// </para>
/// </summary>
public sealed class KibanaEsqlQuerySourceOfTruthTests
{
    /// <summary>
    /// 正本ファイル → それが載っているパネル（dashboard id, panelsJSON の実インデックス）。
    ///
    /// <para>
    /// <b>この表は「1 対 1 である」という主張そのもの</b>なので、両方向を検算する。
    /// パネルが増えたのに <c>.esql</c> が無ければ <see cref="全てのESQLパネルは正本ファイルと対応づいている"/> が、
    /// <c>.esql</c> が増えたのに表に無ければ <see cref="全ての正本ファイルは対応づけが宣言されている"/> が赤くなる。
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, (string DashboardId, int PanelIndex)> PanelByQueryFile = new()
    {
        ["heavy-spans.esql"] = ("debugstudio-overview-dashboard", 2),
        ["tag-breakdown.esql"] = ("debugstudio-overview-dashboard", 3),
        ["runs.esql"] = ("debugstudio-run-over-run-dashboard", 0),
        ["app-startup-per-run.esql"] = ("debugstudio-run-over-run-dashboard", 1),
        ["scene-load-per-run.esql"] = ("debugstudio-run-over-run-dashboard", 2),
        ["event-rate-per-run.esql"] = ("debugstudio-run-over-run-dashboard", 3),
    };

    /// <summary>
    /// パネルを持たない正本。**「まだ作っていない」ことを明示的に宣言する欄**であって、
    /// 例外の置き場ではない。<c>frame-cost-per-run.esql</c> は実データで 0 行を返す
    /// （<c>ProfilerSummary</c> が Unity から emit されていない）ためパネルにしていない。
    /// </summary>
    private static readonly string[] QueryFilesWithoutPanel =
    {
        "frame-cost-per-run.esql",
    };

    private static readonly Regex LineComment = new(@"//[^\n]*", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    [Fact]
    public void パネルに載っているESQLは対応する正本ファイルと一致する()
    {
        var panels = ReadEmbeddedPanelQueries();

        foreach (var (fileName, location) in PanelByQueryFile)
        {
            Assert.True(
                panels.TryGetValue(location, out var panelQuery),
                $"'{location.DashboardId}' の panelsJSON[{location.PanelIndex}] が by-value ES|QL パネルではない。");

            Assert.Equal(
                Normalize(ReadQueryFile(fileName)),
                Normalize(panelQuery!));
        }
    }

    [Fact]
    public void 全てのESQLパネルは正本ファイルと対応づいている()
    {
        var declared = PanelByQueryFile.Values.ToHashSet();
        var actual = ReadEmbeddedPanelQueries().Keys.ToArray();

        var undeclared = actual.Where(p => !declared.Contains(p)).ToArray();

        Assert.True(
            undeclared.Length == 0,
            "queries/ に正本の無い ES|QL パネルがある（§1.3 の 1 対 1 が崩れている）: "
            + string.Join(", ", undeclared.Select(p => $"{p.DashboardId}[{p.PanelIndex}]")));
    }

    [Fact]
    public void 全ての正本ファイルは対応づけが宣言されている()
    {
        var declared = PanelByQueryFile.Keys.Concat(QueryFilesWithoutPanel).ToHashSet();
        var actual = EnumerateQueryFileNames();

        var undeclared = actual.Where(f => !declared.Contains(f)).ToArray();

        Assert.True(
            undeclared.Length == 0,
            "パネルとの対応づけが宣言されていない .esql がある: " + string.Join(", ", undeclared));
    }

    [Fact]
    public void パネル未実装の正本はどのパネルにも埋め込まれていない()
    {
        var embedded = ReadEmbeddedPanelQueries().Values.Select(Normalize).ToArray();

        foreach (var fileName in QueryFilesWithoutPanel)
        {
            var normalized = Normalize(ReadQueryFile(fileName));
            Assert.DoesNotContain(normalized, embedded);
        }
    }

    private static string Normalize(string esql) =>
        Whitespace.Replace(LineComment.Replace(esql, " "), " ").Trim();

    private static Dictionary<(string DashboardId, int PanelIndex), string> ReadEmbeddedPanelQueries()
    {
        var ndjson = ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson();
        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);

        var result = new Dictionary<(string, int), string>();
        foreach (var dashboard in bundle.Objects.Where(o => o.Type == "dashboard"))
        {
            var panelsJson = dashboard.Attributes.GetProperty("panelsJSON").GetString()
                ?? throw new InvalidOperationException($"panelsJSON missing on '{dashboard.Id}'");

            using var doc = JsonDocument.Parse(panelsJson);
            var panelIndex = -1;
            foreach (var panel in doc.RootElement.EnumerateArray())
            {
                panelIndex++;
                if (panel.TryGetProperty("embeddableConfig", out var embeddableConfig)
                    && embeddableConfig.TryGetProperty("attributes", out var attributes)
                    && attributes.TryGetProperty("state", out var state)
                    && state.TryGetProperty("query", out var query)
                    && query.TryGetProperty("esql", out var esql)
                    && esql.ValueKind == JsonValueKind.String)
                {
                    result[(dashboard.Id, panelIndex)] = esql.GetString()!;
                }
            }
        }

        return result;
    }

    private const string ResourcePrefix = "DebugStudio.Export.Tests.Elastic.Queries.";

    private static string ReadQueryFile(string fileName)
    {
        var assembly = typeof(KibanaEsqlQuerySourceOfTruthTests).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + fileName)
            ?? throw new InvalidOperationException(
                $"埋め込みリソース '{ResourcePrefix + fileName}' が無い。csproj の EmbeddedResource を確認すること。");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string[] EnumerateQueryFileNames() =>
        typeof(KibanaEsqlQuerySourceOfTruthTests).Assembly
            .GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(n => n[ResourcePrefix.Length..])
            .ToArray();
}
