#nullable enable

using System;
using System.IO;
using System.Linq;
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
        ContentDirectoryBuild Build(BuildContentProjectionResult projection, string identity, string workspace);
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
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var approvedRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "artifacts", "bs2b"));
            var root = Path.GetFullPath(request.ArtifactsRoot);
            if (!SamePath(approvedRoot, root) && !Contained(approvedRoot, root))
                throw new ArgumentException("Output root must be inside artifacts/bs2b.");
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
            BuildContentReport.WritePreflight(preflight, identity, TargetName, request.ContentSet, request.Plan, projection);
            if (!projection.IsValid)
            {
                var failure = new BuildContentResult(identity, preflight, outcome, null, null, null,
                    "Preflight failed", projection.Issues.ToArray());
                BuildContentReport.WriteOutcome(outcome, failure);
                return failure;
            }
            var published = false;
            try
            {
                _adapter.ValidateTarget();
                Directory.CreateDirectory(workspace);
                var built = _adapter.Build(projection, identity, workspace);
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
                var success = new BuildContentResult(identity, preflight, outcome, final,
                    Path.Combine(final, Path.GetRelativePath(workspace, built.ManifestPointer)),
                    Path.Combine(final, Path.GetRelativePath(workspace, built.MetadataPath)), built.Summary,
                    Array.Empty<BuildContentIssue>());
                BuildContentReport.WriteOutcome(outcome, success);
                return success;
            }
            catch (Exception ex)
            {
                // 失敗 workspace は診断・再試行用に保持し、公開途中の staging/final だけを片付ける。
                // result の ContentPath は null として、途中成果物を成功として渡さない。
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                if (published && Directory.Exists(final)) Directory.Delete(final, true);
                var failure = new BuildContentResult(identity, preflight, outcome, null, null, null,
                    ex.ToString(), new[] { new BuildContentIssue(BuildContentIssueCode.BuildFailure, identity, ex.Message) });
                BuildContentReport.WriteOutcome(outcome, failure);
                return failure;
            }
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
