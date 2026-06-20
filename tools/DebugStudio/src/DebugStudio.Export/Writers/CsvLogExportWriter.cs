#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Writers;

/// <summary>
/// log export record を CSV へ書き出す。
/// </summary>
public sealed class CsvLogExportWriter : ILogExportWriter
{
    public LogExportFormat Format => LogExportFormat.Csv;

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
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await writer.WriteLineAsync("sequenceNumber,applicationName,timestampUnixTimeMilliseconds,timestampLocal,kind,rawLogLevel,category,eventId,eventName,message,exception,threadId,threadName,memberName,filePath,lineNumber".AsMemory(), cancellationToken).ConfigureAwait(false);

        foreach (var log in logs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = string.Join(",",
                Escape(log.SequenceNumber.ToString()),
                Escape(log.ApplicationName),
                Escape(log.TimestampUnixTimeMilliseconds.ToString()),
                Escape(log.TimestampLocal),
                Escape(log.Kind),
                Escape(log.RawLogLevel.ToString()),
                Escape(log.Category),
                Escape(log.EventId.ToString()),
                Escape(log.EventName),
                Escape(log.Message),
                Escape(log.Exception),
                Escape(log.ThreadId.ToString()),
                Escape(log.ThreadName),
                Escape(log.MemberName),
                Escape(log.FilePath),
                Escape(log.LineNumber.ToString()));

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
