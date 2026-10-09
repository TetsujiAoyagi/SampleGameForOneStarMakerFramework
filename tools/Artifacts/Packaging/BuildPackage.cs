using System.Text.Json;
using System.Text.RegularExpressions;

namespace OneStarMaker.Artifacts.Packaging;

// Build metadata lives here; PackageIO owns only bounded archive I/O.
// Buildの所属・manifest規約をここに集め、ZIPの安全な入出力はPackageIOへ委ねる。
// EvidenceのtaskやソースrevisionをBuildの必須metadataに混ぜない。
public sealed class BuildPackage
{
    private static readonly Regex Token = new("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant);
    private static readonly Regex Hex32 = new("^[0-9a-f]{32}$", RegexOptions.CultureInvariant);
    private static readonly Regex Hex64 = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    public string RepositoryId { get; }
    public string Project { get; }
    public string Target { get; }
    public string Configuration { get; }
    public string BuildId { get; }
    public string SeriesId => $"{RepositoryId}/{Project}/{Target}/{Configuration}";

    public BuildPackage(string repositoryId, string project, string target, string configuration, string buildId)
    {
        if (!Hex64.IsMatch(repositoryId) || !Token.IsMatch(project) || !Token.IsMatch(target) ||
            !Token.IsMatch(configuration) || !Hex32.IsMatch(buildId))
            throw new InvalidDataException("Build identity invalid.");
        RepositoryId = repositoryId;
        Project = project;
        Target = target;
        Configuration = configuration;
        BuildId = buildId;
    }

    // 保存元のHEADを元Buildのrevisionとして推定しない。ここへ書くのは、
    // 呼出側の明示metadataと固定snapshotから検証したentry情報だけ。
    internal object Manifest(PackageFile[] files) => new
    {
        schemaVersion = 1, purpose = "build", repositoryId = RepositoryId, project = Project,
        target = Target, configuration = Configuration, seriesId = SeriesId, buildId = BuildId,
        files = files.Select(file => new { path = file.Path, bytes = file.Bytes, sha256 = file.Sha256 }).ToArray()
    };

    // ZIP内の宣言を鵜呑みにせず、呼出側が期待したBuild identityと照合する。
    // path/重複/個別bytes/hash/展開上限は共通PackageIO側でも検証される。
    internal void ValidateManifest(JsonElement manifest)
    {
        PackagePolicy.Fields(manifest, "schemaVersion", "purpose", "repositoryId", "project", "target",
            "configuration", "seriesId", "buildId", "files");
        if (PackagePolicy.Integer(manifest, "schemaVersion") != 1 ||
            PackagePolicy.String(manifest, "purpose") != "build" ||
            PackagePolicy.String(manifest, "repositoryId") != RepositoryId ||
            PackagePolicy.String(manifest, "project") != Project ||
            PackagePolicy.String(manifest, "target") != Target ||
            PackagePolicy.String(manifest, "configuration") != Configuration ||
            PackagePolicy.String(manifest, "seriesId") != SeriesId ||
            PackagePolicy.String(manifest, "buildId") != BuildId)
            throw new InvalidDataException("Build manifest identity invalid.");
        var files = manifest.GetProperty("files");
        if (files.ValueKind != JsonValueKind.Array || files.GetArrayLength() is < 1 or > 512)
            throw new InvalidDataException("Build entry count invalid.");
        long total = 0;
        foreach (var item in files.EnumerateArray())
        {
            PackagePolicy.Fields(item, "path", "bytes", "sha256");
            total = checked(total + PackagePolicy.Integer(item, "bytes"));
            if (total > 240L * 1024 * 1024) throw new InvalidDataException("Build snapshot too large.");
        }
    }

    // 512件は入力handle数、240 MiBは単発転送へ収めるsource総量の境界。
    // ZIPの最終256 MiB上限も共通writerで検査し、multipartへ暗黙移行しない。
    public PackageInfo CreateVerified(string operationDirectory, string snapshotDirectory,
        PackageFile[] expected, int budgetMilliseconds)
    {
        if (expected is null || expected.Length is < 1 or > 512 ||
            expected.Sum(file => checked(file.Bytes)) > 240L * 1024 * 1024)
            throw new InvalidDataException("Build snapshot too large.");
        // 40桁ゼロは既存I/Oのidentity引数を満たす内部値であり、Buildのrevisionではない。
        // build分岐のmanifestには出力せず、存在しない由来を捏造しない。
        return PackageIO.CreateVerified(operationDirectory, snapshotDirectory, new string('0', 40),
            new string('0', 40), RepositoryId, BuildId, expected, budgetMilliseconds, build: this);
    }

    // 取得側も同じidentity codecを渡す。外側の信頼hashとmanifest hashの両方を
    // 共通extractorへ渡してから、全entryを検証した出力だけを返す。
    public PackageInfo Extract(string packagePath, string operationDirectory, string expectedSha256,
        string expectedManifestSha256, int budgetMilliseconds) =>
        PackageIO.Extract(packagePath, operationDirectory, expectedSha256, expectedManifestSha256,
            new string('0', 40), new string('0', 40), RepositoryId, BuildId,
            budgetMilliseconds, "build", build: this);
}
