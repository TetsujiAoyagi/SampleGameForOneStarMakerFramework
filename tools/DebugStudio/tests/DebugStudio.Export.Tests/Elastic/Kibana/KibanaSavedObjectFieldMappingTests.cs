#nullable enable

using System.Linq;
using System.Text.Json;
using DebugStudio.Export.Elastic;
using DebugStudio.Export.Elastic.Kibana;

namespace DebugStudio.Export.Tests.Elastic.Kibana;

/// <summary>
/// saved search / lens が参照するフィールドが、対応する index template に mapping されていることを検算する。
///
/// V1〜V10 は正本 NDJSON の内部構造しか見ないので、「参照先が Elastic 側に実在するか」は
/// ここでしか捕まえられない。これが無かったために実在しない `log.level` を参照した
/// saved search が C レビュー 3 巡と C' 監査を通過し、実地確認まで
/// 「import も描画も成功して 0 件」に気づけなかった。
///
/// V11（lens）と query 内フィールド参照もここで見る。正本に lens が 0 個でも検算は成立し、
/// lens が入った瞬間に効き始める。
/// </summary>
public sealed class KibanaSavedObjectFieldMappingTests
{
    [Fact]
    public async Task SavedSearchのcolumnsは対応するIndexTemplateにmappingされている()
    {
        var bundle = KibanaSavedObjectBundleParser.Parse(
            ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson());

        var (telemetryMapped, logMapped) = await LoadMappedFieldPathsAsync();

        AssertColumnsAreMapped(bundle, "debugstudio-telemetry-timeline", telemetryMapped);
        AssertColumnsAreMapped(bundle, "debugstudio-log-warnings", logMapped);
    }

    /// <summary>
    /// T6 / V11 — 合成 lens fixture で未 mapping フィールドを赤にし、正本（lens 0 個）では緑。
    /// </summary>
    [Fact]
    public async Task lensが参照するフィールドはIndexTemplateにmappingされている()
    {
        var (telemetryMapped, _) = await LoadMappedFieldPathsAsync();

        // 合成: 実在しないフィールド → 赤（空振りでないこと）
        var badBundle = KibanaSavedObjectBundleParser.Parse(
            """
            {"id":"lens-unmapped","type":"lens","attributes":{"title":"bad","state":{"datasourceStates":{"formBased":{"layers":{"l1":{"columns":{"c1":{"operationType":"average","sourceField":"payload.doesNotExist","field":"kind"}}}}}}}},"references":[]}
            """);
        Assert.True(badBundle.TryGetById("lens-unmapped", out var badLens));
        var badFields = IndexTemplateFieldMappingHelper.CollectLensReferencedFields(badLens!);
        Assert.Contains("payload.doesNotExist", badFields);
        Assert.Contains("kind", badFields);

        var badUnmapped = badFields.Where(f => !telemetryMapped.Contains(f)).ToArray();
        Assert.True(
            badUnmapped.Length > 0,
            "合成 lens の未 mapping フィールドが検出されなかった（V11 が空振り）。");
        Assert.Contains("payload.doesNotExist", badUnmapped);

        // 正本: lens 0 個でも成立（K3-4 前）
        var canonical = KibanaSavedObjectBundleParser.Parse(
            ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson());
        AssertLensFieldsAreMapped(canonical, telemetryMapped);
    }

    /// <summary>
    /// T7 / V11 拡張 — searchSourceJSON の query 内フィールドも mapping 検算する。
    /// </summary>
    [Fact]
    public async Task searchSourceJSONのquery内フィールドもmapping検算の対象になる()
    {
        var (telemetryMapped, logMapped) = await LoadMappedFieldPathsAsync();

        // 合成: query だけを実在しないフィールドへ → 赤
        var badNdjson = BuildSearchNdjson(
            columnsJson: """["category","message"]""",
            query: """payload.doesNotExist: ("warning" or "error")""");
        var badBundle = KibanaSavedObjectBundleParser.Parse(badNdjson);
        Assert.True(badBundle.TryGetById("s1", out var badSearch));
        var badQueryFields = IndexTemplateFieldMappingHelper.CollectSearchSourceQueryFields(badSearch!);
        Assert.Contains("payload.doesNotExist", badQueryFields);

        var badUnmapped = badQueryFields.Where(f => !logMapped.Contains(f)).ToArray();
        Assert.True(
            badUnmapped.Length > 0,
            "query 内の未 mapping フィールドが検出されなかった（T7 が空振り）。");
        Assert.Contains("payload.doesNotExist", badUnmapped);

        // 正本: log の query（log.level:...）と telemetry の空 query はともに mapping 済み（または参照なし）
        var canonical = KibanaSavedObjectBundleParser.Parse(
            ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson());
        AssertQueryFieldsAreMapped(canonical, "debugstudio-log-warnings", logMapped);
        AssertQueryFieldsAreMapped(canonical, "debugstudio-telemetry-timeline", telemetryMapped);
    }

    private static async Task<(HashSet<string> Telemetry, HashSet<string> Log)> LoadMappedFieldPathsAsync()
    {
        using var telemetryTemplate = JsonDocument.Parse(
            ElasticTelemetryIndexTemplateDefinition.CreateArtifactJson());
        var telemetryMapped = IndexTemplateFieldMappingHelper.CollectMappedFieldPaths(
            IndexTemplateFieldMappingHelper.MappingProperties(telemetryTemplate.RootElement));

        var logTemplatePath = Path.Combine(
            Path.GetTempPath(),
            $"debugstudio-log-index-template-{Guid.NewGuid():N}.json");
        try
        {
            await new ElasticLogIndexTemplateWriter().WriteAsync(logTemplatePath);

            using var logTemplate = JsonDocument.Parse(await File.ReadAllTextAsync(logTemplatePath));
            var logMapped = IndexTemplateFieldMappingHelper.CollectMappedFieldPaths(
                IndexTemplateFieldMappingHelper.MappingProperties(logTemplate.RootElement));
            return (telemetryMapped, logMapped);
        }
        finally
        {
            if (File.Exists(logTemplatePath))
            {
                File.Delete(logTemplatePath);
            }
        }
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

    private static void AssertQueryFieldsAreMapped(
        KibanaSavedObjectBundle bundle,
        string searchId,
        HashSet<string> mappedFieldPaths)
    {
        Assert.True(bundle.TryGetById(searchId, out var search));

        var unmapped = IndexTemplateFieldMappingHelper
            .CollectSearchSourceQueryFields(search!)
            .Where(f => !mappedFieldPaths.Contains(f))
            .ToArray();

        Assert.True(
            unmapped.Length == 0,
            $"saved search '{searchId}' の searchSourceJSON query に index template へ"
            + $" mapping されていないフィールドがある: {string.Join(", ", unmapped)}");
    }

    private static void AssertLensFieldsAreMapped(
        KibanaSavedObjectBundle bundle,
        HashSet<string> mappedFieldPaths)
    {
        foreach (var lens in bundle.Objects.Where(o => string.Equals(o.Type, "lens", StringComparison.Ordinal)))
        {
            var unmapped = IndexTemplateFieldMappingHelper
                .CollectLensReferencedFields(lens)
                .Where(f => !mappedFieldPaths.Contains(f))
                .ToArray();

            Assert.True(
                unmapped.Length == 0,
                $"lens '{lens.Id}' が参照するフィールドに index template へ mapping されていない"
                + $"ものがある: {string.Join(", ", unmapped)}");
        }
    }

    private static string[] ReadColumns(KibanaSavedObject search)
    {
        return search.Attributes.GetProperty("columns")
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .ToArray();
    }

    private static string BuildSearchNdjson(string columnsJson, string query)
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
            ["sort"] = JsonSerializer.Deserialize<JsonElement>("[]"),
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
