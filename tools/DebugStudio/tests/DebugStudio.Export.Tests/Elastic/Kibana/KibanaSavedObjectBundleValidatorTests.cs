#nullable enable

using System.Linq;
using System.Text.Json;
using DebugStudio.Export.Elastic.Kibana;

namespace DebugStudio.Export.Tests.Elastic.Kibana;

public sealed class KibanaSavedObjectBundleValidatorTests
{
    [Fact]
    public void panelsJSONが空配列のdashboardでV3が指摘される()
    {
        var ndjson =
            """
            {"id":"dash","type":"dashboard","attributes":{"title":"x","panelsJSON":"[]"},"references":[]}
            """;

        var issues = Validate(ndjson);

        Assert.Contains(issues, i => i.RuleId == "V3");
    }

    [Fact]
    public void referencesにpanelがあるのにpanelsJSONから参照されていないとV4が指摘される()
    {
        var ndjson =
            """
            {"id":"dash","type":"dashboard","attributes":{"title":"x","panelsJSON":"[{\"type\":\"search\",\"panelIndex\":\"p1\",\"panelRefName\":\"panel_p1\"}]"},"references":[{"id":"s1","name":"panel_0","type":"search"},{"id":"s1","name":"panel_p1","type":"search"}]}
            {"id":"s1","type":"search","attributes":{"title":"s","columns":[],"sort":[]},"references":[]}
            """;

        var issues = Validate(ndjson);

        Assert.Contains(issues, i => i.RuleId == "V4" && i.Message.Contains("panel_0", System.StringComparison.Ordinal));
    }

    [Fact]
    public void panelsJSONのpanelRefNameがreferencesに無いとV4が指摘される()
    {
        var ndjson =
            """
            {"id":"dash","type":"dashboard","attributes":{"title":"x","panelsJSON":"[{\"type\":\"search\",\"panelIndex\":\"p9\",\"panelRefName\":\"panel_p9\"}]"},"references":[]}
            """;

        var issues = Validate(ndjson);

        Assert.Contains(issues, i => i.RuleId == "V4" && i.Message.Contains("panel_p9", System.StringComparison.Ordinal));
    }

    [Fact]
    public void パネルにpanelRefNameが無いとV7で落ちる()
    {
        // V3/V4 は panelRefName 無し + references 空だと緑のまま通る穴。V7 がそれを捕まえる。
        var ndjson =
            """
            {"id":"dash","type":"dashboard","attributes":{"title":"x","panelsJSON":"[{\"type\":\"search\",\"panelIndex\":\"p1\"}]"},"references":[]}
            """;

        var issues = Validate(ndjson);

        Assert.Contains(issues, i => i.RuleId == "V7");
        Assert.DoesNotContain(issues, i => i.RuleId == "V4");
    }

    [Fact]
    public void referencesのtypeが実オブジェクトと違うとV8で落ちる()
    {
        var ndjson =
            """
            {"id":"dash","type":"dashboard","attributes":{"title":"x","panelsJSON":"[{\"type\":\"search\",\"panelIndex\":\"p1\",\"panelRefName\":\"panel_p1\"}]"},"references":[{"id":"s1","name":"panel_p1","type":"lens"}]}
            {"id":"s1","type":"search","attributes":{"title":"s","columns":[],"sort":[],"kibanaSavedObjectMeta":{"searchSourceJSON":"{\"query\":{\"query\":\"\",\"language\":\"kuery\"}}"}},"references":[]}
            """;

        var issues = Validate(ndjson);

        Assert.Contains(issues, i => i.RuleId == "V8");
        Assert.DoesNotContain(issues, i => i.RuleId == "V5");
    }

    [Fact]
    public void searchにkibanaSavedObjectMetaが無いとV9で落ちる()
    {
        var ndjson =
            """
            {"id":"s1","type":"search","attributes":{"title":"s","columns":[],"sort":[]},"references":[]}
            """;

        var issues = Validate(ndjson);

        Assert.Contains(issues, i => i.RuleId == "V9");
    }

    [Fact]
    public void searchのsortが文字列だとV10で落ちる()
    {
        var ndjson =
            """
            {"id":"s1","type":"search","attributes":{"title":"s","columns":[],"sort":"@timestamp","kibanaSavedObjectMeta":{"searchSourceJSON":"{\"query\":{\"query\":\"\",\"language\":\"kuery\"}}"}},"references":[]}
            """;

        var issues = Validate(ndjson);

        Assert.Contains(issues, i => i.RuleId == "V10");
    }

    [Fact]
    public void searchのsortが無いとV10で落ちる()
    {
        var ndjson =
            """
            {"id":"s1","type":"search","attributes":{"title":"s","columns":[],"kibanaSavedObjectMeta":{"searchSourceJSON":"{\"query\":{\"query\":\"\",\"language\":\"kuery\"}}"}},"references":[]}
            """;

        var issues = Validate(ndjson);

        Assert.Contains(issues, i => i.RuleId == "V10" && i.Message.Contains("sort が無い", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("cpuTime")]
    [InlineData("gpuTime")]
    [InlineData("managedMem")]
    [InlineData("nativeMem")]
    [InlineData("cameraTotalViewCount")]
    [InlineData("cameraAdditionalViewCount")]
    [InlineData("cameraBlendingViewCount")]
    [InlineData("cameraMaxStackDepthTotal")]
    public void deprecated8語がcolumnsでV6に落ちる(string field)
    {
        var ndjson = BuildSearchNdjson(
            columnsJson: $"[\"{field}\"]",
            query: string.Empty);
        var issues = Validate(ndjson);
        Assert.Contains(issues, i => i.RuleId == "V6" && i.Message.Contains(field, System.StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("cpuTime")]
    [InlineData("gpuTime")]
    [InlineData("managedMem")]
    [InlineData("nativeMem")]
    [InlineData("cameraTotalViewCount")]
    [InlineData("cameraAdditionalViewCount")]
    [InlineData("cameraBlendingViewCount")]
    [InlineData("cameraMaxStackDepthTotal")]
    public void deprecated8語がqueryでV6に落ちる(string field)
    {
        var ndjson = BuildSearchNdjson(
            columnsJson: "[\"kind\"]",
            query: $"{field} > 0");
        var issues = Validate(ndjson);
        Assert.Contains(issues, i => i.RuleId == "V6" && i.Message.Contains(field, System.StringComparison.Ordinal));
    }

    [Fact]
    public void columnsにcpuTimeがあるとV6が指摘されpayload側は指摘されない()
    {
        var deprecatedColumns = BuildSearchNdjson(
            columnsJson: "[\"cpuTime\"]",
            query: string.Empty);
        var deprecatedIssues = Validate(deprecatedColumns);
        Assert.Contains(deprecatedIssues, i => i.RuleId == "V6");

        var payloadColumns = BuildSearchNdjson(
            columnsJson: "[\"payload.cpuMs\",\"payload.cameraTotalViewCount\"]",
            query: "payload.cpuMs > 0 and payload.cameraTotalViewCount > 0");
        var payloadIssues = Validate(payloadColumns);
        Assert.DoesNotContain(payloadIssues, i => i.RuleId == "V6");

        var deprecatedQuery = BuildSearchNdjson(
            columnsJson: "[\"kind\"]",
            query: "cpuTime > 10");
        var deprecatedQueryIssues = Validate(deprecatedQuery);
        Assert.Contains(deprecatedQueryIssues, i => i.RuleId == "V6");
    }

    [Fact]
    public void sortにcpuTimeがあるとV6が指摘されpayload側は指摘されない()
    {
        var deprecatedSort = BuildSearchNdjson(
            columnsJson: "[\"kind\"]",
            query: string.Empty,
            sortJson: "[[\"cpuTime\",\"desc\"]]");
        var deprecatedIssues = Validate(deprecatedSort);
        Assert.Contains(deprecatedIssues, i => i.RuleId == "V6");

        var payloadSort = BuildSearchNdjson(
            columnsJson: "[\"kind\"]",
            query: string.Empty,
            sortJson: "[[\"payload.cpuMs\",\"desc\"]]");
        var payloadIssues = Validate(payloadSort);
        Assert.DoesNotContain(payloadIssues, i => i.RuleId == "V6");
    }

    [Fact]
    public void 存在しないidを参照するとV5が指摘される()
    {
        var ndjson =
            """
            {"id":"s1","type":"search","attributes":{"title":"s","columns":[],"sort":[],"kibanaSavedObjectMeta":{"searchSourceJSON":"{\"query\":{\"query\":\"\",\"language\":\"kuery\"}}"}},"references":[{"id":"missing-dataview","name":"kibanaSavedObjectMeta.searchSourceJSON.index","type":"index-pattern"}]}
            """;

        var issues = Validate(ndjson);

        Assert.Contains(issues, i => i.RuleId == "V5" && i.Message.Contains("missing-dataview", System.StringComparison.Ordinal));
    }

    [Fact]
    public void id重複でV2が指摘される()
    {
        var ndjson =
            """
            {"id":"dup","type":"search","attributes":{"title":"a","columns":[],"sort":[],"kibanaSavedObjectMeta":{"searchSourceJSON":"{\"query\":{\"query\":\"\",\"language\":\"kuery\"}}"}},"references":[]}
            {"id":"dup","type":"search","attributes":{"title":"b","columns":[],"sort":[],"kibanaSavedObjectMeta":{"searchSourceJSON":"{\"query\":{\"query\":\"\",\"language\":\"kuery\"}}"}},"references":[]}
            """;

        var issues = Validate(ndjson);

        Assert.Contains(issues, i => i.RuleId == "V2");
    }

    [Fact]
    public void exportedCountサマリ行があるとV1が指摘される()
    {
        var ndjson =
            """
            {"id":"a","type":"search","attributes":{"title":"a","columns":[],"sort":[],"kibanaSavedObjectMeta":{"searchSourceJSON":"{\"query\":{\"query\":\"\",\"language\":\"kuery\"}}"}},"references":[]}
            {"exportedCount":5}
            """;

        var issues = Validate(ndjson);

        Assert.Contains(issues, i => i.RuleId == "V1");
    }

    private static IReadOnlyList<KibanaSavedObjectValidationIssue> Validate(string ndjson)
    {
        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);
        return KibanaSavedObjectBundleValidator.Validate(bundle);
    }

    private static string BuildSearchNdjson(
        string columnsJson,
        string query,
        string sortJson = "[]")
    {
        var searchSource = JsonSerializer.Serialize(new
        {
            query = new { query, language = "kuery" },
            filter = Array.Empty<object>(),
            indexRefName = "kibanaSavedObjectMeta.searchSourceJSON.index",
        });

        var attributes = new Dictionary<string, object?>
        {
            ["title"] = "t",
            ["columns"] = JsonSerializer.Deserialize<JsonElement>(columnsJson),
            ["sort"] = JsonSerializer.Deserialize<JsonElement>(sortJson),
            ["kibanaSavedObjectMeta"] = new Dictionary<string, string>
            {
                ["searchSourceJSON"] = searchSource,
            },
        };

        var obj = new Dictionary<string, object?>
        {
            ["id"] = "s1",
            ["type"] = "search",
            ["attributes"] = attributes,
            ["references"] = Array.Empty<object>(),
        };

        return JsonSerializer.Serialize(obj) + "\n";
    }
}
