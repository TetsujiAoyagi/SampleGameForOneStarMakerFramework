#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

// build をまたぐ content file の同一性のスパイク（docs/handoff/CONTENT_FILE_IDENTITY_SPIKE.md）の判定の中核。
// 入力は ContentLayout.json の写しと出力 directory の file の一覧（名前・size・sha256）だけで、Unity の型も製品の型も
// 受け取らない。判定に使う語と判定定義は HANDOFF §2 で実測の前に固定したもので、ここで意味を変えない。
// 実験 code であり、Phase D で git 履歴へ退避して削除する。
namespace OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity
{
    // ContentLayout.json（Version 2）の、判定に使う欄だけの写し。JsonUtility が埋める。欄が無ければ初期値のままなので、
    // 空文字・空配列を「読めない欄」として扱う（Read）。
    [Serializable] internal sealed class LayoutDocument
    {
        public int Version;
        public string BuildManifestHash = "";
        public LayoutSerializedFile[] SerializedFiles = Array.Empty<LayoutSerializedFile>();
        public string[] RootAssets = Array.Empty<string>();
        public LayoutObjectId[] LoadableObjectIds = Array.Empty<LayoutObjectId>();
        public LayoutBinaryArtifact[] BinaryArtifacts = Array.Empty<LayoutBinaryArtifact>();
    }

    [Serializable] internal sealed class LayoutSerializedFile
    {
        public int Index;
        public string ID = "";
        public bool IsBuiltIn;
        public string[] SourceAssets = Array.Empty<string>();
        public string ContentHash = "";
    }

    [Serializable] internal sealed class LayoutObjectId
    {
        public string ObjectIdHash = "";
        public string AssetPath = "";
        public int SerializedFile = -1;
    }

    [Serializable] internal sealed class LayoutBinaryArtifact
    {
        public int Index;
        public string ContentHash = "";
        public string Category = "";
        public long Size;
        public int[] ArtifactReferences = Array.Empty<int>();
    }

    // 出力 directory の直下の 1 file。
    [Serializable] internal sealed class OutputFile
    {
        public string Name = "";
        public long Size;
        public string Sha256 = "";
    }

    // 棚卸しの 1 行。同じ bytes の file が複数の id に当たれば id ごとに 1 行になる（bytes は sha256 で重複を除いて数える）。
    [Serializable] internal sealed class InventoryFile
    {
        public string Id = "";
        public string Name = "";
        public long Size;
        public string Sha256 = "";
        public string Kind = "";
        public string[] SourceAssets = Array.Empty<string>();
        // 除外する file（HANDOFF §2 の 3 つ）: "root"、"manifest"、"pointer"。空なら比べる。
        public string Excluded = "";
    }

    [Serializable] internal sealed class SourceAssetFiles
    {
        public string SourceAsset = "";
        public string Kind = "";
        public string[] Ids = Array.Empty<string>();
    }

    [Serializable] internal sealed class BuildInventory
    {
        public string Build = "";
        public string ManifestHash = "";
        public InventoryFile[] Files = Array.Empty<InventoryFile>();
        public SourceAssetFiles[] Sources = Array.Empty<SourceAssetFiles>();
        public string[] Unknown = Array.Empty<string>();
        public string[] Unreadable = Array.Empty<string>();
        // root 自身の file の source asset の一覧。RootColocated なら除外せず未判定にする。
        public string[] RootSources = Array.Empty<string>();
        public bool RootColocated;
        public string[] MultiSourceFiles = Array.Empty<string>();
        public long TotalBytes;
    }

    // 種類の分類は source asset で事前に固定する（HANDOFF §2）。中核の 3 種を先に見るのは、同じ file に shader 等が
    // 同居していても中核の差を shader の差として読まないため（厳しい側に倒す）。
    internal sealed class KindClassifier
    {
        public const string Texture = "texture", Material = "material", Prefab = "prefab", Shader = "shader",
            BuiltIn = "built-in", Script = "script", Scene = "scene", Other = "other";
        public static readonly string[] Core = { Texture, Material, Prefab };
        public static readonly string[] Tolerated = { Shader, BuiltIn, Script };

        public KindClassifier(string sharedTex, string sharedMat, string litPlain, string sharedPrefab, string sharedShader)
        { SharedTex = sharedTex; SharedMat = sharedMat; LitPlain = litPlain; SharedPrefab = sharedPrefab; SharedShader = sharedShader; }
        public string SharedTex { get; }
        public string SharedMat { get; }
        public string LitPlain { get; }
        public string SharedPrefab { get; }
        public string SharedShader { get; }

        public string Classify(IReadOnlyCollection<string> sources)
        {
            if (sources.Contains(SharedTex)) return Texture;
            if (sources.Contains(SharedMat) || sources.Contains(LitPlain)) return Material;
            if (sources.Contains(SharedPrefab)) return Prefab;
            if (sources.Any(s => s.EndsWith(".shader", StringComparison.OrdinalIgnoreCase)
                                 || s.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))) return Shader;
            if (sources.Contains("Resources/unity_builtin_extra")) return BuiltIn;
            if (sources.Count > 0 && sources.All(s => s.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))) return Script;
            if (sources.Any(s => s.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))) return Scene;
            return Other;
        }
    }

    internal static class FileInventory
    {
        public const string PointerName = "BuildManifestHash.txt";
        private const string VirtualDefaultResources = "Library/unity default resources";

        public static BuildInventory Read(string build, LayoutDocument? layout, IReadOnlyList<OutputFile> files,
            string pointerText, KindClassifier classifier)
        {
            var unreadable = new List<string>();
            var unknown = new List<string>();
            var entries = new List<InventoryFile>();
            var inventory = new BuildInventory { Build = build };
            if (layout == null) { inventory.Unreadable = new[] { "layout: missing" }; return inventory; }
            if (layout.Version != 2) unreadable.Add("layout: unsupported Version " + layout.Version);
            if (layout.BuildManifestHash.Length == 0) unreadable.Add("layout: BuildManifestHash missing");
            if (pointerText.Trim() != layout.BuildManifestHash) unreadable.Add("pointer: does not match layout BuildManifestHash");
            inventory.ManifestHash = layout.BuildManifestHash;

            // 内容 hash を持たない仮想の built-in 項目だけを比較と照合から外す。それ以外で欄が欠ければ読めない。
            var serialized = new List<LayoutSerializedFile>();
            foreach (var sf in layout.SerializedFiles)
            {
                if (sf.IsBuiltIn && sf.ID == VirtualDefaultResources && sf.ContentHash.Length == 0) continue;
                if (sf.ID.Length == 0 || sf.ContentHash.Length == 0) { unreadable.Add("serialized file " + sf.Index + ": ID or ContentHash missing"); continue; }
                serialized.Add(sf);
            }
            foreach (var artifact in layout.BinaryArtifacts)
                if (artifact.ContentHash.Length == 0 || artifact.Category.Length == 0)
                    unreadable.Add("binary artifact " + artifact.Index + ": ContentHash or Category missing");
            var artifactsByHash = layout.BinaryArtifacts.Where(x => x.ContentHash.Length != 0)
                .GroupBy(x => x.ContentHash, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
            var sfByHash = serialized.GroupBy(x => x.ContentHash, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);

            foreach (var file in files.OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                if (file.Name == PointerName)
                {
                    entries.Add(Entry("pointer", file, "pointer", Array.Empty<string>(), "pointer"));
                    continue;
                }
                var stem = file.Name.Contains('.') ? file.Name.Substring(0, file.Name.IndexOf('.')) : file.Name;
                if (!artifactsByHash.TryGetValue(stem, out var artifacts)) { unknown.Add(file.Name); continue; }
                foreach (var artifact in artifacts)
                {
                    if (artifact.Size != file.Size) unreadable.Add(file.Name + ": Size differs from the file");
                    if (artifact.Category == "manifest")
                    {
                        if (stem != layout.BuildManifestHash) unreadable.Add(file.Name + ": manifest is not the build's manifest");
                        entries.Add(Entry("manifest", file, "manifest", Array.Empty<string>(), "manifest"));
                    }
                    else if (artifact.Category == "contentfile")
                    {
                        if (!sfByHash.TryGetValue(stem, out var owners)) { unreadable.Add(file.Name + ": no serialized file has this hash"); continue; }
                        foreach (var sf in owners)
                            entries.Add(Entry(sf.ID, file, classifier.Classify(sf.SourceAssets), sf.SourceAssets, ""));
                    }
                    else
                    {
                        // .resS / .resource は、それを ArtifactReferences に持つ content file の ID と Category の組を id にする。
                        var owners = layout.BinaryArtifacts
                            .Where(x => x.Category == "contentfile" && x.ArtifactReferences.Contains(artifact.Index))
                            .SelectMany(x => sfByHash.TryGetValue(x.ContentHash, out var s) ? s : Array.Empty<LayoutSerializedFile>())
                            .ToArray();
                        if (owners.Length == 0) { unreadable.Add(file.Name + ": no content file references this artifact"); continue; }
                        foreach (var sf in owners)
                            entries.Add(Entry(sf.ID + "|" + artifact.Category, file, classifier.Classify(sf.SourceAssets), sf.SourceAssets, ""));
                    }
                }
            }
            // 照合は両方向で行う。layout にあって directory に無い file も未知として名前を残す。
            var onDisk = new HashSet<string>(files.Select(x => x.Name.Contains('.') ? x.Name.Substring(0, x.Name.IndexOf('.')) : x.Name), StringComparer.Ordinal);
            foreach (var hash in artifactsByHash.Keys.Where(x => !onDisk.Contains(x))) unknown.Add("absent from directory: " + hash);

            // root 自身の file は、source asset が生成した root の asset 1 つだけの時に限って除外する。
            if (layout.RootAssets.Length != 1) unreadable.Add("layout: RootAssets is not exactly one");
            else
            {
                var objectId = layout.LoadableObjectIds.FirstOrDefault(x => x.ObjectIdHash == layout.RootAssets[0]);
                var rootFile = objectId == null ? null : serialized.FirstOrDefault(x => x.Index == objectId.SerializedFile);
                if (objectId == null || rootFile == null) unreadable.Add("layout: root serialized file not found");
                else
                {
                    inventory.RootSources = rootFile.SourceAssets.ToArray();
                    inventory.RootColocated = !(rootFile.SourceAssets.Length == 1 && rootFile.SourceAssets[0] == objectId.AssetPath);
                    if (!inventory.RootColocated)
                        foreach (var entry in entries.Where(x => x.Id == rootFile.ID)) { entry.Excluded = "root"; entry.Kind = "root"; }
                }
            }

            inventory.Files = entries.ToArray();
            inventory.Sources = serialized.SelectMany(sf => sf.SourceAssets.Select(source => (source, sf.ID)))
                .GroupBy(x => x.source, StringComparer.Ordinal).OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new SourceAssetFiles { SourceAsset = x.Key, Kind = classifier.Classify(new[] { x.Key }),
                    Ids = x.Select(y => y.ID).Distinct().OrderBy(y => y, StringComparer.Ordinal).ToArray() }).ToArray();
            inventory.MultiSourceFiles = serialized.Where(x => x.SourceAssets.Length > 1)
                .Select(x => x.ID + " <- " + string.Join(", ", x.SourceAssets)).ToArray();
            inventory.Unknown = unknown.ToArray();
            inventory.Unreadable = unreadable.ToArray();
            inventory.TotalBytes = DistinctBytes(files.Select(x => (x.Sha256, x.Size)));
            return inventory;
        }

        private static InventoryFile Entry(string id, OutputFile file, string kind, string[] sources, string excluded) =>
            new InventoryFile { Id = id, Name = file.Name, Size = file.Size, Sha256 = file.Sha256, Kind = kind,
                SourceAssets = sources.ToArray(), Excluded = excluded };

        public static long DistinctBytes(IEnumerable<(string sha, long size)> files) =>
            files.GroupBy(x => x.sha, StringComparer.Ordinal).Sum(x => x.First().size);

        // 比べる 2 build の 2 つの軸（id ごとの bytes と、source asset ごとの帰属先 id）。changedSources に帰属する差は
        // 「想定した変更」として違った file の集計から外す。
        public static PairResult Compare(BuildInventory a, BuildInventory b, IReadOnlyCollection<string>? changedSources = null)
        {
            changedSources ??= Array.Empty<string>();
            var result = new PairResult { A = a.Build, B = b.Build };
            var left = a.Files.GroupBy(x => x.Id, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
            var right = b.Files.GroupBy(x => x.Id, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
            var compared = new Dictionary<string, int>(StringComparer.Ordinal);
            var differed = new List<PairItem>();
            var expected = new List<PairItem>();
            var oneSided = new List<PairItem>();
            foreach (var id in left.Keys.Union(right.Keys).OrderBy(x => x, StringComparer.Ordinal))
            {
                var inLeft = left.TryGetValue(id, out var l);
                var inRight = right.TryGetValue(id, out var r);
                var sample = (l ?? r)![0];
                if (!inLeft || !inRight)
                {
                    oneSided.Add(Item(id, sample, inLeft ? a.Build : b.Build, sample.Excluded.Length != 0));
                    continue;
                }
                if (sample.Excluded.Length != 0 || r![0].Excluded.Length != 0) continue;
                compared[sample.Kind] = compared.TryGetValue(sample.Kind, out var n) ? n + 1 : 1;
                var shaL = l!.Select(x => x.Sha256).OrderBy(x => x, StringComparer.Ordinal);
                var shaR = r.Select(x => x.Sha256).OrderBy(x => x, StringComparer.Ordinal);
                if (shaL.SequenceEqual(shaR)) continue;
                // 同じ id でも source asset の一覧は build ごとに違いうる。種類を左の build だけで決めると、右の build で
                // 中核の asset や Scene が同じ id に加わった差を script・shader の差として読み、判定を甘い側に倒す。
                // 種類は両側それぞれの分類を残し（どちらかが中核・Scene・その他なら N2 / N4 に入る）、source は和集合を残す。
                var item = Item(id, sample, "", false);
                item.KindB = r[0].Kind;
                item.SourceAssets = l.Concat(r).SelectMany(x => x.SourceAssets).Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray();
                item.SizeB = r[0].Size;
                var expectedChange = l.Concat(r).SelectMany(x => x.SourceAssets).Any(changedSources.Contains);
                (expectedChange ? expected : differed).Add(item);
            }
            var srcB = b.Sources.ToDictionary(x => x.SourceAsset, StringComparer.Ordinal);
            result.Moved = a.Sources.Where(x => srcB.TryGetValue(x.SourceAsset, out var y) && !x.Ids.SequenceEqual(y.Ids))
                .Select(x => new MovedSource { SourceAsset = x.SourceAsset, Kind = x.Kind, IdsA = x.Ids, IdsB = srcB[x.SourceAsset].Ids })
                .ToArray();
            result.Compared = compared.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "=" + x.Value).ToArray();
            result.ComparedKinds = compared.Keys.ToArray();
            result.Differed = differed.ToArray();
            result.ExpectedChanges = expected.ToArray();
            result.OneSided = oneSided.ToArray();
            return result;
        }

        private static PairItem Item(string id, InventoryFile file, string side, bool excluded) =>
            new PairItem { Id = id, Kind = file.Kind, SourceAssets = file.SourceAssets, SizeA = file.Size, Side = side, Excluded = excluded };

        // S の中核の 3 種と shader の source asset のうち、相手の layout に載っていないもの（不在: fixture の不備）。
        public static string[] Absent(BuildInventory s, BuildInventory other)
        {
            var present = new HashSet<string>(other.Sources.Select(x => x.SourceAsset), StringComparer.Ordinal);
            return s.Sources.Where(x => (KindClassifier.Core.Contains(x.Kind) || x.Kind == KindClassifier.Shader)
                                        && !present.Contains(x.SourceAsset)).Select(x => x.SourceAsset).ToArray();
        }

        // round の build で、基準の build に無い sha256 の bytes（配り直しになる量）。
        public static long RedistributedBytes(BuildInventory baseline, BuildInventory round)
        {
            var known = new HashSet<string>(baseline.Files.Select(x => x.Sha256), StringComparer.Ordinal);
            return DistinctBytes(round.Files.Where(x => !known.Contains(x.Sha256)).Select(x => (x.Sha256, x.Size)));
        }

        // AC1 の観測: 同じ名前が別の sha256 を持つ、または同じ sha256 が別の名前を持つ組を列挙する（合否にしない）。
        public static string[] NameShaBreaks(IEnumerable<BuildInventory> builds)
        {
            var files = builds.SelectMany(b => b.Files).Select(x => (x.Name, x.Sha256)).Distinct().ToArray();
            return files.GroupBy(x => x.Name, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => "name " + g.Key + " has " + g.Count() + " sha256")
                .Concat(files.GroupBy(x => x.Sha256, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => "sha256 " + g.Key + " has " + g.Count() + " names"))
                .ToArray();
        }
    }

    [Serializable] internal sealed class PairItem
    {
        public string Id = "";
        // 片方だけの id ではその側の file の種類。両方の build にある id では左の build の file の種類で、KindB に右の
        // build の file の種類が入る（片方だけなら KindB は空）。
        public string Kind = "";
        public string KindB = "";
        // 両方の build にある id では両側の source asset の和集合。
        public string[] SourceAssets = Array.Empty<string>();
        public long SizeA;
        public long SizeB;
        public string Side = "";
        public bool Excluded;
        public IEnumerable<string> Kinds => KindB.Length == 0 || KindB == Kind ? new[] { Kind } : new[] { Kind, KindB };
        public string KindLabel => string.Join("/", Kinds);
    }

    [Serializable] internal sealed class MovedSource
    {
        public string SourceAsset = "";
        public string Kind = "";
        public string[] IdsA = Array.Empty<string>();
        public string[] IdsB = Array.Empty<string>();
    }

    [Serializable] internal sealed class PairResult
    {
        public string A = "";
        public string B = "";
        public string[] Compared = Array.Empty<string>();
        public string[] ComparedKinds = Array.Empty<string>();
        public PairItem[] Differed = Array.Empty<PairItem>();
        public PairItem[] ExpectedChanges = Array.Empty<PairItem>();
        public PairItem[] OneSided = Array.Empty<PairItem>();
        public MovedSource[] Moved = Array.Empty<MovedSource>();
        public bool HasOneSidedOutsideExclusions => OneSided.Any(x => !x.Excluded);
        public IEnumerable<string> FindingKinds => Differed.SelectMany(x => x.Kinds).Concat(Moved.Select(x => x.Kind));
    }

    internal enum Conclusion { Undecided, NoGo, Go, ConditionalAccept }

    // AC5 の 1 round。Restore は差が無いことが合格の復元 round。
    internal sealed class Round
    {
        public Round(string baseline, string build, string[] changedSources, bool restore)
        { Baseline = baseline; Build = build; ChangedSources = changedSources; Restore = restore; }
        public string Baseline { get; }
        public string Build { get; }
        public string[] ChangedSources { get; }
        public bool Restore { get; }
    }

    // 判定の入力。harness の不備（build を開始できない、source の不一致、後始末の不備、数値の欠落 等）は
    // 呼び出し側が HarnessUndecided に理由として入れる。比べる対は AC2・AC3・AC5 が名指しした対だけを渡す。
    internal sealed class Evidence
    {
        public Dictionary<string, BuildInventory> Builds { get; } = new Dictionary<string, BuildInventory>(StringComparer.Ordinal);
        public List<string> HarnessUndecided { get; } = new List<string>();
        public List<(string a, string b, bool crossProcess)> SameSourcePairs { get; } = new List<(string, string, bool)>();
        public List<(string a, string b)> RootSetPairs { get; } = new List<(string, string)>();
        public List<Round> Rounds { get; } = new List<Round>();
        // 種類の不足と不在を確かめる S の名前と、組み合わせが違うことを確かめる 2 build。
        public string FixtureOnly { get; set; } = "S";
        public (string a, string b) DistinctCombination { get; set; } = ("A", "B");
    }

    [Serializable] internal sealed class Verdict
    {
        public string Conclusion = "";
        public string[] Reasons = Array.Empty<string>();
        public string[] Notes = Array.Empty<string>();
    }

    internal static class Judgement
    {
        // HANDOFF §2 の判定定義を上から順に当てはめ、最初に当たったものを結論とする。
        public static Verdict Judge(Evidence e, KindClassifier classifier)
        {
            var undecided = new List<string>(e.HarnessUndecided);
            var notes = new List<string>();
            var pairs = new List<(string a, string b)>();
            pairs.AddRange(e.SameSourcePairs.Select(x => (x.a, x.b)));
            pairs.AddRange(e.RootSetPairs);
            pairs.AddRange(e.Rounds.Select(x => (x.Baseline, x.Build)));
            foreach (var name in pairs.SelectMany(x => new[] { x.a, x.b }).Distinct().Where(x => !e.Builds.ContainsKey(x)))
                undecided.Add("build " + name + ": no inventory");
            foreach (var b in e.Builds.Values)
            {
                undecided.AddRange(b.Unreadable.Select(x => "build " + b.Build + " unreadable (Phase A): " + x));
                undecided.AddRange(b.Unknown.Select(x => "build " + b.Build + " unknown file (Phase A): " + x));
                if (b.RootColocated) undecided.Add("build " + b.Build + " root file has other source assets (Phase A): " + string.Join(", ", b.RootSources));
            }
            if (undecided.Count > 0) return Result(Conclusion.Undecided, undecided, notes);

            var rootSet = e.RootSetPairs.Select(x => FileInventory.Compare(e.Builds[x.a], e.Builds[x.b])).ToArray();
            foreach (var pair in rootSet.Where(x => x.A == e.FixtureOnly || x.B == e.FixtureOnly))
            {
                var s = e.Builds[e.FixtureOnly];
                var other = e.Builds[pair.A == e.FixtureOnly ? pair.B : pair.A];
                undecided.AddRange(FileInventory.Absent(s, other).Select(x => pair.A + "-" + pair.B + " absent (fixture): " + x));
                foreach (var kind in KindClassifier.Core.Append(KindClassifier.Shader).Where(k => !pair.ComparedKinds.Contains(k)))
                    undecided.Add(pair.A + "-" + pair.B + " compared no " + kind + " file");
            }
            var combo = rootSet.FirstOrDefault(x => x.A == e.DistinctCombination.a && x.B == e.DistinctCombination.b);
            if (combo == null || !combo.OneSided.Any(x => !x.Excluded && x.Side == combo.A) || !combo.OneSided.Any(x => !x.Excluded && x.Side == combo.B))
                undecided.Add("combination " + e.DistinctCombination.a + "-" + e.DistinctCombination.b + " is not shown to differ by ids");
            var rounds = e.Rounds.Select(r => (r, FileInventory.Compare(e.Builds[r.Baseline], e.Builds[r.Build], r.ChangedSources))).ToArray();
            foreach (var (r, c) in rounds.Where(x => !x.r.Restore && x.Item2.ExpectedChanges.Length == 0))
                undecided.Add("round " + r.Build + ": the expected change did not occur");
            if (undecided.Count > 0) return Result(Conclusion.Undecided, undecided, notes);

            var noGo = new List<string>();
            var n1Cross = new List<bool>();
            foreach (var (a, b, cross) in e.SameSourcePairs)
            {
                var c = FileInventory.Compare(e.Builds[a], e.Builds[b]);
                if (c.Differed.Length == 0 && c.Moved.Length == 0 && !c.HasOneSidedOutsideExclusions) continue;
                noGo.Add("N1 " + a + "-" + b + ": " + Describe(c));
                n1Cross.Add(cross);
            }
            foreach (var c in rootSet)
            {
                if (c.FindingKinds.Any(k => KindClassifier.Core.Contains(k))) noGo.Add("N2 " + c.A + "-" + c.B + ": " + Describe(c));
                if (c.FindingKinds.Any(k => k == KindClassifier.Scene || k == KindClassifier.Other)) noGo.Add("N4 " + c.A + "-" + c.B + ": " + Describe(c));
            }
            foreach (var (r, c) in rounds)
                if (c.Differed.Length != 0 || c.Moved.Length != 0 || c.HasOneSidedOutsideExclusions || (r.Restore && c.ExpectedChanges.Length != 0))
                    noGo.Add("N3 " + r.Baseline + "-" + r.Build + ": " + Describe(c));
            if (noGo.Count > 0)
            {
                if (n1Cross.Count > 0 && n1Cross.All(x => x))
                    notes.Add("N1 occurred only in cross-process pairs; reimport and rebuild are not separated.");
                return Result(Conclusion.NoGo, noGo, notes);
            }

            var findings = rootSet.Where(c => c.Differed.Length != 0 || c.Moved.Length != 0).ToArray();
            if (findings.Length == 0) return Result(Conclusion.Go, new List<string> { "AC3 pairs have no differed file and no moved source asset." }, notes);
            var shader = findings.Any(c => c.Differed.Any(x => x.SourceAssets.Contains(classifier.SharedShader))
                                           || c.Moved.Any(x => x.SourceAsset == classifier.SharedShader));
            notes.Add("SharedShader differed or moved: " + (shader ? "yes" : "no"));
            return Result(Conclusion.ConditionalAccept,
                findings.Select(c => c.A + "-" + c.B + ": " + Describe(c)).ToList(), notes);
        }

        private static string Describe(PairResult c) =>
            "differed=" + string.Join(",", c.Differed.Select(x => x.KindLabel + ":" + x.Id))
            + " moved=" + string.Join(",", c.Moved.Select(x => x.Kind + ":" + x.SourceAsset))
            + " oneSided=" + string.Join(",", c.OneSided.Where(x => !x.Excluded).Select(x => x.Kind + ":" + x.Id))
            + " expected=" + c.ExpectedChanges.Length;

        private static Verdict Result(Conclusion conclusion, List<string> reasons, List<string> notes) =>
            new Verdict { Conclusion = conclusion.ToString(), Reasons = reasons.ToArray(), Notes = notes.ToArray() };
    }
}
