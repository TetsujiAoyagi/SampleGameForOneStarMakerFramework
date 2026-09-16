#nullable enable

using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace OneStarMaker.Editor.Build.Content
{
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

    public sealed class BuildContentCoordinator
    {
        internal const string TargetName = "StandaloneWindows64-Player";
        private readonly IContentDirectoryAdapter _adapter;
        public BuildContentCoordinator() : this(new UnityContentDirectoryAdapter()) { }
        internal BuildContentCoordinator(IContentDirectoryAdapter adapter) => _adapter = adapter;

        public BuildContentResult Build(BuildContentRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
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
                if (!Directory.Exists(workspace) || !File.Exists(built.ManifestPointer) ||
                    !Directory.Exists(built.MetadataPath) || !Contained(workspace, built.ManifestPointer) ||
                    !Contained(workspace, built.MetadataPath))
                    throw new InvalidOperationException("Unity build report or manifest metadata is missing.");
                if (Directory.Exists(staging) || Directory.Exists(final))
                    throw new IOException("Build identity output already exists.");
                CopyDirectory(workspace, staging);
                var manifest = Path.Combine(staging, Path.GetRelativePath(workspace, built.ManifestPointer));
                var metadata = Path.Combine(staging, Path.GetRelativePath(workspace, built.MetadataPath));
                if (!File.Exists(manifest) || !Directory.Exists(metadata))
                    throw new IOException("Staging copy missed Unity metadata.");
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
            Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        private static bool SamePath(string a, string b) =>
            string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }
}
