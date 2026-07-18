#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Export.Elastic;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Writers;

/// <summary>
/// telemetry export record を Elastic `_bulk` API へそのまま流し込みやすい NDJSON へ変換する writer。
///
/// <para>
/// Elastic 固有の責務は WPF app ではなく export project 側へ閉じ込める。
/// これにより UI と ops artifact の境界を保ったまま、CLI や CI からも再利用しやすくする。
/// </para>
/// </summary>
public sealed class ElasticBulkTelemetryExportWriter : ITelemetryExportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public TelemetryExportFormat Format => TelemetryExportFormat.ElasticBulk;

    public async Task WriteAsync(IReadOnlyList<TelemetryExportRecord> records, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("An output path is required.", nameof(outputPath));
        }

        var directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await using var stream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        await using var writer = new StreamWriter(stream);

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var indexName = BuildIndexName(record);
            var actionLine = JsonSerializer.Serialize(
                new ElasticBulkActionLine(new ElasticBulkActionMetadata(indexName)),
                SerializerOptions);
            var payloadLine = JsonSerializer.Serialize(ToElasticPayload(record), SerializerOptions);

            await writer.WriteLineAsync(actionLine.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.WriteLineAsync(payloadLine.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static object ToElasticPayload(TelemetryExportRecord record)
    {
        var document = ElasticTelemetryDocumentFactory.Create(record);

        // 匿名型の `@timestamp` は C# ではプロパティ名 `timestamp` になる。
        // Kibana Data View / ECS は `@timestamp` を前提にするため、Dictionary で明示する。
        return new Dictionary<string, object?>
        {
            ["@timestamp"] = record.TimestampUtc,
            ["timestampUnixTimeMilliseconds"] = record.TimestampUnixTimeMilliseconds,
            ["stream"] = record.Stream,
            ["source"] = record.Source,
            ["name"] = record.Name,
            ["status"] = record.Status,
            ["message"] = record.Message,
            ["isSuccess"] = record.IsSuccess,
            ["elapsedMs"] = record.ElapsedMs,
            ["level"] = record.Level,
            ["traceId"] = record.TraceId,
            ["spanId"] = record.SpanId,
            ["parentSpanId"] = record.ParentSpanId,
            ["tagBits"] = record.TagBits,
            ["tags"] = record.Tags,
            ["cpuTime"] = record.CpuTime,
            ["gpuTime"] = record.GpuTime,
            ["managedMem"] = record.ManagedMem,
            ["nativeMem"] = record.NativeMem,
            ["sceneFrom"] = record.SceneFrom,
            ["sceneTo"] = record.SceneTo,
            ["cameraTotalViewCount"] = record.CameraTotalViewCount,
            ["cameraAdditionalViewCount"] = record.CameraAdditionalViewCount,
            ["cameraBlendingViewCount"] = record.CameraBlendingViewCount,
            ["cameraMaxStackDepthTotal"] = record.CameraMaxStackDepthTotal,
            ["cameraViewId"] = record.CameraViewId,
            ["cameraActiveCameraHash"] = record.CameraActiveCameraHash,
            ["event"] = new
            {
                category = document.Event.Category,
                action = document.Event.Action,
            },
            ["trace"] = new
            {
                id = document.Trace.Id,
            },
            ["span"] = new
            {
                id = document.Span.Id,
                parent = new
                {
                    id = document.Span.ParentId,
                }
            },
            ["service"] = new
            {
                name = document.Service.Name,
            }
        };
    }

    /// <summary>
    /// stream ごとに index を分ける。
    /// telemetry と service status は用途が異なるため、最初から別 index にしておく方が後続の mapping 管理が楽。
    /// </summary>
    private static string BuildIndexName(TelemetryExportRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var prefix = string.Equals(record.Stream, "serviceStatus", StringComparison.Ordinal)
            ? "debugstudio-service-status"
            : "debugstudio-telemetry";

        if (record.TimestampUnixTimeMilliseconds <= 0)
        {
            return prefix + "-1970.01.01";
        }

        try
        {
            var utc = DateTimeOffset.FromUnixTimeMilliseconds(record.TimestampUnixTimeMilliseconds).UtcDateTime;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{prefix}-{utc:yyyy.MM.dd}");
        }
        catch
        {
            return prefix + "-1970.01.01";
        }
    }

    private sealed class ElasticBulkActionLine
    {
        public ElasticBulkActionLine(ElasticBulkActionMetadata create)
        {
            Create = create;
        }

        public ElasticBulkActionMetadata Create { get; }
    }

    private sealed class ElasticBulkActionMetadata
    {
        public ElasticBulkActionMetadata(string index)
        {
            Index = index;
        }

        /// <summary>
        /// Elastic `_bulk` の action metadata では index 名は必須で `_index`。
        /// camelCase の `index` だと unknown parameter として 400 になる。
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("_index")]
        public string Index { get; }
    }
}
