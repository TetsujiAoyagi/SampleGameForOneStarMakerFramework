#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// 現在の NDJSON log export を受けるための index template writer。
/// log 側も telemetry と同じ Elastic project で受けることで、後続の横断可視化に備える。
/// </summary>
public sealed class ElasticLogIndexTemplateWriter
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
            ["sequenceNumber"] = new { type = "long" },
            ["applicationName"] = new { type = "keyword" },
            ["timestampUnixTimeMilliseconds"] = new { type = "long" },
            ["timestampLocal"] = new { type = "keyword" },
            ["kind"] = new { type = "keyword" },
            ["rawLogLevel"] = new { type = "integer" },
            ["category"] = new { type = "keyword" },
            ["message"] = new { type = "text" },
            ["exception"] = new { type = "text" },
            ["threadId"] = new { type = "integer" },
            ["threadName"] = new { type = "keyword" },
            ["memberName"] = new { type = "keyword" },
            ["filePath"] = new { type = "keyword" },
            ["lineNumber"] = new { type = "integer" },
            ["event"] = new
            {
                properties = new
                {
                    id = new { type = "integer" },
                    name = new { type = "keyword" },
                }
            },
            ["log"] = new
            {
                properties = new
                {
                    level = new { type = "keyword" },
                    logger = new { type = "keyword" },
                }
            },
            ["service"] = new
            {
                properties = new
                {
                    name = new { type = "keyword" },
                }
            },
            ["sessionId"] = new { type = "keyword" },
            ["producerSequence"] = new { type = "long" },
            ["unityFrameAtEmit"] = new { type = "long" },
            ["traceId"] = new { type = "long" },
            ["spanId"] = new { type = "long" },
        };

        // Filebeat 入力の pipeline 指定に加え、template 側でも default_pipeline を固定する。
        // L1 Push は telemetry のみ bootstrap するため、artifact / import 経路で log 側が欠けると
        // Filebeat が pipeline 未登録で drop する。
        var document = new
        {
            index_patterns = new[] { "debugstudio-log-*" },
            template = new
            {
                settings = new
                {
                    index = new
                    {
                        default_pipeline = "debugstudio-log",
                    }
                },
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
