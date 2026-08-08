#nullable enable

using System.Linq;
using System.Text.Json;
using DebugStudio.Export.Elastic;
using DebugStudio.Export.Elastic.Kibana;

namespace DebugStudio.Export.Tests.Elastic.Kibana;

/// <summary>
/// saved search の columns が、対応する index template に mapping されているフィールドだけで
/// 構成されていることを検算する。
///
/// V1〜V6 は正本 NDJSON の内部構造しか見ないので、「参照先が Elastic 側に実在するか」は
/// ここでしか捕まえられない。これが無かったために実在しない `log.level` を参照した
/// saved search が C レビュー 3 巡と C' 監査を通過し、実地確認まで
/// 「import も描画も成功して 0 件」に気づけなかった。
/// </summary>
public sealed class KibanaSavedObjectFieldMappingTests
{
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
