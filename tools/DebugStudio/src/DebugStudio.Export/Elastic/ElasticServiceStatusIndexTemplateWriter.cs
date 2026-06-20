#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// service status export record を受けるための index template writer。
/// こちらもまずは現在の export shape を安全に受けることだけを目的にする。
/// </summary>
public sealed class ElasticServiceStatusIndexTemplateWriter
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
            ["status"] = new { type = "keyword" },
            ["message"] = new { type = "text" },
        };

        var document = new
        {
            index_patterns = new[] { "debugstudio-service-status-*" },
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
