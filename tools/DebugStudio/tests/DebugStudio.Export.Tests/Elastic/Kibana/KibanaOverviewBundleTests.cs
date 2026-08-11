#nullable enable

using System.Linq;
using System.Text.Json;
using DebugStudio.Export.Elastic.Kibana;
using DebugStudio.Export.Elastic.Kibana.Validation;

namespace DebugStudio.Export.Tests.Elastic.Kibana;

/// <summary>
/// 正本 NDJSON（埋め込みリソース）が V1〜V12 と確定仕様を満たすことの検算。
/// </summary>
public sealed class KibanaOverviewBundleTests
{
    /// <summary>
    /// 並び順は Kibana の <c>_export</c> が返した順。手で並べ替えない（§1.4）。
    /// </summary>
    private static readonly string[] ExpectedIds =
    {
        "debugstudio-telemetry-dataview",
        "debugstudio-telemetry-timeline",
        "debugstudio-log-dataview",
        "debugstudio-log-warnings",
        "debugstudio-overview-dashboard",
        "debugstudio-run-over-run-dashboard",
    };

    private static readonly string[] ExpectedDashboardIds =
    {
        "debugstudio-overview-dashboard",
        "debugstudio-run-over-run-dashboard",
    };

    private static readonly string[] ExpectedTelemetryColumns =
    {
        "kind",
        "name",
        "elapsedMs",
        "payload.stage",
        "buildVersion",
        "sessionId",
    };

    private static readonly string[] ExpectedLogColumns =
    {
        "log.level",
        "category",
        "message",
        "sessionId",
    };

    /// <summary>
    /// <see cref="KibanaSavedObjectBundleValidator.Validate"/> を丸ごと呼ぶため、
    /// ルールが増えれば自動的にそれも正本に強制される（現在 V1〜V10 と V12）。
    /// V11（lens / query のフィールド mapping 検算）は index template を要するため
    /// <c>KibanaSavedObjectFieldMappingTests</c> 側にある。
    /// </summary>
    [Fact]
    public void 正本NDJSONはV1からV12で指摘0件である()
    {
        var ndjson = ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson();
        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);
        var issues = KibanaSavedObjectBundleValidator.Validate(bundle);

        Assert.True(
            issues.Count == 0,
            "正本に検算指摘がある:\n" + string.Join("\n", issues.Select(i => i.Message)));
    }

    [Fact]
    public void 正本は6オブジェクトでidとsearchのcolumnsが仕様どおりである()
    {
        var ndjson = ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson();
        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);

        Assert.Equal(6, bundle.Objects.Count);
        Assert.Equal(ExpectedIds, bundle.Objects.Select(o => o.Id).ToArray());

        Assert.True(bundle.TryGetById("debugstudio-telemetry-timeline", out var telemetrySearch));
        Assert.Equal(ExpectedTelemetryColumns, ReadColumns(telemetrySearch!));

        Assert.True(bundle.TryGetById("debugstudio-log-warnings", out var logSearch));
        Assert.Equal(ExpectedLogColumns, ReadColumns(logSearch!));
    }

    /// <summary>
    /// T9: 正本のダッシュボードは 2 枚あり、どのパネルも中身が解決できる。
    ///
    /// <para>
    /// パネルは 2 通りある。by-reference（saved search）は <c>panelRefName</c> が
    /// <c>references</c> と 1:1 で対応し、by-value（ES|QL）は
    /// <c>embeddableConfig.attributes</c> に中身が丸ごと入る。
    /// **どちらでもないパネルは「参照先が消えたのに気づけない」状態**なので、
    /// パネル 0 枚事故と同型の再発検知としてここで落とす。
    /// </para>
    /// <para>
    /// Kibana 8.17 の <c>_export</c> は reference 名に <c>&lt;panelIndex&gt;:</c> の
    /// 接頭辞を付ける（<c>p1:panel_p1</c>）ので、突き合わせは接頭辞を剥がして行う。
    /// </para>
    /// </summary>
    [Fact]
    public void 正本のダッシュボードは2枚ありどのパネルも参照先かbyvalueの中身を持つ()
    {
        var ndjson = ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson();
        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);

        var dashboards = bundle.Objects
            .Where(o => o.Type == "dashboard")
            .ToArray();

        Assert.Equal(ExpectedDashboardIds, dashboards.Select(d => d.Id).ToArray());

        foreach (var dashboard in dashboards)
        {
            var panelsJson = dashboard.Attributes.GetProperty("panelsJSON").GetString()
                ?? throw new InvalidOperationException($"panelsJSON missing on '{dashboard.Id}'");

            using var panelsDoc = JsonDocument.Parse(panelsJson);
            var panels = panelsDoc.RootElement;
            Assert.True(panels.GetArrayLength() > 0, $"'{dashboard.Id}' のパネルが 0 枚。");

            // 正規化は本番（V4）と同じ関数を使う。T9 が独自実装を持っていた頃は
            // 「':' 以降を無条件で剥がす」ため controlGroup 参照まで辞書に入っていた。
            var refIdByPanelName = dashboard.References
                .Select(r => (Name: PanelReferenceRules.NormalizePanelReferenceName(r.Name), r.Id))
                .Where(r => r.Name is not null)
                .ToDictionary(r => r.Name!, r => r.Id, StringComparer.Ordinal);

            var index = 0;
            foreach (var panel in panels.EnumerateArray())
            {
                var where = $"'{dashboard.Id}' の panelsJSON[{index}]";
                index++;

                if (panel.TryGetProperty("panelRefName", out var refName)
                    && refName.ValueKind == JsonValueKind.String)
                {
                    var name = refName.GetString()!;
                    Assert.True(
                        refIdByPanelName.TryGetValue(name, out var targetId),
                        $"{where} の panelRefName '{name}' に対応する references が無い。");
                    Assert.True(
                        bundle.TryGetById(targetId!, out _),
                        $"{where} の参照先 '{targetId}' が bundle 内に無い。");
                    continue;
                }

                // **`attributes` の存在だけでは足りない。** `attributes: {}` は中身の無い
                // by-value パネルであり、V7 が塞ごうとした状態そのもの。ここでも中身
                // （ES|QL パネルなら state.query.esql）まで要求する。
                Assert.True(
                    panel.TryGetProperty("embeddableConfig", out var embeddableConfig)
                    && embeddableConfig.TryGetProperty("attributes", out var attributes)
                    && attributes.TryGetProperty("state", out var state)
                    && state.TryGetProperty("query", out var query)
                    && query.TryGetProperty("esql", out var esql)
                    && !string.IsNullOrWhiteSpace(esql.GetString()),
                    $"{where} は panelRefName も、中身のある embeddableConfig.attributes.state.query.esql も持たない。");
            }
        }
    }

    [Fact]
    public void logSearchのsearchSourceJSONのqueryはwarning以上のKQLと完全一致する()
    {
        var ndjson = ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson();
        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);

        Assert.True(bundle.TryGetById("debugstudio-log-warnings", out var logSearch));
        var searchSourceJson = logSearch!.Attributes
            .GetProperty("kibanaSavedObjectMeta")
            .GetProperty("searchSourceJSON")
            .GetString()
            ?? throw new InvalidOperationException("searchSourceJSON missing");

        using var searchSource = JsonDocument.Parse(searchSourceJson);
        var query = searchSource.RootElement.GetProperty("query").GetProperty("query").GetString();
        Assert.Equal("log.level: (\"warning\" or \"error\" or \"critical\")", query);
    }

    private static string[] ReadColumns(KibanaSavedObject search)
    {
        return search.Attributes.GetProperty("columns")
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .ToArray();
    }
}
