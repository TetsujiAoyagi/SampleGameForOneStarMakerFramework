using DebugStudio.Export.Elastic;

/// <summary>
/// Elastic operator 向け artifact 一式（template / pipeline / import-telemetry.ps1 など）を生成する。
/// リポジトリに ps1 を常駐させず、必要なときに同じ契約で吐き出すための薄い入口。
/// </summary>
/// <remarks>
/// 使い方:
///   dotnet run --project tools/DebugStudio/src/DebugStudio.ElasticArtifactGen
///   dotnet run --project ... -- &lt;outputRoot&gt; [inputRoot]
/// </remarks>

var outputRoot = args.Length > 0
    ? args[0]
    : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DebugStudio",
        "elastic-artifacts");

// Filebeat が tail する L0 永続化ルート。未指定時は %LocalAppData%\DebugStudio。
var inputRoot = args.Length > 1
    ? args[1]
    : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DebugStudio");

Directory.CreateDirectory(outputRoot);

var bundle = await new ElasticArtifactBundleWriter()
    .WriteAsync(outputRoot, inputRoot)
    .ConfigureAwait(false);

Console.WriteLine($"Artifacts written to: {outputRoot}");
Console.WriteLine($"Filebeat input root (L0): {inputRoot}");
Console.WriteLine($"import-telemetry.ps1: {bundle.Layout.BulkImportCommandPath}");
Console.WriteLine($"invoke-ingest.ps1:    {bundle.Layout.IngestRunnerCommandPath}");
