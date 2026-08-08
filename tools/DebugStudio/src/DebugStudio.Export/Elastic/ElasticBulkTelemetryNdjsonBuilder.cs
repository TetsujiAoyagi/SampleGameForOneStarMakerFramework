#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// telemetry export record を Elastic `_bulk` 向け NDJSON へ変換する builder。
/// ファイル export と HTTP `_bulk` が byte レベルで同一になるよう、生成経路を 1 箇所に集約する。
/// </summary>
public static class ElasticBulkTelemetryNdjsonBuilder
{
    public const string BulkContentType = "application/x-ndjson";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        // Contract v3: null フィールド（sample の elapsedMs 等）はキー省略
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// UTF-8 NDJSON payload を構築する。末尾改行を必ず付ける。
    /// </summary>
    public static byte[] BuildBulkPayload(IReadOnlyList<TelemetryExportRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        using var stream = new MemoryStream();
        WriteBulkPayload(records, stream);
        return stream.ToArray();
    }

    /// <summary>
    /// 任意の stream へ NDJSON を書き出す。file writer もこの経路を使う。
    /// </summary>
    public static void WriteBulkPayload(IReadOnlyList<TelemetryExportRecord> records, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(destination);

        using var writer = new StreamWriter(destination, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 4096, leaveOpen: true);

        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            var actionLine = JsonSerializer.Serialize(
                new ElasticBulkActionLine(new ElasticBulkActionMetadata(BuildIndexName(record))),
                SerializerOptions);
            var payloadLine = JsonSerializer.Serialize(CreatePayloadDictionary(record), SerializerOptions);

            writer.WriteLine(actionLine);
            writer.WriteLine(payloadLine);
        }

        writer.Flush();
    }

    internal static Dictionary<string, object?> CreatePayloadDictionary(TelemetryExportRecord record)
    {
        var document = ElasticTelemetryDocumentFactory.Create(record);

        // 匿名型の `@timestamp` は C# ではプロパティ名 `timestamp` になる。
        // Kibana Data View / ECS は `@timestamp` を前提にするため、Dictionary で明示する。
        var payload = new Dictionary<string, object?>
        {
            ["@timestamp"] = record.TimestampUtc,
            ["timestampUnixTimeMilliseconds"] = record.TimestampUnixTimeMilliseconds,
            ["stream"] = record.Stream,
            ["source"] = record.Source,
            ["name"] = record.Name,
            // Contract v3: kind / schemaVersion / payload を Elastic 文書の正本として載せる
            ["kind"] = record.Kind,
            ["schemaVersion"] = record.SchemaVersion,
            ["payload"] = record.Payload,
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
            ["sessionId"] = record.SessionId,
            ["producerSequence"] = record.ProducerSequence,
            ["unityFrameAtStart"] = record.UnityFrameAtStart,
            ["unityFrameAtEnd"] = record.UnityFrameAtEnd,
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

        // Dictionary の null 値は WhenWritingNull でもキーが残るため、欠測はエントリ自体を入れない。
        AddIfPresent(payload, "buildVersion", record.BuildVersion);
        AddIfPresent(payload, "platform", record.Platform);
        AddIfPresent(payload, "deviceModel", record.DeviceModel);
        AddIfPresent(payload, "osVersion", record.OsVersion);
        AddIfPresent(payload, "engineVersion", record.EngineVersion);
        return payload;
    }

    private static void AddIfPresent(Dictionary<string, object?> payload, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            payload[key] = value;
        }
    }

    /// <summary>
    /// stream ごとに index を分ける。
    /// telemetry と service status は用途が異なるため、最初から別 index にしておく方が後続の mapping 管理が楽。
    /// </summary>
    internal static string BuildIndexName(TelemetryExportRecord record)
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
        [JsonPropertyName("_index")]
        public string Index { get; }
    }
}
