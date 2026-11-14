#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Build.Selection;
using UnityEngine;

namespace OneStarMaker.Editor.Build.Content
{
    /// <summary>
    /// workspace の扱い。<see cref="Incremental"/> は前回の Unity の出力を残したまま build する（日常の build の速さのため）。
    /// <see cref="Full"/> は build の前に workspace を空にし、成果物がその build の出力だけからなる。
    /// publish は <see cref="Full"/> の成果物だけを受け付ける（前回の選択の file を配布しないため）。
    /// </summary>
    public enum BuildContentMode
    {
        Incremental,
        Full,
    }

    // BS1 の成功 plan と、同じ候補集合に対する BS2a materialization snapshot を対で渡す。
    // ContentSet は固定 workspace の識別子。ArtifactsRoot は coordinator が許可範囲を検証する。
    public sealed class BuildContentRequest
    {
        public BuildContentRequest(BuildPlan plan, OneStarMaker.Editor.Build.Materialization.BuildMaterializationSnapshot snapshot,
            string contentSet, string artifactsRoot)
            : this(plan, snapshot, contentSet, artifactsRoot, null, null)
        {
        }

        /// <param name="declaration">
        /// この build を導いた宣言。directory の root と transport manifest に写す。合成だけの build（fixture）では null。
        /// </param>
        /// <param name="requirements">
        /// 到達規則が導いた requirement と到達経路。preflight の `requirements[]` に「なぜ入っているか」として残す。
        /// 選択 plan を変えない記録専用の入力で、null なら preflight に書かない。
        /// </param>
        public BuildContentRequest(BuildPlan plan, OneStarMaker.Editor.Build.Materialization.BuildMaterializationSnapshot snapshot,
            string contentSet, string artifactsRoot, BuildPurposeDeclaration? declaration, IReadOnlyList<BuildPurposeRequirement>? requirements)
            : this(plan, snapshot, contentSet, artifactsRoot, declaration, requirements, BuildContentMode.Incremental)
        {
        }

        /// <param name="mode">workspace の扱い。publish する build は <see cref="BuildContentMode.Full"/> にする。</param>
        public BuildContentRequest(BuildPlan plan, OneStarMaker.Editor.Build.Materialization.BuildMaterializationSnapshot snapshot,
            string contentSet, string artifactsRoot, BuildPurposeDeclaration? declaration, IReadOnlyList<BuildPurposeRequirement>? requirements,
            BuildContentMode mode)
        {
            Plan = plan; Snapshot = snapshot; ContentSet = contentSet; ArtifactsRoot = artifactsRoot;
            Declaration = declaration; Requirements = requirements; Mode = mode;
        }

        /// <summary>
        /// 上書きの build。plan と snapshot は逆引きの結果から取り（別の plan や snapshot を渡す道を持たない）、mode は
        /// <see cref="BuildContentMode.Full"/> に固定する（前の上書きの file を directory に残さない）。
        /// preflight に上書きの詳細、outcome に上書きの印が残り、その成果物は publish できない。
        /// </summary>
        /// <param name="selection">逆引きの結果。上書き不要の結果は受け付けない（呼び手はその時 build しない）。</param>
        /// <param name="baseIdentity">上書きを重ねる base。contentSet は base と別の名前にする（大文字小文字だけ違う名前も不可）。</param>
        /// <param name="declaration">base と同じ宣言。root に写り、宣言の名前は build の前に base の記録と照合する。</param>
        /// <param name="requirements">base と同じ到達経路。preflight の記録専用（既存のコンストラクタと同じ扱い）。</param>
        public BuildContentRequest(ContentOverlaySelectionResult selection, ContentOverlayBaseIdentity baseIdentity,
            string contentSet, string artifactsRoot, BuildPurposeDeclaration? declaration, IReadOnlyList<BuildPurposeRequirement>? requirements)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (baseIdentity == null) throw new ArgumentNullException(nameof(baseIdentity));
            if (!selection.IsOverlayRequired)
                throw new ArgumentException("The selection needs no overlay; do not build one.", nameof(selection));
            Plan = selection.Plan!; Snapshot = selection.Snapshot; ContentSet = contentSet; ArtifactsRoot = artifactsRoot;
            Declaration = declaration; Requirements = requirements; Mode = BuildContentMode.Full;
            Overlay = new ContentOverlayRecord(selection, baseIdentity);
        }

        public BuildPlan Plan { get; }
        public OneStarMaker.Editor.Build.Materialization.BuildMaterializationSnapshot Snapshot { get; }
        public string ContentSet { get; }
        public string ArtifactsRoot { get; }
        public BuildPurposeDeclaration? Declaration { get; }
        public IReadOnlyList<BuildPurposeRequirement>? Requirements { get; }
        public BuildContentMode Mode { get; }
        /// <summary>上書きの build の記録。上書きでない build では null。</summary>
        public ContentOverlayRecord? Overlay { get; }
    }

    // 1回の build の結果。preflight/outcome は失敗時にも診断用に残す。
    // ContentPath がある場合だけ成功 directory として後続へ渡せる。
    public sealed class BuildContentResult
    {
        public BuildContentResult(string identity, string preflightPath, string outcomePath, string? contentPath,
            string? manifestPointer, string? metadataPath, string summary, BuildContentIssue[] issues)
            : this(identity, preflightPath, outcomePath, contentPath, manifestPointer, metadataPath, summary, issues, null)
        {
        }

        public BuildContentResult(string identity, string preflightPath, string outcomePath, string? contentPath,
            string? manifestPointer, string? metadataPath, string summary, BuildContentIssue[] issues, BuildPurposeDeclaration? declaration)
            : this(identity, preflightPath, outcomePath, contentPath, manifestPointer, metadataPath, summary, issues, declaration, BuildContentMode.Incremental)
        {
        }

        public BuildContentResult(string identity, string preflightPath, string outcomePath, string? contentPath,
            string? manifestPointer, string? metadataPath, string summary, BuildContentIssue[] issues, BuildPurposeDeclaration? declaration,
            BuildContentMode mode)
        { Mode = mode; Identity = identity; PreflightPath = preflightPath; OutcomePath = outcomePath; ContentPath = contentPath;
          ManifestPointer = manifestPointer; MetadataPath = metadataPath; Summary = summary; Issues = issues; Declaration = declaration; }
        public string Identity { get; }
        public string PreflightPath { get; }
        public string OutcomePath { get; }
        public string? ContentPath { get; }
        public string? ManifestPointer { get; }
        public string? MetadataPath { get; }
        public string Summary { get; }
        public BuildContentIssue[] Issues { get; }
        // request の宣言をそのまま運ぶ。publisher が manifest に写す（preflight の形は変えない）。
        public BuildPurposeDeclaration? Declaration { get; }
        // request の mode。outcome の buildMode に書かれ、publisher は file の側を正として読む。
        public BuildContentMode Mode { get; }
        public bool IsSuccess => ContentPath != null && Issues.Length == 0;
    }

    // JsonUtility 用の保存形。Unity の内部 BuildReport 型を外部 protocol にしない。
    // preflight と outcome は同じ形を使うが、前者は入力判断、後者は build 結果を記録する。
    // requirements と buildMode は後方互換の追加項目。既存の読み手（判定 script、Artifacts の collect、Player build の検証）は未知の項目を無視する。
    // buildMode は outcome だけに書く（preflight では空）。欄の無い旧 outcome は publish できない。
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
        public ContentReportRequirement[] requirements = Array.Empty<ContentReportRequirement>();
        public string buildMode = "";
        // 上書きの欄（後方互換の追加項目）。overlay は上書きの印で、preflight と outcome の両方に書く（publisher は outcome を読む）。
        // 印は「base の識別が空でない」から推測させず、明示の真偽で持つ。詳細（base の識別、編集、root ごとの理由、
        // 当たらなかった編集、共有 asset）は preflight だけに書く。上書きでない build では印が false、詳細は空のまま。
        public bool overlay;
        public ContentReportOverlayBase overlayBase = new ContentReportOverlayBase();
        public string[] overlayEdits = Array.Empty<string>();
        public ContentReportOverlayRoot[] overlayRoots = Array.Empty<ContentReportOverlayRoot>();
        public ContentReportOverlayUnmatched[] overlayUnmatched = Array.Empty<ContentReportOverlayUnmatched>();
        public string[] overlaySharedAssets = Array.Empty<string>();
    }

    // 上書きを重ねる base の識別（登録契約 C6 の材料）。宣言の無い base では declarationName が空。
    [Serializable] internal sealed class ContentReportOverlayBase
    {
        public string contentSet = "";
        public string revision = "";
        public string target = "";
        public string manifestDigest = "";
        public string declarationName = "";
    }

    // 上書きに入れた root と、入れた理由になった編集。
    [Serializable] internal sealed class ContentReportOverlayRoot
    {
        public string stableKey = "";
        public string logicalKey = "";
        public string[] edits = Array.Empty<string>();
    }

    // 上書きに入らなかった編集と理由 code（ContentOverlayUnmatchedReason の名前）。
    [Serializable] internal sealed class ContentReportOverlayUnmatched
    {
        public string path = "";
        public string reason = "";
    }

    // 「なぜ入っているか」: (logical key, 表現) と root からの到達経路。logicalKey、次に representation の ordinal で並ぶ。
    [Serializable] internal sealed class ContentReportRequirement
    {
        public string logicalKey = "";
        public string representation = "";
        public string[] path = Array.Empty<string>();
    }

    internal static class BuildContentReport
    {
        public static void WritePreflight(string path, string identity, string target, string contentSet,
            BuildPlan plan, BuildContentProjectionResult projection)
            => WritePreflight(path, identity, target, contentSet, plan, projection, null);

        // Unity build 前に確定した選択、除外理由、root、閉包、構造化 issue を保存する。
        // 失敗してもこのファイルを読めば、どの候補を build しようとしたか追跡できる。
        public static void WritePreflight(string path, string identity, string target, string contentSet,
            BuildPlan plan, BuildContentProjectionResult projection, IReadOnlyList<BuildPurposeRequirement>? requirements)
            => WritePreflight(path, identity, target, contentSet, plan, projection, requirements, null);

        public static void WritePreflight(string path, string identity, string target, string contentSet,
            BuildPlan plan, BuildContentProjectionResult projection, IReadOnlyList<BuildPurposeRequirement>? requirements,
            ContentOverlayRecord? overlay)
        {
            var file = new ContentReportFile {
                identity = identity, target = target, contentSet = contentSet,
                selected = plan.SelectedContent.Select(x => x.StableKey + "|" + x.LogicalKey + "|" + x.PhysicalKey).ToArray(),
                excluded = plan.ExcludedContent.Select(x => x.Candidate.StableKey + "|" + x.ReasonCode + "|" + x.Dimension + "|" + x.Value).ToArray(),
                roots = projection.Roots.Select(x => x.Candidate.StableKey + "|" + x.Path + "|" + x.Representation).ToArray(),
                closure = projection.Files.Select(x => x.Guid + "|" + x.Path).ToArray(),
                issues = projection.Issues.Select(x => x.Code + "|" + x.Key + "|" + x.Detail).ToArray(),
                requirements = (requirements ?? Array.Empty<BuildPurposeRequirement>())
                    .OrderBy(x => x.LogicalKey, StringComparer.Ordinal).ThenBy(x => x.Representation, StringComparer.Ordinal)
                    .Select(x => new ContentReportRequirement { logicalKey = x.LogicalKey, representation = x.Representation, path = x.Path.ToArray() })
                    .ToArray()
            };
            if (overlay != null)
            {
                var b = overlay.BaseIdentity;
                var s = overlay.Selection;
                file.overlay = true;
                file.overlayBase = new ContentReportOverlayBase {
                    contentSet = b.ContentSet, revision = b.Revision, target = b.Target,
                    manifestDigest = b.ManifestDigest, declarationName = b.DeclarationName ?? ""
                };
                file.overlayEdits = s.Edits.ToArray();
                file.overlayRoots = s.Roots.Select(x => new ContentReportOverlayRoot {
                    stableKey = x.Root.StableKey, logicalKey = x.Root.LogicalKey, edits = x.Edits.ToArray()
                }).ToArray();
                file.overlayUnmatched = s.Unmatched.Select(x => new ContentReportOverlayUnmatched {
                    path = x.Path, reason = x.Reason.ToString()
                }).ToArray();
                file.overlaySharedAssets = s.SharedAssets.ToArray();
            }
            Write(path, file);
        }

        public static void WriteOutcome(string path, BuildContentResult result) => WriteOutcome(path, result, false);

        public static void WriteOutcome(string path, BuildContentResult result, bool overlay)
        {
            // 成功 path / manifest / metadata と summary、または失敗 issue を build identity で結ぶ。
            // 選択と閉包の詳細は preflight が正本であり、ここに重複させない。上書きの印だけは publisher が outcome を正として読むので書く。
            Write(path, new ContentReportFile {
                identity = result.Identity, target = BuildContentCoordinator.TargetName,
                summary = result.Summary, contentPath = result.ContentPath ?? "",
                manifestPointer = result.ManifestPointer ?? "", metadataPath = result.MetadataPath ?? "",
                issues = result.Issues.Select(x => x.Code + "|" + x.Key + "|" + x.Detail).ToArray(),
                buildMode = result.Mode.ToString(),
                overlay = overlay
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
