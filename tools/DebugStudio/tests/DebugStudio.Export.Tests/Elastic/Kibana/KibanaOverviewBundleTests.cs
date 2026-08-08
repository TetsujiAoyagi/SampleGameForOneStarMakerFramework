#nullable enable

using System.Linq;
using System.Text.Json;
using DebugStudio.Export.Elastic.Kibana;

namespace DebugStudio.Export.Tests.Elastic.Kibana;

/// <summary>
/// 正本 NDJSON（埋め込みリソース）が V1〜V6 と確定仕様を満たすことの検算。
/// </summary>
public sealed class KibanaOverviewBundleTests
{
    private static readonly string[] ExpectedIds =
    {
        "debugstudio-telemetry-dataview",
        "debugstudio-log-dataview",
        "debugstudio-telemetry-timeline",
        "debugstudio-log-warnings",
        "debugstudio-overview-dashboard",
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

    [Fact]
    public void 正本NDJSONはV1からV6で指摘0件である()
    {
        var ndjson = ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson();
        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);
        var issues = KibanaSavedObjectBundleValidator.Validate(bundle);

        Assert.True(
            issues.Count == 0,
            "正本に検算指摘がある:\n" + string.Join("\n", issues.Select(i => i.Message)));
    }

    [Fact]
    public void 正本は5オブジェクトでidとsearchのcolumnsが仕様どおりである()
    {
        var ndjson = ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson();
        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);

        Assert.Equal(5, bundle.Objects.Count);
        Assert.Equal(ExpectedIds, bundle.Objects.Select(o => o.Id).ToArray());

        Assert.True(bundle.TryGetById("debugstudio-telemetry-timeline", out var telemetrySearch));
        Assert.Equal(ExpectedTelemetryColumns, ReadColumns(telemetrySearch!));

        Assert.True(bundle.TryGetById("debugstudio-log-warnings", out var logSearch));
        Assert.Equal(ExpectedLogColumns, ReadColumns(logSearch!));
    }

    [Fact]
    public void dashboardのpanelsJSONはsearchパネル2枚で参照先が正しい()
    {
        var ndjson = ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson();
        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);

        Assert.True(bundle.TryGetById("debugstudio-overview-dashboard", out var dashboard));
        var panelsJson = dashboard!.Attributes.GetProperty("panelsJSON").GetString()
            ?? throw new InvalidOperationException("panelsJSON missing");

        using var panelsDoc = JsonDocument.Parse(panelsJson);
        var panels = panelsDoc.RootElement;
        Assert.Equal(2, panels.GetArrayLength());

        var panel0 = panels[0];
        var panel1 = panels[1];
        Assert.Equal("search", panel0.GetProperty("type").GetString());
        Assert.Equal("search", panel1.GetProperty("type").GetString());

        var refByName = dashboard.References.ToDictionary(r => r.Name, r => r.Id, StringComparer.Ordinal);
        Assert.Equal("debugstudio-telemetry-timeline", refByName[panel0.GetProperty("panelRefName").GetString()!]);
        Assert.Equal("debugstudio-log-warnings", refByName[panel1.GetProperty("panelRefName").GetString()!]);
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
