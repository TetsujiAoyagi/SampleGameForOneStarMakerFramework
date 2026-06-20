#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Writers;

/// <summary>
/// log export record を Elastic `_bulk` API 向け NDJSON へ変換する。
/// </summary>
public sealed class ElasticBulkLogExportWriter : ILogExportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public LogExportFormat Format => LogExportFormat.ElasticBulk;

    public async Task WriteAsync(IReadOnlyList<LogExportRecord> logs, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logs);

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

        foreach (var log in logs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var actionLine = JsonSerializer.Serialize(
                new ElasticBulkActionLine(new ElasticBulkActionMetadata(BuildIndexName(log))),
                SerializerOptions);
            var payloadLine = JsonSerializer.Serialize(ToElasticPayload(log), SerializerOptions);

            await writer.WriteLineAsync(actionLine.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.WriteLineAsync(payloadLine.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string BuildIndexName(LogExportRecord log)
    {
        ArgumentNullException.ThrowIfNull(log);

        if (log.TimestampUnixTimeMilliseconds <= 0)
        {
            return "debugstudio-log-1970.01.01";
        }

        var utc = DateTimeOffset.FromUnixTimeMilliseconds(log.TimestampUnixTimeMilliseconds).UtcDateTime;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"debugstudio-log-{utc:yyyy.MM.dd}");
    }

    private static object ToElasticPayload(LogExportRecord log)
    {
        return new
        {
            @timestamp = log.TimestampUtc,
            log.SequenceNumber,
            log.ApplicationName,
            log.TimestampUnixTimeMilliseconds,
            log.TimestampLocal,
            log.Kind,
            log.RawLogLevel,
            log.Category,
            log.EventId,
            log.EventName,
            log.Message,
            log.Exception,
            log.ThreadId,
            log.ThreadName,
            log.MemberName,
            log.FilePath,
            log.LineNumber,
            @event = new
            {
                id = log.EventId,
                name = log.EventName,
            },
            log = new
            {
                level = log.LogLevel,
                logger = log.Category,
            },
            service = new
            {
                name = log.ServiceName ?? log.ApplicationName,
            }
        };
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

        public string Index { get; }
    }
}
