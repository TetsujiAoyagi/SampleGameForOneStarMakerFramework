#nullable enable

using System.Text.Json;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// telemetry ingest pipeline の JSON 正本。
/// index template の default_pipeline と同じ名前を使う。
/// </summary>
public static class ElasticTelemetryIngestPipelineDefinition
{
    public const string PipelineName = "debugstudio-telemetry";

    private static readonly JsonSerializerOptions IndentedSerializerOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions CompactSerializerOptions = new()
    {
        WriteIndented = false,
    };

    public static string CreateBootstrapJson()
    {
        return JsonSerializer.Serialize(CreateDocument(), CompactSerializerOptions);
    }

    public static string CreateArtifactJson()
    {
        return JsonSerializer.Serialize(CreateDocument(), IndentedSerializerOptions);
    }

    public static object CreateDocument()
    {
        return new
        {
            description = "debugstudio telemetry ingest pipeline",
            processors = new object[]
            {
                new
                {
                    set = new
                    {
                        field = "stream",
                        value = "telemetry",
                    }
                },
                new
                {
                    set = new
                    {
                        field = "observer.name",
                        value = "DebugStudio",
                    }
                }
            }
        };
    }
}
