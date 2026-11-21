#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Editor.Build.Content
{
    public static class ContentTransportPublisher
    {
        public static string Publish(BuildContentResult result, string publishRoot)
        {
            if (result == null || !result.IsSuccess)
                throw new InvalidOperationException("Only a successful content build can be published.");

            var preflight = JsonUtility.FromJson<Preflight>(File.ReadAllText(result.PreflightPath))
                ?? throw new InvalidOperationException("Preflight report is invalid.");
            if (preflight.identity != result.Identity || preflight.target != BuildContentCoordinator.TargetName)
                throw new InvalidOperationException("Build result and preflight identity do not match.");
            // Full の build だけを publish する。Incremental の workspace には前回の選択の file が残りうるので、
            // 配布物に混ぜない。mode は result の欄ではなく outcome の file を正とし、欄の無い旧 outcome も拒む。
            // publish 先と staging を作る前に止める。
            if (!File.Exists(result.OutcomePath))
                throw new InvalidOperationException("Outcome report is missing; only a Full build can be published.");
            var outcome = JsonUtility.FromJson<Outcome>(File.ReadAllText(result.OutcomePath))
                ?? throw new InvalidOperationException("Outcome report is invalid.");
            if (outcome.identity != result.Identity)
                throw new InvalidOperationException("Build result and outcome identity do not match.");
            if (outcome.buildMode != nameof(BuildContentMode.Full))
                throw new InvalidOperationException("Only a Full build can be published (buildMode='" + outcome.buildMode + "').");
            // 上書きと、selected を絞っただけの build は、宣言の選択の一部しか持たないので配らない。上書きは outcome の印で、
            // 印の無い絞り（public な BuildPlan.WithSelectedSubset から記録なしで作った plan）は preflight の除外の理由 code で拒む。
            // excluded の行は `stableKey|reason|dimension|value` だが key に `|` が入りうるので、どの区切りに理由 code があっても拒む
            // （誤って一致するのは拒む側だけ）。
            if (outcome.overlay)
                throw new InvalidOperationException("An overlay build cannot be published.");
            if (preflight.excluded.Any(x => x.Split('|').Contains(nameof(OneStarMaker.Build.Selection.BuildExclusionReasonCode.NotInSubset))))
                throw new InvalidOperationException("A build of a selected subset cannot be published.");

            var final = Path.GetFullPath(publishRoot);
            var sourceRoot = Path.GetFullPath(result.ContentPath!);
            if (final.Equals(sourceRoot, StringComparison.OrdinalIgnoreCase)
                || final.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Publish destination must be outside the build content source.");
            if (Directory.Exists(final))
                throw new IOException("Publish destination already exists.");

            // この呼出し専用の staging だけを所有する。既存の公開先を更新せず、
            // manifest と全 bytes が揃ってから同一 volume の rename で公開する。
            var staging = StagingPathFor(final);
            try
            {
                var contentRoot = Path.Combine(staging, "content");
                Directory.CreateDirectory(contentRoot);
                // Unity 内部 manifest も opaque な file として運ぶ。内部形式を配信 protocol にしない。
                foreach (var source in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(sourceRoot, source);
                    var target = Path.Combine(contentRoot, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(source, target);
                }

                var files = Directory.GetFiles(contentRoot, "*", SearchOption.AllDirectories)
                    .Select(path => new ContentTransportFile
                    {
                        path = Path.GetRelativePath(contentRoot, path).Replace('\\', '/'),
                        size = new FileInfo(path).Length,
                        sha256 = Sha256(path),
                    })
                    .OrderBy(x => x.path, StringComparer.Ordinal)
                    .ToArray();

                // source 閉包は開発時の案内と、手元の編集から上書きを決める計画の比較の相手。実行時の content 取得や登録には要求しない。
                // hash は build の前に coordinator が取った値（preflight）を書き、publish の時点の disk を読まない（build の後に source を
                // 変えてから publish しても、記録が build した中身と食い違わないように）。hash の記録の無い旧 preflight だけ、今の時点で取る。
                // どちらも、その時点で在る file だけを書く。
                var sources = preflight.sourceHashRecord == 1
                    ? preflight.sourceHashes.Where(x => x.present)
                        .Select(x => new ContentSourceFile { path = x.path.Replace('\\', '/'), sha256 = x.sha256 })
                        .GroupBy(x => x.path, StringComparer.Ordinal).Select(x => x.First())
                        .OrderBy(x => x.path, StringComparer.Ordinal).ToArray()
                    : preflight.closure
                        .Select(x => x.Contains('|') ? x.Substring(x.LastIndexOf('|') + 1) : x)
                        .SelectMany(x => File.Exists(x + ".meta") ? new[] { x, x + ".meta" } : new[] { x })
                        .Where(File.Exists).Distinct(StringComparer.Ordinal)
                        .Select(x => new ContentSourceFile { path = x.Replace('\\', '/'), sha256 = Sha256(x) })
                        .OrderBy(x => x.path, StringComparer.Ordinal).ToArray();
                // 宣言は build result から写す（preflight の形は変えない）。version と rootSchemaVersion は据え置き:
                // 任意の追加項目で、宣言の無い旧 manifest は name が空のまま読まれる。
                var declaration = result.Declaration == null ? new ContentManifestDeclaration() : new ContentManifestDeclaration
                {
                    name = result.Declaration.Name,
                    representation = result.Declaration.Representation.Primary,
                    runtimeDefaults = result.Declaration.RuntimeDefaults
                        .Select(pair => new ContentManifestDeclarationValue { key = pair.Key, value = pair.Value }).ToArray(),
                };
                // 索引は coordinator が outcome に記録した entry から作る（preflight の requirements や plan からは作らない。
                // それらは root と別の集合になりうる）。記録の無い outcome（旧 outcome、coordinator を通さない result）は拒まずに
                // 版 0（索引なし）で publish する。表現は生の値のまま書き、並べ順は logical key、kind の名前、表現の ordinal。
                var index = outcome.indexRecord switch
                {
                    0 => new ContentTransportIndex(),
                    1 => new ContentTransportIndex
                    {
                        version = 1,
                        entries = outcome.indexEntries.Select(BuildContentResult.Copy)
                            .OrderBy(x => x.logicalKey, StringComparer.Ordinal)
                            .ThenBy(x => x.kind, StringComparer.Ordinal)
                            .ThenBy(x => x.representation, StringComparer.Ordinal)
                            .ToArray(),
                    },
                    _ => throw new InvalidOperationException("Outcome index record is unknown: " + outcome.indexRecord),
                };
                var manifest = new ContentTransportManifest
                {
                    version = 1,
                    product = "OneStarMaker",
                    contentSet = preflight.contentSet,
                    revision = result.Identity,
                    target = preflight.target,
                    unityVersion = Application.unityVersion,
                    rootSchemaVersion = 2,
                    playerConfigSchemaVersion = 1,
                    files = files,
                    sourceFiles = sources,
                    declaration = declaration,
                    index = index,
                    sourceClosure = SourceClosure(preflight, index),
                };
                var manifestPath = Path.Combine(staging, "transport.json");
                var written = Write(manifestPath, manifest);
                // 書いた bytes を選択の読み口で読み直し、重複・空白だけの値などの形の失敗があれば公開しない（staging は finally が消す）。
                // 値の規則を publisher に 2 つ目として書かず、読み手が受け付けない索引を配布しないための検査。
                var reread = ContentPurposeIndex.Read(written, Sha256(written));
                if (reread.Failure == ContentPurposeIndexFailure.TooLarge && manifest.sourceClosure.version != 0)
                {
                    // 閉包の記録で上限を超えた時は、記録なし（版 0）で publish する。その base の部分 checkout の計画は、欠けた selected の root を
                    // 全て作り直せない root にする（全ての source がある checkout は今までどおり動く）。版 0 でも超えれば下で今までどおり失敗する。
                    Debug.LogWarning("[ContentTransportPublisher] The source closure record exceeds the manifest limit; published without it (sourceClosure version 0): " + final);
                    manifest.sourceClosure = new ContentSourceClosure();
                    written = Write(manifestPath, manifest);
                    reread = ContentPurposeIndex.Read(written, Sha256(written));
                }
                var expected = index.version == 0 ? ContentPurposeIndexState.NoIndex : ContentPurposeIndexState.Indexed;
                if (reread.State != expected)
                    throw new InvalidOperationException("Published index is invalid: " + reread.Detail);
                // 閉包の記録も計画の読み口で読み直し、読み手が「壊れている」と読む記録を配布しない。
                var closureRead = ContentSourceClosureRecord.Read(JsonUtility.FromJson<ContentTransportManifest>(Encoding.UTF8.GetString(written)));
                var closureExpected = manifest.sourceClosure.version == 0 ? ContentSourceClosureState.Missing : ContentSourceClosureState.Present;
                if (closureRead.State != closureExpected)
                    throw new InvalidOperationException("Published source closure record is invalid: " + closureRead.Detail);
                Directory.Move(staging, final);
                return final;
            }
            finally
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, true);
            }
        }

        private static byte[] Write(string path, ContentTransportManifest manifest)
        {
            File.WriteAllText(path, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
            return File.ReadAllBytes(path);
        }

        // 閉包の記録は、build の前の hash が閉包の全ての file と .meta で取れ、索引が版 1 の時だけ書く（root の集合は索引の entry と等しい）。
        // hash の記録の無い旧 preflight、hash の時点で無い file がある build（合成の test の build）、索引が版 0 の publish は版 0（記録なし）。
        private static ContentSourceClosure SourceClosure(Preflight preflight, ContentTransportIndex index)
        {
            if (preflight.sourceHashRecord != 1 || index.version != 1 || preflight.sourceHashes.Length == 0 || preflight.sourceHashes.Any(x => !x.present))
                return new ContentSourceClosure();
            return ContentSourceClosureRecord.Create(preflight.rootClosures.Select(x => new ContentSourceClosureRootRecord(
                x.logicalKey, x.kind, x.representation, x.guid,
                x.closure.Select(e => new OneStarMaker.Editor.Build.Materialization.BuildDependencyEntry(
                    e.Substring(0, e.IndexOf('|')), e.Substring(e.IndexOf('|') + 1))))));
        }

        // staging は公開先の親の下の `.staging/<guid32>` に置く。名前（41 文字）が build identity（52 文字）より短いので、
        // staging の中の各 path は final の同じ file より長くならない（final の名前に接尾辞を足すと path の予算を超えた）。
        // 同じ親の下なので final への move は同じ volume。GUID は切り詰めない。
        // 空になった `.staging` 自体は他の publish と共有しうるので消さない。
        internal static string StagingPathFor(string final) =>
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(final))!, ".staging", Guid.NewGuid().ToString("N"));

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = System.Security.Cryptography.SHA256.Create();
            return string.Concat(hash.ComputeHash(stream).Select(x => x.ToString("x2")));
        }

        private static string Sha256(byte[] bytes)
        {
            using var hash = System.Security.Cryptography.SHA256.Create();
            return string.Concat(hash.ComputeHash(bytes).Select(x => x.ToString("x2")));
        }

        [Serializable]
        private sealed class Outcome
        {
            public string identity = "";
            public string buildMode = "";
            public bool overlay;
            public int indexRecord;
            public ContentTransportIndexEntry[] indexEntries = Array.Empty<ContentTransportIndexEntry>();
        }

        [Serializable]
        private sealed class Preflight
        {
            public string identity = "";
            public string target = "";
            public string contentSet = "";
            public string[] excluded = Array.Empty<string>();
            public string[] closure = Array.Empty<string>();
            public ContentReportRootClosure[] rootClosures = Array.Empty<ContentReportRootClosure>();
            public int sourceHashRecord;
            public ContentReportSourceHash[] sourceHashes = Array.Empty<ContentReportSourceHash>();
        }
    }
}
