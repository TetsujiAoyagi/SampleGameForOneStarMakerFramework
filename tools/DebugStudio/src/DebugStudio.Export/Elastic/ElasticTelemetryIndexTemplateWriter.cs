#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// telemetry export record を受けるための index template writer。
/// ECS-lite 寄せは後続タスクで行うため、ここでは現在の export shape を安全に受ける mapping を出す。
/// </summary>
public sealed class ElasticTelemetryIndexTemplateWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public async Task WriteAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("An output path is required.", nameof(outputPath));
        }

        var directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var properties = new Dictionary<string, object?>
        {
            ["@timestamp"] = new { type = "date" },
            ["timestampUnixTimeMilliseconds"] = new { type = "long" },
            ["stream"] = new { type = "keyword" },
            ["source"] = new { type = "keyword" },
            ["name"] = new { type = "keyword" },
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
        };

        var document = new
        {
            index_patterns = new[] { "debugstudio-telemetry-*" },
            template = new
            {
                mappings = new
                {
                    properties
                }
            }
        };

        var json = JsonSerializer.Serialize(document, SerializerOptions);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken).ConfigureAwait(false);
    }
}
