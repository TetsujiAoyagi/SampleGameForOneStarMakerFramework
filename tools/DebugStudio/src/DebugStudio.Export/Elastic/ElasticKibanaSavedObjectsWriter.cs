#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// Kibana import 用の saved objects bundle を出力する。
/// 正本は埋め込みリソースの NDJSON。組み立ては行わない。
/// </summary>
public sealed class ElasticKibanaSavedObjectsWriter
{
    public const string ResourceName = "DebugStudio.Export.Elastic.Kibana.debugstudio-overview.ndjson";

    /// <summary>
    /// 正本 NDJSON をそのまま読み出す。テストからも同じ内容を検算できるよう public にする。
    /// </summary>
    public static string ReadSavedObjectsNdjson()
    {
        using var stream = typeof(ElasticKibanaSavedObjectsWriter).Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded Kibana saved objects resource '{ResourceName}' was not found.");

        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return reader.ReadToEnd();
    }

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

        await File.WriteAllTextAsync(
            outputPath,
            ReadSavedObjectsNdjson(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken).ConfigureAwait(false);
    }
}
