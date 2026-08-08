#nullable enable

using System.Linq;
using System.Text.Json;
using DebugStudio.Export.Elastic;
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

    /// <summary>
    /// saved search の columns が、対応する index template に mapping されているフィールドだけで
    /// 構成されていることを検算する。
    ///
    /// これが無かったために log saved search が実在しない `log.level` を参照したまま
    /// C レビュー 3 巡と C' 監査を通過し、実地確認まで「import も描画も成功して 0 件」に
    /// 気づけなかった。V1〜V6 は正本の内部構造しか見ないので、この穴はここでしか塞げない。
    /// </summary>
    [Fact]
    public async Task SavedSearchのcolumnsは対応するIndexTemplateにmappingされている()
    {
        var bundle = KibanaSavedObjectBundleParser.Parse(
            ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson());

        using var telemetryTemplate = JsonDocument.Parse(
            ElasticTelemetryIndexTemplateDefinition.CreateArtifactJson());
        AssertColumnsAreMapped(
            bundle,
            "debugstudio-telemetry-timeline",
            CollectMappedFieldPaths(MappingProperties(telemetryTemplate.RootElement)));

        // log 側の template writer はファイル出力しか持たないので temp へ書いて読む。
        var logTemplatePath = Path.Combine(
            Path.GetTempPath(),
            $"debugstudio-log-index-template-{Guid.NewGuid():N}.json");
        try
        {
            await new ElasticLogIndexTemplateWriter().WriteAsync(logTemplatePath);

            using var logTemplate = JsonDocument.Parse(await File.ReadAllTextAsync(logTemplatePath));
            AssertColumnsAreMapped(
                bundle,
                "debugstudio-log-warnings",
                CollectMappedFieldPaths(MappingProperties(logTemplate.RootElement)));
        }
        finally
        {
            if (File.Exists(logTemplatePath))
            {
                File.Delete(logTemplatePath);
            }
        }
    }

    private static JsonElement MappingProperties(JsonElement templateRoot)
    {
        return templateRoot.GetProperty("template").GetProperty("mappings").GetProperty("properties");
    }

    /// <summary>
    /// mappings.properties を再帰的に辿り、葉フィールドをドット区切りのパスとして集める。
    /// </summary>
    private static HashSet<string> CollectMappedFieldPaths(JsonElement properties, string prefix = "")
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in properties.EnumerateObject())
        {
            var path = prefix.Length == 0 ? property.Name : prefix + "." + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object
                && property.Value.TryGetProperty("properties", out var nested))
            {
                paths.UnionWith(CollectMappedFieldPaths(nested, path));
            }
            else
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private static void AssertColumnsAreMapped(
        KibanaSavedObjectBundle bundle,
        string searchId,
        HashSet<string> mappedFieldPaths)
    {
        Assert.True(bundle.TryGetById(searchId, out var search));

        var unmapped = ReadColumns(search!).Where(c => !mappedFieldPaths.Contains(c)).ToArray();

        Assert.True(
            unmapped.Length == 0,
            $"saved search '{searchId}' の columns に index template へ mapping されていない"
            + $"フィールドがある: {string.Join(", ", unmapped)}");
    }

    private static string[] ReadColumns(KibanaSavedObject search)
    {
        return search.Attributes.GetProperty("columns")
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .ToArray();
    }
}
