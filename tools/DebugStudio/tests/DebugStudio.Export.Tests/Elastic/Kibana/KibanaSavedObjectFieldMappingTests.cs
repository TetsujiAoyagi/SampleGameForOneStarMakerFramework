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
/// lens が入った瞬間に効き始める。照合先 mapping は references の index-pattern id で振り分ける。
/// </summary>
public sealed class KibanaSavedObjectFieldMappingTests
{
    [Fact]
    public async Task SavedSearchのcolumnsは対応するIndexTemplateにmappingされている()
    {
        var bundle = KibanaSavedObjectBundleParser.Parse(
            ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson());

        var (telemetryMapped, logMapped) = await LoadMappedFieldPathsAsync();

        AssertColumnsAreMapped(bundle, "debugstudio-telemetry-timeline", telemetryMapped, logMapped);
        AssertColumnsAreMapped(bundle, "debugstudio-log-warnings", telemetryMapped, logMapped);
    }

    /// <summary>
    /// T6 / V11 — references の data view で mapping を振り分け、赤→緑の順で確認する。
    /// </summary>
    [Fact]
    public async Task lensが参照するフィールドはIndexTemplateにmappingされている()
    {
        var (telemetryMapped, logMapped) = await LoadMappedFieldPathsAsync();

        // 1) 赤: log data view なのに telemetry 専用フィールド → unmapped
        var redBundle = KibanaSavedObjectBundleParser.Parse(
            BuildLensNdjson(
                id: "lens-log-telemetry-field",
                sourceField: "payload.cpuMs",
                dataViewId: IndexTemplateFieldMappingHelper.LogDataViewId));
        Assert.True(redBundle.TryGetById("lens-log-telemetry-field", out var redLens));
        Assert.True(
            IndexTemplateFieldMappingHelper.TryResolveMappedFieldPaths(
                redLens!, telemetryMapped, logMapped, out var redMapped, out var redResolveFailure),
            redResolveFailure);
        var redFields = IndexTemplateFieldMappingHelper.CollectLensReferencedFields(redLens!);
        Assert.Contains("payload.cpuMs", redFields);
        var redUnmapped = redFields.Where(f => !redMapped!.Contains(f)).ToArray();
        Assert.True(
            redUnmapped.Length > 0,
            "log data view + payload.cpuMs が赤にならなかった（V11 振り分けが空振り）。");
        Assert.Contains("payload.cpuMs", redUnmapped);

        // 2) 緑: 同じ log data view で log 専用フィールド
        var greenBundle = KibanaSavedObjectBundleParser.Parse(
            BuildLensNdjson(
                id: "lens-log-level",
                sourceField: "log.level",
                dataViewId: IndexTemplateFieldMappingHelper.LogDataViewId));
        AssertLensFieldsAreMapped(greenBundle, telemetryMapped, logMapped);

        // 3) 緑: Lens count の sentinel ___records___ は mapping に無くても除外されて通る
        var recordsBundle = KibanaSavedObjectBundleParser.Parse(
            BuildLensNdjson(
                id: "lens-count-records",
                sourceField: "___records___",
                dataViewId: IndexTemplateFieldMappingHelper.LogDataViewId));
        Assert.True(recordsBundle.TryGetById("lens-count-records", out var recordsLens));
        var recordsFields = IndexTemplateFieldMappingHelper.CollectLensReferencedFields(recordsLens!);
        Assert.DoesNotContain("___records___", recordsFields);
        AssertLensFieldsAreMapped(recordsBundle, telemetryMapped, logMapped);

        // 4) 赤: sentinel 以外の実在しないフィールドは引き続き unmapped
        var missingBundle = KibanaSavedObjectBundleParser.Parse(
            BuildLensNdjson(
                id: "lens-missing-field",
                sourceField: "payload.doesNotExist",
                dataViewId: IndexTemplateFieldMappingHelper.LogDataViewId));
        Assert.True(missingBundle.TryGetById("lens-missing-field", out var missingLens));
        Assert.True(
            IndexTemplateFieldMappingHelper.TryResolveMappedFieldPaths(
                missingLens!, telemetryMapped, logMapped, out var missingMapped, out var missingResolveFailure),
            missingResolveFailure);
        var missingFields = IndexTemplateFieldMappingHelper.CollectLensReferencedFields(missingLens!);
        Assert.Contains("payload.doesNotExist", missingFields);
        var missingUnmapped = missingFields.Where(f => !missingMapped!.Contains(f)).ToArray();
        Assert.Contains("payload.doesNotExist", missingUnmapped);

        // 正本: lens 0 個でも成立（K3-4 前）
        var canonical = KibanaSavedObjectBundleParser.Parse(
            ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson());
        AssertLensFieldsAreMapped(canonical, telemetryMapped, logMapped);
    }

    /// <summary>
    /// T7 / V11 拡張 — searchSourceJSON の query 内フィールドも mapping 検算する。
    /// 振り分けは columns と同様に references の index-pattern で行う（既に同系統）。
    /// </summary>
    [Fact]
    public async Task searchSourceJSONのquery内フィールドもmapping検算の対象になる()
    {
        var (telemetryMapped, logMapped) = await LoadMappedFieldPathsAsync();

        // 合成: log data view + 実在しないフィールド → 赤
        var badNdjson = BuildSearchNdjson(
            columnsJson: """["category","message"]""",
            query: """payload.doesNotExist: ("warning" or "error")""",
            dataViewId: IndexTemplateFieldMappingHelper.LogDataViewId);
        var badBundle = KibanaSavedObjectBundleParser.Parse(badNdjson);
        Assert.True(badBundle.TryGetById("s1", out var badSearch));
        Assert.True(
            IndexTemplateFieldMappingHelper.TryResolveMappedFieldPaths(
                badSearch!, telemetryMapped, logMapped, out var badMapped, out var badResolveFailure),
            badResolveFailure);
        var badQueryFields = IndexTemplateFieldMappingHelper.CollectSearchSourceQueryFields(badSearch!);
        Assert.Contains("payload.doesNotExist", badQueryFields);

        var badUnmapped = badQueryFields.Where(f => !badMapped!.Contains(f)).ToArray();
        Assert.True(
            badUnmapped.Length > 0,
            "query 内の未 mapping フィールドが検出されなかった（T7 が空振り）。");
        Assert.Contains("payload.doesNotExist", badUnmapped);

        // 正本: log の query（log.level:...）と telemetry の空 query はともに mapping 済み（または参照なし）
        var canonical = KibanaSavedObjectBundleParser.Parse(
            ElasticKibanaSavedObjectsWriter.ReadSavedObjectsNdjson());
        AssertQueryFieldsAreMapped(canonical, "debugstudio-log-warnings", telemetryMapped, logMapped);
        AssertQueryFieldsAreMapped(canonical, "debugstudio-telemetry-timeline", telemetryMapped, logMapped);
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
        HashSet<string> telemetryMapped,
        HashSet<string> logMapped)
    {
        Assert.True(bundle.TryGetById(searchId, out var search));
        Assert.True(
            IndexTemplateFieldMappingHelper.TryResolveMappedFieldPaths(
                search!, telemetryMapped, logMapped, out var mapped, out var failureReason),
            failureReason);

        var unmapped = ReadColumns(search!).Where(c => !mapped!.Contains(c)).ToArray();

        Assert.True(
            unmapped.Length == 0,
            $"saved search '{searchId}' の columns に index template へ mapping されていない"
            + $"フィールドがある: {string.Join(", ", unmapped)}");
    }

    private static void AssertQueryFieldsAreMapped(
        KibanaSavedObjectBundle bundle,
        string searchId,
        HashSet<string> telemetryMapped,
        HashSet<string> logMapped)
    {
        Assert.True(bundle.TryGetById(searchId, out var search));
        Assert.True(
            IndexTemplateFieldMappingHelper.TryResolveMappedFieldPaths(
                search!, telemetryMapped, logMapped, out var mapped, out var failureReason),
            failureReason);

        var unmapped = IndexTemplateFieldMappingHelper
            .CollectSearchSourceQueryFields(search!)
            .Where(f => !mapped!.Contains(f))
            .ToArray();

        Assert.True(
            unmapped.Length == 0,
            $"saved search '{searchId}' の searchSourceJSON query に index template へ"
            + $" mapping されていないフィールドがある: {string.Join(", ", unmapped)}");
    }

    private static void AssertLensFieldsAreMapped(
        KibanaSavedObjectBundle bundle,
        HashSet<string> telemetryMapped,
        HashSet<string> logMapped)
    {
        foreach (var lens in bundle.Objects.Where(o => string.Equals(o.Type, "lens", StringComparison.Ordinal)))
        {
            Assert.True(
                IndexTemplateFieldMappingHelper.TryResolveMappedFieldPaths(
                    lens, telemetryMapped, logMapped, out var mapped, out var failureReason),
                failureReason);

            var unmapped = IndexTemplateFieldMappingHelper
                .CollectLensReferencedFields(lens)
                .Where(f => !mapped!.Contains(f))
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

    private static string BuildLensNdjson(string id, string sourceField, string dataViewId)
    {
        var state = new
        {
            datasourceStates = new
            {
                formBased = new
                {
                    layers = new
                    {
                        l1 = new
                        {
                            columns = new
                            {
                                c1 = new
                                {
                                    operationType = "count",
                                    sourceField,
                                },
                            },
                        },
                    },
                },
            },
        };

        var obj = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["type"] = "lens",
            ["attributes"] = new Dictionary<string, object?>
            {
                ["title"] = id,
                ["state"] = state,
            },
            ["references"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["id"] = dataViewId,
                    ["name"] = "indexpattern-datasource-layer-l1",
                    ["type"] = "index-pattern",
                },
            },
        };

        return JsonSerializer.Serialize(obj) + "\n";
    }

    private static string BuildSearchNdjson(string columnsJson, string query, string dataViewId)
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
            ["references"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["id"] = dataViewId,
                    ["name"] = "kibanaSavedObjectMeta.searchSourceJSON.index",
                    ["type"] = "index-pattern",
                },
            },
        };

        return JsonSerializer.Serialize(obj) + "\n";
    }
}
