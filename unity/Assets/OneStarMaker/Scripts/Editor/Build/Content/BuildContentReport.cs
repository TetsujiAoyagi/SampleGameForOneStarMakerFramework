#nullable enable

using System;
using System.IO;
using System.Linq;
using OneStarMaker.Build.Selection;
using UnityEngine;

namespace OneStarMaker.Editor.Build.Content
{
    public sealed class BuildContentRequest
    {
        public BuildContentRequest(BuildPlan plan, OneStarMaker.Editor.Build.Materialization.BuildMaterializationSnapshot snapshot,
            string contentSet, string artifactsRoot)
        { Plan = plan; Snapshot = snapshot; ContentSet = contentSet; ArtifactsRoot = artifactsRoot; }
        public BuildPlan Plan { get; }
        public OneStarMaker.Editor.Build.Materialization.BuildMaterializationSnapshot Snapshot { get; }
        public string ContentSet { get; }
        public string ArtifactsRoot { get; }
    }

    public sealed class BuildContentResult
    {
        public BuildContentResult(string identity, string preflightPath, string outcomePath, string? contentPath,
            string? manifestPointer, string? metadataPath, string summary, BuildContentIssue[] issues)
        { Identity = identity; PreflightPath = preflightPath; OutcomePath = outcomePath; ContentPath = contentPath;
          ManifestPointer = manifestPointer; MetadataPath = metadataPath; Summary = summary; Issues = issues; }
        public string Identity { get; }
        public string PreflightPath { get; }
        public string OutcomePath { get; }
        public string? ContentPath { get; }
        public string? ManifestPointer { get; }
        public string? MetadataPath { get; }
        public string Summary { get; }
        public BuildContentIssue[] Issues { get; }
        public bool IsSuccess => ContentPath != null && Issues.Length == 0;
    }

    [Serializable] internal sealed class ContentReportFile
    {
        public string identity = "";
        public string target = "";
        public string contentSet = "";
        public string[] selected = Array.Empty<string>();
        public string[] excluded = Array.Empty<string>();
        public string[] roots = Array.Empty<string>();
        public string[] closure = Array.Empty<string>();
        public string[] issues = Array.Empty<string>();
        public string summary = "";
        public string contentPath = "";
        public string manifestPointer = "";
        public string metadataPath = "";
    }

    internal static class BuildContentReport
    {
        public static void WritePreflight(string path, string identity, string target, string contentSet,
            BuildPlan plan, BuildContentProjectionResult projection)
        {
            Write(path, new ContentReportFile {
                identity = identity, target = target, contentSet = contentSet,
                selected = plan.SelectedContent.Select(x => x.StableKey + "|" + x.LogicalKey + "|" + x.PhysicalKey).ToArray(),
                excluded = plan.ExcludedContent.Select(x => x.Candidate.StableKey + "|" + x.ReasonCode + "|" + x.Dimension + "|" + x.Value).ToArray(),
                roots = projection.Roots.Select(x => x.Candidate.StableKey + "|" + x.Path + "|" + x.Representation).ToArray(),
                closure = projection.Files.Select(x => x.Guid + "|" + x.Path).ToArray(),
                issues = projection.Issues.Select(x => x.Code + "|" + x.Key + "|" + x.Detail).ToArray()
            });
        }

        public static void WriteOutcome(string path, BuildContentResult result)
        {
            Write(path, new ContentReportFile {
                identity = result.Identity, target = BuildContentCoordinator.TargetName,
                summary = result.Summary, contentPath = result.ContentPath ?? "",
                manifestPointer = result.ManifestPointer ?? "", metadataPath = result.MetadataPath ?? "",
                issues = result.Issues.Select(x => x.Code + "|" + x.Key + "|" + x.Detail).ToArray()
            });
        }

        private static void Write(string path, ContentReportFile data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }
    }
}
