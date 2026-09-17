#nullable enable

using System;
using System.IO;
using System.Linq;
using OneStarMaker.Build.Selection;
using UnityEngine;

namespace OneStarMaker.Editor.Build.Content
{
    // BS1 の成功 plan と、同じ候補集合に対する BS2a materialization snapshot を対で渡す。
    // ContentSet は固定 workspace の識別子。ArtifactsRoot は coordinator が許可範囲を検証する。
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

    // 1回の build の結果。preflight/outcome は失敗時にも診断用に残す。
    // ContentPath がある場合だけ成功 directory として後続へ渡せる。
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

    // JsonUtility 用の保存形。Unity の内部 BuildReport 型を外部 protocol にしない。
    // preflight と outcome は同じ形を使うが、前者は入力判断、後者は build 結果を記録する。
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
        // Unity build 前に確定した選択、除外理由、root、閉包、構造化 issue を保存する。
        // 失敗してもこのファイルを読めば、どの候補を build しようとしたか追跡できる。
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
            // 成功 path / manifest / metadata と summary、または失敗 issue を build identity で結ぶ。
            // 選択と閉包の詳細は preflight が正本であり、ここに重複させない。
            Write(path, new ContentReportFile {
                identity = result.Identity, target = BuildContentCoordinator.TargetName,
                summary = result.Summary, contentPath = result.ContentPath ?? "",
                manifestPointer = result.ManifestPointer ?? "", metadataPath = result.MetadataPath ?? "",
                issues = result.Issues.Select(x => x.Code + "|" + x.Key + "|" + x.Detail).ToArray()
            });
        }

        private static void Write(string path, ContentReportFile data)
        {
            // report は content directory と別に保持するため、失敗した build でも調査できる。
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }
    }
}
