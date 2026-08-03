#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// telemetry index template の JSON 正本。
/// artifact ファイル出力と HTTP bootstrap の双方が同一定義を使う。
/// </summary>
public static class ElasticTelemetryIndexTemplateDefinition
{
    public const string TemplateName = "debugstudio-telemetry";

    public const string IndexPattern = "debugstudio-telemetry-*";

    public const string DefaultIngestPipelineName = ElasticTelemetryIngestPipelineDefinition.PipelineName;

    private static readonly JsonSerializerOptions IndentedSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions CompactSerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// bootstrap PUT 用の compact JSON を返す。
    /// </summary>
    public static string CreateBootstrapJson()
    {
        return JsonSerializer.Serialize(CreateDocument(), CompactSerializerOptions);
    }

    /// <summary>
    /// artifact 出力用の pretty JSON を返す。
    /// </summary>
    public static string CreateArtifactJson()
    {
        return JsonSerializer.Serialize(CreateDocument(), IndentedSerializerOptions);
    }

    /// <summary>
    /// template 本体。settings.index.default_pipeline を設定し、pipeline 登録だけでは ingest が走らない問題を避ける。
    /// </summary>
    public static object CreateDocument()
    {
        var properties = new Dictionary<string, object?>
        {
            ["@timestamp"] = new { type = "date" },
            ["timestampUnixTimeMilliseconds"] = new { type = "long" },
            ["stream"] = new { type = "keyword" },
            ["source"] = new { type = "keyword" },
            ["name"] = new { type = "keyword" },
            ["kind"] = new { type = "keyword" },
            ["schemaVersion"] = new { type = "integer" },
            ["payload"] = new
            {
                properties = new Dictionary<string, object?>
                {
                    ["shape"] = new { type = "keyword" },
                    ["targetIdentity"] = new { type = "keyword" },
                    ["stage"] = new { type = "keyword" },
                    ["managedBeforeBytes"] = new { type = "long" },
                    ["nativeBeforeBytes"] = new { type = "long" },
                    ["managedAfterBytes"] = new { type = "long" },
                    ["nativeAfterBytes"] = new { type = "long" },
                    ["managedDeltaBytes"] = new { type = "long" },
                    ["nativeDeltaBytes"] = new { type = "long" },
                    ["fps"] = new { type = "float" },
                    ["cpuMs"] = new { type = "float" },
                    ["gpuMs"] = new { type = "float" },
                    ["gpuAvailable"] = new { type = "boolean" },
                    ["managedBytes"] = new { type = "long" },
                    ["nativeBytes"] = new { type = "long" },
                    ["gcGen0Delta"] = new { type = "integer" },
                    ["unityFrame"] = new { type = "integer" },
                    ["cameraTotalViewCount"] = new { type = "integer" },
                    ["cameraAdditionalViewCount"] = new { type = "integer" },
                    ["cameraBlendingViewCount"] = new { type = "integer" },
                    ["cameraMaxStackDepthTotal"] = new { type = "integer" },
                }
            },
            ["message"] = new { type = "text" },
            ["isSuccess"] = new { type = "boolean" },
            ["elapsedMs"] = new { type = "double" },
            ["level"] = new { type = "integer" },
            ["traceId"] = new { type = "long" },
            ["spanId"] = new { type = "long" },
            ["parentSpanId"] = new { type = "long" },
            ["tagBits"] = new { type = "integer" },
            ["tags"] = new { type = "keyword" },
            ["event"] = new
            {
                properties = new
                {
                    category = new { type = "keyword" },
                    action = new { type = "keyword" },
                }
            },
            ["trace"] = new
            {
                properties = new
                {
                    id = new { type = "keyword" },
                }
            },
            ["span"] = new
            {
                properties = new Dictionary<string, object?>
                {
                    ["id"] = new { type = "keyword" },
                    ["parent"] = new
                    {
                        properties = new
                        {
                            id = new { type = "keyword" },
                        }
                    }
                }
            },
            ["service"] = new
            {
                properties = new
                {
                    name = new { type = "keyword" },
                }
            },
            ["cpuTime"] = new { type = "float" },
            ["gpuTime"] = new { type = "float" },
            ["managedMem"] = new { type = "long" },
            ["nativeMem"] = new { type = "long" },
            ["sceneFrom"] = new { type = "integer" },
            ["sceneTo"] = new { type = "integer" },
            ["cameraTotalViewCount"] = new { type = "integer" },
            ["cameraAdditionalViewCount"] = new { type = "integer" },
            ["cameraBlendingViewCount"] = new { type = "integer" },
            ["cameraMaxStackDepthTotal"] = new { type = "integer" },
            ["cameraViewId"] = new { type = "integer" },
            ["cameraActiveCameraHash"] = new { type = "integer" },
            ["sessionId"] = new { type = "keyword" },
            ["producerSequence"] = new { type = "long" },
            ["unityFrameAtStart"] = new { type = "long" },
            ["unityFrameAtEnd"] = new { type = "long" },
        };

        return new ElasticTelemetryIndexTemplateDocument(
            new[] { IndexPattern },
        new ElasticTelemetryIndexTemplateBody(
                new ElasticTelemetryIndexTemplateSettings(
                    new ElasticTelemetryIndexSettings(DefaultIngestPipelineName)),
                new ElasticTelemetryIndexTemplateMappings(properties)));
    }

    private sealed record ElasticTelemetryIndexTemplateDocument(
        [property: JsonPropertyName("index_patterns")] string[] IndexPatterns,
        ElasticTelemetryIndexTemplateBody Template);

    private sealed record ElasticTelemetryIndexTemplateBody(
        ElasticTelemetryIndexTemplateSettings Settings,
        ElasticTelemetryIndexTemplateMappings Mappings);

    private sealed record ElasticTelemetryIndexTemplateSettings(
        ElasticTelemetryIndexSettings Index);

    private sealed record ElasticTelemetryIndexSettings(
        [property: JsonPropertyName("default_pipeline")] string DefaultPipeline);

    private sealed record ElasticTelemetryIndexTemplateMappings(object Properties);
}
