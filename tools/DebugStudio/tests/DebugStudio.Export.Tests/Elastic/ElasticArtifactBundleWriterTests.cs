#nullable enable

using System.IO;
using DebugStudio.Export.Elastic;

namespace DebugStudio.Export.Tests.Elastic;

/// <summary>
/// operator が迷わないよう、投入に必要な artifact 一式が
/// まとめて出ることを先に固定する。
/// </summary>
public sealed class ElasticArtifactBundleWriterTests
{
    [Fact]
    public async Task WriteAsync_投入に必要なartifact一式をまとめて出力する()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"debugstudio-elastic-bundle-{Guid.NewGuid():N}");
        var exportRoot = @"C:\Users\void\Documents\DebugStudio\exports";

        try
        {
            var writer = new ElasticArtifactBundleWriter();

            var bundle = await writer.WriteAsync(outputRoot, exportRoot);

            Assert.True(File.Exists(bundle.Layout.TelemetryIndexTemplatePath));
            Assert.True(File.Exists(bundle.Layout.LogIndexTemplatePath));
            Assert.True(File.Exists(bundle.Layout.TelemetryIngestPipelinePath));
            Assert.True(File.Exists(bundle.Layout.LogIngestPipelinePath));
            Assert.True(File.Exists(bundle.Layout.FilebeatConfigPath));
            Assert.True(File.Exists(bundle.Layout.BulkImportCommandPath));
            Assert.True(File.Exists(bundle.Layout.KibanaImportCommandPath));
            Assert.True(File.Exists(bundle.Layout.IngestRunnerCommandPath));
            Assert.True(File.Exists(bundle.Layout.KibanaSavedObjectsPath));
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }
}
