#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// log ingest pipeline の最小 writer。
/// まずは stream 固定と observer 印付けだけに絞り、後続の schema alignment で processor を育てる。
/// </summary>
public sealed class ElasticLogIngestPipelineWriter
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

        var document = new
        {
            description = "debugstudio log ingest pipeline",
            processors = new object[]
            {
                // L0 rolling NDJSON は log level を flat な `logLevel` で出し、Filebeat はそれを
                // event root にそのまま復元する。一方 ElasticBulkLogExportWriter と本 index template は
                // ECS 寄せの `log.level` を正本にしている。同じ debugstudio-log-* に 2 つの shape が
                // 混ざると Kibana の saved search がどちらか片方しか拾えないため、
                // 投入経路によらず必ず通る default_pipeline のここで `log.level` へ正規化する。
                // bulk 経路の document には `logLevel` が無いので ignore_missing で素通りさせる。
                new
                {
                    rename = new
                    {
                        field = "logLevel",
                        target_field = "log.level",
                        ignore_missing = true,
                    }
                },
                new
                {
                    set = new
                    {
                        field = "stream",
                        value = "log",
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

        var json = JsonSerializer.Serialize(document, SerializerOptions);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken).ConfigureAwait(false);
    }
}
