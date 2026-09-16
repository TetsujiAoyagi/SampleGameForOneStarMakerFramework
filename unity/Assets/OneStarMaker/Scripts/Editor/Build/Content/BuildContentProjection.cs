#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;

namespace OneStarMaker.Editor.Build.Content
{
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

    public sealed class ProjectedContent
    {
        public ProjectedContent(BuildContentCandidate candidate, string path, string representation,
            IReadOnlyList<BuildDependencyEntry> closure)
        { Candidate = candidate; Path = path; Representation = representation; Closure = closure; }
        public BuildContentCandidate Candidate { get; }
        public string Path { get; }
        public string Representation { get; }
        public IReadOnlyList<BuildDependencyEntry> Closure { get; }
    }

    public sealed class BuildContentProjectionResult
    {
        public BuildContentProjectionResult(IEnumerable<ProjectedContent> roots,
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

    // No Unity I/O: this layer consumes only the frozen plan and materialization snapshot.
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
                var tags = snapshot.TagProviders.SelectMany(x => x.GetTags(matches[0]))
                    .Where(x => x.Dimension == "Representation").Select(x => x.Value).Distinct(StringComparer.Ordinal).ToArray();
                if (tags.Length > 1 || tags.Any(string.IsNullOrEmpty))
                    issues.Add(new BuildContentIssue(BuildContentIssueCode.InvalidRepresentation, candidate.StableKey, "Ambiguous representation."));
                roots.Add(new ProjectedContent(candidate, closure.RootPath, tags.Length == 1 ? tags[0]! : "",
                    Array.AsReadOnly(entries)));
            }
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
