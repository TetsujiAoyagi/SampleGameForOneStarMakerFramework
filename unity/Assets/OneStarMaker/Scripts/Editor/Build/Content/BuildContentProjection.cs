#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;

namespace OneStarMaker.Editor.Build.Content
{
    // build 前に確定できる入力不整合と、Unity の build 実行中に起きた失敗を同じ結果面で扱う。
    // 呼出側は文言ではなく Code で分岐し、Key と Detail は診断・report に残す。
    public enum BuildContentIssueCode
    {
        MissingCandidate, ExtraSelectedCandidate, CandidateMismatch, PhysicalKeyCollision,
        MissingClosure, DuplicateClosure, RootMismatch, DependencyMismatch, PathCollision,
        InvalidRepresentation, BuildFailure
    }

    public sealed class BuildContentIssue
    {
        public BuildContentIssue(BuildContentIssueCode code, string key, string detail)
        { Code = code; Key = key; Detail = detail; }
        public BuildContentIssueCode Code { get; }
        public string Key { get; }
        public string Detail { get; }
    }

    // 1つの選択候補に対応する「build root」と既存の依存閉包。
    // Path は materialization 時点の root path であり、ここで AssetDatabase を再走査しない。
    internal sealed class ProjectedContent
    {
        internal ProjectedContent(BuildContentCandidate candidate, string path, string representation,
            IReadOnlyList<BuildDependencyEntry> closure)
        { Candidate = candidate; Path = path; Representation = representation; Closure = closure; }
        public BuildContentCandidate Candidate { get; }
        public string Path { get; }
        public string Representation { get; }
        public IReadOnlyList<BuildDependencyEntry> Closure { get; }
    }

    // adapter に渡す canonical な入力。Roots は選択 root のみ、Files はそれらの閉包の和集合。
    // 同じ依存ファイルを複数 root が共有しても Files には一度だけ載せる。
    internal sealed class BuildContentProjectionResult
    {
        internal BuildContentProjectionResult(IEnumerable<ProjectedContent> roots,
            IEnumerable<BuildDependencyEntry> files, IEnumerable<BuildContentIssue> issues)
        {
            Roots = Array.AsReadOnly(roots.ToArray());
            Files = Array.AsReadOnly(files.ToArray());
            Issues = Array.AsReadOnly(issues.ToArray());
        }
        public IReadOnlyList<ProjectedContent> Roots { get; }
        public IReadOnlyList<BuildDependencyEntry> Files { get; }
        public IReadOnlyList<BuildContentIssue> Issues { get; }
        public bool IsValid => Issues.Count == 0;
    }

    // Unity I/O を持たない中核。成功済み plan と対応 snapshot の整合性だけを調べる。
    // 選択 policy や依存探索をここでやり直すと、BS1/BS2a の判断と build 入力がずれるため行わない。
    internal static class BuildContentProjection
    {
        public static BuildContentProjectionResult Create(BuildPlan plan, BuildMaterializationSnapshot snapshot)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var issues = new List<BuildContentIssue>();
            var roots = new List<ProjectedContent>();
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            var fileGuids = new Dictionary<string, string>(StringComparer.Ordinal);
            // 入力順で build 結果や issue の並びが変わらないよう、候補と出力を安定 key で扱う。
            // 重複 StableKey は辞書化の前に配列として保持し、後段で不整合として報告する。
            var all = snapshot.Candidates.GroupBy(x => x.StableKey, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
            var selected = plan.SelectedContent.OrderBy(x => x.StableKey, StringComparer.Ordinal).ToArray();
            var selectedKeys = new HashSet<string>(selected.Select(x => x.StableKey), StringComparer.Ordinal);
            var excludedKeys = new HashSet<string>(plan.ExcludedContent.Select(x => x.Candidate.StableKey), StringComparer.Ordinal);
            foreach (var group in selected.GroupBy(x => x.StableKey, StringComparer.Ordinal))
                if (group.Count() > 1 || excludedKeys.Contains(group.Key))
                    issues.Add(new BuildContentIssue(BuildContentIssueCode.CandidateMismatch, group.Key, "Candidate occurs more than once in plan."));
            foreach (var exclusion in plan.ExcludedContent)
            {
                var candidate = exclusion.Candidate;
                if (!all.TryGetValue(candidate.StableKey, out var matches))
                    issues.Add(new BuildContentIssue(BuildContentIssueCode.MissingCandidate, candidate.StableKey, "Excluded candidate absent from snapshot."));
                else if (matches.Length != 1 || !Same(candidate, matches[0]))
                    issues.Add(new BuildContentIssue(BuildContentIssueCode.CandidateMismatch, candidate.StableKey, "Excluded candidate keys mismatch."));
            }
            foreach (var candidate in snapshot.Candidates)
                if (!selectedKeys.Contains(candidate.StableKey) && !excludedKeys.Contains(candidate.StableKey))
                    issues.Add(new BuildContentIssue(BuildContentIssueCode.MissingCandidate, candidate.StableKey, "Plan omits snapshot candidate."));
            var physical = new Dictionary<string, string>(StringComparer.Ordinal);
            // 依存ファイルの共有は許すが、二つの候補が同じ physical key を所有するのは不可。
            // この二種類の「重複」を混同すると、正しい共有依存まで build 前に落としてしまう。
            foreach (var group in snapshot.Candidates.GroupBy(x => x.PhysicalKey, StringComparer.Ordinal))
                if (group.Select(x => x.StableKey).Distinct(StringComparer.Ordinal).Count() > 1)
                    issues.Add(new BuildContentIssue(BuildContentIssueCode.PhysicalKeyCollision, group.Key, "Snapshot candidate ownership collision."));
            foreach (var candidate in selected)
            {
                if (!all.TryGetValue(candidate.StableKey, out var matches))
                {
                    issues.Add(new BuildContentIssue(BuildContentIssueCode.ExtraSelectedCandidate, candidate.StableKey, "Selected candidate absent from snapshot."));
                    continue;
                }
                if (matches.Length != 1 || !Same(candidate, matches[0]))
                {
                    issues.Add(new BuildContentIssue(BuildContentIssueCode.CandidateMismatch, candidate.StableKey, "Stable, logical or physical key mismatch."));
                    continue;
                }
                if (physical.TryGetValue(candidate.PhysicalKey, out var owner))
                    issues.Add(new BuildContentIssue(BuildContentIssueCode.PhysicalKeyCollision, candidate.PhysicalKey, owner + "/" + candidate.StableKey));
                else physical.Add(candidate.PhysicalKey, candidate.StableKey);

                // materialization が保存した閉包をそのまま消費する。root に対応する閉包は厳密に1件。
                // 欠損・重複を Unity build に渡してから発見するより、preflight issue として固定する。
                var closures = snapshot.Dependencies.Where(x => x.RootGuid == candidate.PhysicalKey).ToArray();
                if (closures.Length != 1)
                {
                    issues.Add(new BuildContentIssue(closures.Length == 0 ? BuildContentIssueCode.MissingClosure : BuildContentIssueCode.DuplicateClosure,
                        candidate.StableKey, candidate.PhysicalKey));
                    continue;
                }
                var closure = closures[0];
                if (!Valid(closure.RootGuid, closure.RootPath) || closure.RootGuid != candidate.PhysicalKey)
                    issues.Add(new BuildContentIssue(BuildContentIssueCode.RootMismatch, candidate.StableKey, closure.RootPath));
                var entries = closure.Entries.OrderBy(x => x.Path, StringComparer.Ordinal)
                    .ThenBy(x => x.Guid, StringComparer.Ordinal).ToArray();
                if (!entries.Any(x => x.Guid == closure.RootGuid && x.Path == closure.RootPath))
                    issues.Add(new BuildContentIssue(BuildContentIssueCode.RootMismatch, candidate.StableKey, "Root absent from closure."));
                var localGuids = new Dictionary<string, string>(StringComparer.Ordinal);
                // GUID→path と path→GUID の両方向を照合する。同一 path の異なる GUID、
                // 同一 GUID の異なる path は、共有依存として正規化できないため issue にする。
                foreach (var entry in entries)
                {
                    if (!Valid(entry.Guid, entry.Path) || (localGuids.TryGetValue(entry.Guid, out var path) && path != entry.Path))
                        issues.Add(new BuildContentIssue(BuildContentIssueCode.DependencyMismatch, candidate.StableKey, entry.Guid + ":" + entry.Path));
                    else localGuids[entry.Guid] = entry.Path;
                    if (files.TryGetValue(entry.Path, out var guid) && guid != entry.Guid)
                        issues.Add(new BuildContentIssue(BuildContentIssueCode.PathCollision, entry.Path, guid + "/" + entry.Guid));
                    else files[entry.Path] = entry.Guid;
                    if (fileGuids.TryGetValue(entry.Guid, out var existingPath) && existingPath != entry.Path)
                        issues.Add(new BuildContentIssue(BuildContentIssueCode.DependencyMismatch, entry.Guid,
                            existingPath + "/" + entry.Path));
                    else fileGuids[entry.Guid] = entry.Path;
                }
                // Representation は既存 tag の値だけを保持する。provider 名や Scene 固有語彙は読まない。
                // 未指定は空文字とし、複数の異なる値は暗黙に一つを選ばず preflight で止める。
                var tags = snapshot.TagProviders.SelectMany(x => x.GetTags(matches[0]))
                    .Where(x => x.Dimension == "Representation").Select(x => x.Value).Distinct(StringComparer.Ordinal).ToArray();
                if (tags.Length > 1 || tags.Any(string.IsNullOrEmpty))
                    issues.Add(new BuildContentIssue(BuildContentIssueCode.InvalidRepresentation, candidate.StableKey, "Ambiguous representation."));
                roots.Add(new ProjectedContent(candidate, closure.RootPath, tags.Length == 1 ? tags[0]! : "",
                    Array.AsReadOnly(entries)));
            }
            // Issue も canonical な順序へ揃え、同じ入力集合なら report とテスト結果を比較できるようにする。
            return new BuildContentProjectionResult(roots,
                files.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new BuildDependencyEntry(x.Value, x.Key)),
                issues.OrderBy(x => x.Code).ThenBy(x => x.Key, StringComparer.Ordinal).ThenBy(x => x.Detail, StringComparer.Ordinal));
        }

        private static bool Same(BuildContentCandidate a, BuildContentCandidate b) =>
            a.StableKey == b.StableKey && a.LogicalKey == b.LogicalKey && a.PhysicalKey == b.PhysicalKey;

        private static bool Valid(string guid, string path) =>
            !string.IsNullOrWhiteSpace(guid) && !string.IsNullOrWhiteSpace(path) &&
            path.StartsWith("Assets/", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains('\\');
    }
}
