#nullable enable

using System;
using System.IO;
using System.Linq;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Editor.Build.Content
{
    // Unity 固有の build 操作を coordinator から隔離する境界。
    // fake 実装を使うと publish 失敗経路を実 Content Directory build なしで確認できる。
    internal sealed class ContentDirectoryBuild
    {
        public ContentDirectoryBuild(string summary, string manifestPointer, string metadataPath)
        { Summary = summary; ManifestPointer = manifestPointer; MetadataPath = metadataPath; }
        public string Summary { get; }
        public string ManifestPointer { get; }
        public string MetadataPath { get; }
    }

    internal interface IContentDirectoryAdapter
    {
        void ValidateTarget();
        // declaration は root に写す宣言。無ければ root は宣言無しで作る（合成 fixture・旧経路）。
        // overlayBase は上書きの build の時だけ渡す base の記録で、root に上書きの印として写す（contentSet は上書き自身の値）。
        // 上書きでない build では null で、root は記録無しで作る。
        ContentDirectoryBuild Build(BuildContentProjectionResult projection, string identity, string workspace,
            OneStarMaker.Build.Selection.BuildPurposeDeclaration? declaration, ContentOverlayBaseIdentity? overlayBase, string contentSet);
    }

    // 1回の build の所有者。入力検証、preflight、Unity build、公開、outcome の順序を固定する。
    // Runtime の登録や選択 policy には立ち入らず、成功した directory を次段へ渡す。
    public sealed class BuildContentCoordinator
    {
        internal const string TargetName = "StandaloneWindows64-Player";
        private readonly IContentDirectoryAdapter _adapter;
        public BuildContentCoordinator() : this(new UnityContentDirectoryAdapter()) { }
        internal BuildContentCoordinator(IContentDirectoryAdapter adapter) => _adapter = adapter;

        public BuildContentResult Build(BuildContentRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            // 呼出側に任意の出力先を許さない。contentSet は path segment としても使うため先に検証する。
            // work は同じ target/contentSet で再利用し、content/{identity} は成功ごとに別名で残す。
            ValidateSegment(request.ContentSet);
            if (request.Overlay != null) ValidateOverlay(request);
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var approvedRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "artifacts", "bs2b"));
            var testRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "TestResults", "bs3-fixture"));
            var root = Path.GetFullPath(request.ArtifactsRoot);
            // 実 directory の検証は build を伴うが、既存の検証ログを増減させない。
            // テスト専用 root だけを追加で許し、任意の workspace path への出力は引き続き拒む。
            var boundary = SamePath(approvedRoot, root) || Contained(approvedRoot, root) ? approvedRoot
                : SamePath(testRoot, root) || Contained(testRoot, root) ? testRoot
                : throw new ArgumentException("Output root must be inside artifacts/bs2b or TestResults/bs3-fixture.");
            var identity = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N");
            var reportDirectory = Child(root, "reports", identity);
            var preflight = Path.Combine(reportDirectory, "preflight.json");
            var outcome = Path.Combine(reportDirectory, "outcome.json");
            var workspace = Child(root, "work", TargetName, request.ContentSet);
            var staging = Child(root, "staging", identity);
            var final = Child(root, "content", identity);
            // Unity I/O の前に plan と snapshot を照合し、失敗でも preflight を残す。
            // ここで止めた場合は成功 directory を作らず、構造化 issue を outcome に記録する。
            var projection = BuildContentProjection.Create(request.Plan, request.Snapshot);
            // 閉包の file と .meta を build の前に hash する。publish の sourceFiles と閉包の記録はこの値を書き、Unity の build の後に
            // 取り直した値と違う build は成功にしない（hash の後から Unity が読むまでに変わった source が記録と食い違うので）。
            // hash の時点で無い file は「無い」と記録し、前後とも「無い」なら等しい（上書きでない build は今までどおり失敗にしない）。
            var sourceHashes = ContentSourceHashes.Capture(projectRoot, projection.Files.Select(x => x.Path));
            BuildContentReport.WritePreflight(preflight, identity, TargetName, request.ContentSet, request.Plan, projection,
                request.Requirements, request.Overlay, sourceHashes);
            var isOverlay = request.Overlay != null;
            var overlayPlan = request.Overlay?.Plan;
            if (!projection.IsValid)
            {
                var failure = new BuildContentResult(identity, preflight, outcome, null, null, null,
                    "Preflight failed", projection.Issues.ToArray(), request.Declaration, request.Mode);
                BuildContentReport.WriteOutcome(outcome, failure, isOverlay);
                return failure;
            }
            var published = false;
            BuildContentResult success;
            try
            {
                _adapter.ValidateTarget();
                // Full は workspace の中身を消してから Unity を呼ぶ。消せなければ（リンク、他 process の lock）
                // Unity の build を始めずに下の失敗として記録する。workspace 自体は残す。
                // 作成より先に検査し、途中の segment がリンクなら CreateDirectory でリンク先に書かない。
                if (overlayPlan != null)
                {
                    // 計画から作った上書きは、手元に無い source を Unity の build に渡さない（計画が欠けた root を外した後に消えた file も含む）。
                    var missing = projection.Files.Select(x => x.Path).Where(x => sourceHashes[x] == null).ToArray();
                    if (missing.Length != 0)
                        throw new BuildIssueException(new BuildContentIssue(BuildContentIssueCode.SourceFileMissing, missing[0],
                            missing.Length + " closure file(s) of the overlay are missing locally: " + string.Join(", ", missing)));
                }
                if (request.Mode == BuildContentMode.Full) ClearWorkspace(boundary, workspace);
                Directory.CreateDirectory(workspace);
                var built = _adapter.Build(projection, identity, workspace, request.Declaration, request.Overlay?.BaseIdentity, request.ContentSet);
                var after = ContentSourceHashes.Capture(projectRoot, projection.Files.Select(x => x.Path));
                var changed = ContentSourceHashes.Differences(sourceHashes, after);
                if (changed.Count == 0 && overlayPlan != null) changed = ContentSourceHashes.Differences(overlayPlan.PlanHashes, after);
                if (changed.Count != 0)
                    throw new BuildIssueException(new BuildContentIssue(BuildContentIssueCode.SourceChangedDuringBuild, changed[0],
                        changed.Count + " source file(s) changed during the build: " + string.Join(", ", changed)));
                // Unity report が成功でも、manifest と metadata が所定 workspace に無ければ公開しない。
                // adapter から返る path の包含も調べ、別の directory を誤って配布しない。
                if (!Directory.Exists(workspace) || !File.Exists(built.ManifestPointer) ||
                    !Directory.Exists(built.MetadataPath) || !Contained(workspace, built.ManifestPointer) ||
                    (!SamePath(workspace, built.MetadataPath) && !Contained(workspace, built.MetadataPath)))
                    throw new InvalidOperationException("Unity build report or manifest metadata is missing.");
                if (Directory.Exists(staging) || Directory.Exists(final))
                    throw new IOException("Build identity output already exists.");
                // Unity の出力一式を staging に複写し、検証後に同じ volume 内で final へ移す。
                // 既存の成功 directory は上書きしない。manifest と metadata は移設後の path に読み替える。
                CopyDirectory(workspace, staging);
                var manifest = Path.Combine(staging, Path.GetRelativePath(workspace, built.ManifestPointer));
                var metadata = Path.Combine(staging, Path.GetRelativePath(workspace, built.MetadataPath));
                if (!File.Exists(manifest) || !Directory.Exists(metadata))
                    throw new IOException("Staging copy missed Unity metadata.");
                Directory.CreateDirectory(Path.GetDirectoryName(final)!);
                Directory.Move(staging, final);
                published = true;
                // publish の索引は、adapter が root に entry を書いたのと同じ関数で projection の root から作る。
                // 記録するのは成功した build だけで、失敗の result は記録なし（null）のまま。
                var indexEntries = projection.Roots.Select(BuildContentRootKeys.Describe)
                    .Select(x => new ContentTransportIndexEntry
                        { logicalKey = x.LogicalKey, kind = x.Kind.ToString(), representation = x.Representation })
                    .ToArray();
                success = new BuildContentResult(identity, preflight, outcome, final,
                    Path.Combine(final, Path.GetRelativePath(workspace, built.ManifestPointer)),
                    Path.Combine(final, Path.GetRelativePath(workspace, built.MetadataPath)), built.Summary,
                    Array.Empty<BuildContentIssue>(), request.Declaration, request.Mode, indexEntries, overlayPlan?.Key);
                BuildContentReport.WriteOutcome(outcome, success, isOverlay);
                if (overlayPlan == null) return success;
            }
            catch (BuildIssueException ex)
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                if (published && Directory.Exists(final)) Directory.Delete(final, true);
                var failure = new BuildContentResult(identity, preflight, outcome, null, null, null,
                    ex.Issue.Detail, new[] { ex.Issue }, request.Declaration, request.Mode);
                BuildContentReport.WriteOutcome(outcome, failure, isOverlay);
                return failure;
            }
            catch (Exception ex)
            {
                // 失敗 workspace は診断・再試行用に保持し、公開途中の staging/final だけを片付ける。
                // result の ContentPath は null として、途中成果物を成功として渡さない。
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                if (published && Directory.Exists(final)) Directory.Delete(final, true);
                var failure = new BuildContentResult(identity, preflight, outcome, null, null, null,
                    ex.ToString(), new[] { new BuildContentIssue(BuildContentIssueCode.BuildFailure, identity, ex.Message) }, request.Declaration, request.Mode);
                BuildContentReport.WriteOutcome(outcome, failure, isOverlay);
                return failure;
            }

            // 成功した計画の上書きだけを再利用の記録に残す（失敗した build と、計画を通さない上書きは記録を変えない）。
            // 成功の outcome を書いた後の別の試みなので、ここの失敗は build を失敗にしない。上の catch に入れると final を消し、
            // outcome を失敗で上書きして、成功した上書きを捨てる。理由を結果に写し、呼び手が示す。
            // 次の同じ鍵の計画は、記録が無いか古い鍵を指すので build し直す。
            try
            {
                ContentOverlayReuse.Write(root, request.ContentSet, overlayPlan!.Key!, success);
                return success;
            }
            catch (Exception ex)
            {
                return success.WithReuseRecordFailure("The overlay reuse record could not be written: " + ex.Message);
            }
        }

        // 構造化した issue で build を止める（文言でなく code で分岐できる失敗）。Unity の例外の BuildFailure と分ける。
        private sealed class BuildIssueException : Exception
        {
            public BuildIssueException(BuildContentIssue issue) : base(issue.Detail) => Issue = issue;
            public BuildContentIssue Issue { get; }
        }

        // 上書きの build を、report を書く前に拒む条件。
        // (1) base と同じ contentSet は同じ workspace を使い、Full の build が base の workspace を空にする。Windows では
        //     大文字小文字だけ違う名前も同じ folder になる（ValidateSegment は両方を通す）ので、大小を無視して比べる。
        // (2) 宣言の名前と target が base の記録と違う上書きは、登録契約 C6 で重ねられない物になるので作らない。
        //     宣言付きの base に宣言なしの上書き（その逆も）も名前の不一致として拒む。
        // (3) base の digest が transport manifest の SHA-256 の形（小文字 16 進 64 字）でない上書きは、root に写しても
        //     登録の照合（install した base の receipt の値と ordinal 比較）で必ず落ちるので作らない。
        private static void ValidateOverlay(BuildContentRequest request)
        {
            var b = request.Overlay!.BaseIdentity;
            if (string.Equals(b.ContentSet, request.ContentSet, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("An overlay must not use the base content set '" + b.ContentSet + "' (case-insensitive).");
            if (!string.Equals(b.DeclarationName, request.Declaration?.Name, StringComparison.Ordinal))
                throw new ArgumentException("The overlay declaration '" + (request.Declaration?.Name ?? "(none)") +
                                            "' does not match the base declaration '" + (b.DeclarationName ?? "(none)") + "'.");
            if (!string.Equals(b.Target, TargetName, StringComparison.Ordinal))
                throw new ArgumentException("The base target '" + b.Target + "' does not match the build target '" + TargetName + "'.");
            if (!OneStarMaker.Runtime.BuildContent.BuildContentOverlayBase.IsManifestSha256(b.ManifestDigest))
                throw new ArgumentException("The base manifest digest must be the SHA-256 of the transport manifest bytes (64 lowercase hexadecimal characters).");
        }

        // workspace の中身だけを消す。字句上の包含（Child）だけでは、途中の segment や中の directory が
        // junction・symbolic link の場合にリンク先（root の外でありうる）を消してしまう。そこで root から workspace までの
        // 各 segment と workspace の中の全 directory を先に調べ、reparse point が 1 つでもあれば何も消さずに失敗する。
        // リンクの中は辿らない。汎用の filesystem 防御ではなく、この削除経路のための検査に限る。
        private static void ClearWorkspace(string boundary, string workspace)
        {
            if (!SamePath(boundary, workspace) && !Contained(boundary, workspace))
                throw new IOException("Workspace escapes the approved root.");
            var segment = Path.GetFullPath(boundary).TrimEnd(Path.DirectorySeparatorChar);
            RejectReparsePoint(segment);
            foreach (var part in Path.GetRelativePath(boundary, workspace).Split(Path.DirectorySeparatorChar))
            {
                segment = Path.Combine(segment, part);
                RejectReparsePoint(segment);
            }
            if (!Directory.Exists(workspace)) return;
            var pending = new System.Collections.Generic.Stack<string>();
            pending.Push(workspace);
            while (pending.Count > 0)
                foreach (var child in Directory.GetDirectories(pending.Pop()))
                {
                    RejectReparsePoint(child);
                    pending.Push(child);
                }
            // 検査を全て通ってから消す。ここでの lock による失敗は途中まで消えた workspace を残すが、
            // workspace は再利用の cache であり、次の Full が改めて空にする。
            foreach (var file in Directory.GetFiles(workspace)) File.Delete(file);
            foreach (var directory in Directory.GetDirectories(workspace)) Directory.Delete(directory, true);
        }

        private static void RejectReparsePoint(string path)
        {
            if (!Directory.Exists(path) && !File.Exists(path)) return;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Workspace path contains a link and is not cleared: " + path);
        }

        private static void ValidateSegment(string segment)
        {
            // file system の予約文字や相対移動を受け付けないため、安定した ASCII 識別子に限定する。
            if (string.IsNullOrEmpty(segment) || segment.Length > 64 ||
                segment.Any(c => !(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                                 !(c >= '0' && c <= '9') && c != '-' && c != '_'))
                throw new ArgumentException("Content set must be a safe ASCII path segment.");
        }
        private static string Child(string root, params string[] parts)
        {
            var result = Path.GetFullPath(Path.Combine(new[] { root }.Concat(parts).ToArray()));
            if (!Contained(root, result)) throw new IOException("Output escapes artifacts root.");
            return result;
        }
        private static bool Contained(string root, string path) =>
            // 区切り文字を含めて比較し、"artifacts/bs2b-other" を子として誤認しない。
            Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        private static bool SamePath(string a, string b) =>
            string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

        private static void CopyDirectory(string source, string target)
        {
            // Unity が作った内部 file の形式や一覧を解釈せず、directory 全体を成果物として複写する。
            Directory.CreateDirectory(target);
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }
}
