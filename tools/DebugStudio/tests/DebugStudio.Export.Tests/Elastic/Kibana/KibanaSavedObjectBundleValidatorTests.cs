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

    /// <summary>
    /// N1（PR #16 レビュー指摘）— query の引用符内の<b>値</b>に deprecated 語が入っているだけでは
    /// フィールド参照ではないので V6 に落とさない。引用符付きの<b>フィールド名</b>は落とす。
    /// </summary>
    [Fact]
    public void queryの引用符内の値はV6に落ちず引用符付きフィールド名は落ちる()
    {
        var quotedValue = BuildSearchNdjson(
            columnsJson: "[\"kind\"]",
            query: "message: \"cpuTime is high\" or message: \"gpuTime\"");
        var quotedValueIssues = Validate(quotedValue);
        Assert.DoesNotContain(quotedValueIssues, i => i.RuleId == "V6");

        var quotedFieldName = BuildSearchNdjson(
            columnsJson: "[\"kind\"]",
            query: "\"cpuTime\": 10");
        var quotedFieldNameIssues = Validate(quotedFieldName);
        Assert.Contains(quotedFieldNameIssues, i => i.RuleId == "V6");

        // 引用符を挟んでも、素の参照は従来どおり落ちる。
        var mixed = BuildSearchNdjson(
            columnsJson: "[\"kind\"]",
            query: "message: \"ok\" and cpuTime > 10");
        var mixedIssues = Validate(mixed);
        Assert.Contains(mixedIssues, i => i.RuleId == "V6");
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
    // ── K3-4 で判明した実 _export 非対応（HANDOFF §7.8 G-1 / G-2）──

    [Fact]
    public void export形式のpanelIndex接頭辞付きreference名でもV4で落ちない()
    {
        // Kibana 8.17 の _export は reference 名を "<panelIndex>:panel_<panelIndex>" で出す。
        // 手書き正本の "panel_p1" しか受け付けないと、実 _export が丸ごと赤になる。
        var ndjson =
            """
            {"id":"dash","type":"dashboard","attributes":{"title":"x","panelsJSON":"[{\"type\":\"search\",\"panelIndex\":\"p1\",\"panelRefName\":\"panel_p1\"}]"},"references":[{"id":"s1","name":"p1:panel_p1","type":"search"}]}
            {"id":"s1","type":"search","attributes":{"title":"s","columns":[],"sort":[["@timestamp","desc"]],"kibanaSavedObjectMeta":{"searchSourceJSON":"{}"}},"references":[]}
            """;

        var issues = Validate(ndjson);

        Assert.DoesNotContain(issues, i => i.RuleId == "V4");
        Assert.DoesNotContain(issues, i => i.RuleId == "V7");
    }

    [Fact]
    public void byvalueパネルはpanelRefNameが無くてもV7で落ちない()
    {
        // ES|QL パネルは by-value で、中身が embeddableConfig.attributes に丸ごと埋まる。
        // panelRefName は原理的に存在しない。
        var esql = "FROM debugstudio-telemetry-* | LIMIT 5";

        var issues = Validate(EsqlPanelBundle(esql));

        Assert.DoesNotContain(issues, i => i.RuleId == "V7");
        Assert.DoesNotContain(issues, i => i.RuleId == "V12");
    }

    [Fact]
    public void byvalueでもembeddableConfigのattributesが無ければV7で落ちる()
    {
        // 「panelRefName が無い」を一律に許すと、V7 が塞いだ穴が戻る。
        var ndjson =
            """
            {"id":"dash","type":"dashboard","attributes":{"title":"x","panelsJSON":"[{\"type\":\"lens\",\"panelIndex\":\"e0\",\"embeddableConfig\":{}}]"},"references":[]}
            """;

        var issues = Validate(ndjson);

        Assert.Contains(issues, i => i.RuleId == "V7");
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
    public void ESQLパネルのqueryにdeprecated語があるとV12で落ちる(string deprecatedField)
    {
        // §1.6 は「K3 で追加するパネルにも同じ禁止がかかる」と書いているが、
        // V6 は type=search しか見ないため by-value パネルは素通りしていた。
        var esql = $"FROM debugstudio-telemetry-* | STATS m = MAX({deprecatedField}) BY sessionId";

        var issues = Validate(EsqlPanelBundle(esql));

        Assert.Contains(issues, i => i.RuleId == "V12" && i.Message.Contains(deprecatedField, StringComparison.Ordinal));
    }

    [Fact]
    public void ESQLパネルのpayload接頭辞付きの同名はV12で落ちない()
    {
        var esql = "FROM debugstudio-telemetry-* | STATS m = MAX(payload.cameraTotalViewCount) BY sessionId";

        var issues = Validate(EsqlPanelBundle(esql));

        Assert.DoesNotContain(issues, i => i.RuleId == "V12");
    }

    [Fact]
    public void ESQLパネルの文字列リテラル内のdeprecated語はV12で落ちない()
    {
        var esql = """FROM debugstudio-telemetry-* | WHERE name == "cpuTime" | LIMIT 5""";

        var issues = Validate(EsqlPanelBundle(esql));

        Assert.DoesNotContain(issues, i => i.RuleId == "V12");
    }

    [Fact]
    public void ESQLパネルのFROMがbundle内のindexpatternに無いとV12で落ちる()
    {
        // bundle が宣言していない index を引くパネルは、import 先で黙って 0 件になる。
        var esql = "FROM debugstudio-telemetry-2026.08.11 | LIMIT 5";

        var issues = Validate(EsqlPanelBundle(esql));

        Assert.Contains(issues, i => i.RuleId == "V12"
            && i.Message.Contains("debugstudio-telemetry-2026.08.11", StringComparison.Ordinal));
    }

    /// <summary>
    /// index-pattern 1 本と by-value ES|QL パネル 1 枚だけの最小 bundle。
    /// </summary>
    private static string EsqlPanelBundle(string esql)
    {
        var attributes = new Dictionary<string, object?>
        {
            ["title"] = "t",
            ["references"] = Array.Empty<object>(),
            ["state"] = new Dictionary<string, object?>
            {
                ["query"] = new Dictionary<string, string> { ["esql"] = esql },
            },
        };

        var panels = new[]
        {
            new Dictionary<string, object?>
            {
                ["type"] = "lens",
                ["panelIndex"] = "e0",
                ["embeddableConfig"] = new Dictionary<string, object?> { ["attributes"] = attributes },
            },
        };

        var dataView = new Dictionary<string, object?>
        {
            ["id"] = "dv",
            ["type"] = "index-pattern",
            ["attributes"] = new Dictionary<string, string> { ["title"] = "debugstudio-telemetry-*" },
            ["references"] = Array.Empty<object>(),
        };

        var dashboard = new Dictionary<string, object?>
        {
            ["id"] = "dash",
            ["type"] = "dashboard",
            ["attributes"] = new Dictionary<string, object?>
            {
                ["title"] = "x",
                ["panelsJSON"] = JsonSerializer.Serialize(panels),
            },
            ["references"] = Array.Empty<object>(),
        };

        return JsonSerializer.Serialize(dataView) + "\n" + JsonSerializer.Serialize(dashboard) + "\n";
    }
}
