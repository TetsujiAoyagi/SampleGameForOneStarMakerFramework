#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OneStarMaker.Build.Selection;

namespace OneStarMaker.Editor.Build.Materialization
{
    public sealed class BuildMaterializationSnapshot
    {
        private readonly ReadOnlyCollection<BuildContentCandidate> _candidates;
        private readonly ReadOnlyCollection<IBuildTagProvider> _providers;
        private readonly ReadOnlyCollection<BuildContentRequirement> _requirements;
        private readonly ReadOnlyCollection<BuildDependencySnapshot> _dependencies;

        internal BuildMaterializationSnapshot(IEnumerable<BuildContentCandidate> candidates,
            IReadOnlyDictionary<string, IReadOnlyList<BuildTag>> tags,
            IEnumerable<BuildContentRequirement> requirements, IEnumerable<BuildDependencySnapshot> dependencies)
        {
            _candidates = Array.AsReadOnly(candidates.OrderBy(x => x.StableKey, StringComparer.Ordinal).ToArray());
            _providers = Array.AsReadOnly<IBuildTagProvider>(new IBuildTagProvider[] { new SnapshotTagProvider(tags) });
            _requirements = Array.AsReadOnly(requirements.OrderBy(x => x.LogicalKey, StringComparer.Ordinal).ToArray());
            _dependencies = Array.AsReadOnly(dependencies.OrderBy(x => x.RootGuid, StringComparer.Ordinal).ToArray());
        }

        public IReadOnlyList<BuildContentCandidate> Candidates => _candidates;
        public IReadOnlyList<IBuildTagProvider> TagProviders => _providers;
        public IReadOnlyList<BuildContentRequirement> Requirements => _requirements;
        public IReadOnlyList<BuildDependencySnapshot> Dependencies => _dependencies;
        public BuildDependencySnapshot? FindDependency(string physicalKey) =>
            _dependencies.FirstOrDefault(x => string.Equals(x.RootGuid, physicalKey, StringComparison.Ordinal));

        private sealed class SnapshotTagProvider : IBuildTagProvider
        {
            private readonly IReadOnlyDictionary<string, IReadOnlyList<BuildTag>> _tags;
            public SnapshotTagProvider(IReadOnlyDictionary<string, IReadOnlyList<BuildTag>> tags) =>
                _tags = new ReadOnlyDictionary<string, IReadOnlyList<BuildTag>>(tags.ToDictionary(x => x.Key,
                    x => (IReadOnlyList<BuildTag>)Array.AsReadOnly(x.Value.Select(t => new BuildTag(t.Dimension, t.Value)).ToArray()), StringComparer.Ordinal));
            public string StableProviderKey => "osm.scene-resource-map.representation.v1";
            public IReadOnlyList<BuildTag> GetTags(BuildContentCandidate candidate) =>
                _tags.TryGetValue(candidate.StableKey, out var value) ? value : Array.Empty<BuildTag>();
        }
    }

    public sealed class BuildMaterializationResult
    {
        private readonly ReadOnlyCollection<BuildMaterializationIssue> _issues;
        internal BuildMaterializationResult(BuildMaterializationSnapshot? snapshot, IEnumerable<BuildMaterializationIssue> issues)
        {
            _issues = Array.AsReadOnly(issues.Distinct().OrderBy(x => x.Code).ThenBy(x => x.Subject)
                .ThenBy(x => x.SubjectKey, StringComparer.Ordinal).ThenBy(x => x.RootGuid, StringComparer.Ordinal)
                .ThenBy(x => x.Path, StringComparer.Ordinal).ThenBy(x => x.DetailKey, StringComparer.Ordinal).ToArray());
            Snapshot = _issues.Count == 0 ? snapshot : null;
        }
        public BuildMaterializationSnapshot? Snapshot { get; }
        public IReadOnlyList<BuildMaterializationIssue> Issues => _issues;
        public bool HasErrors => _issues.Count != 0;
    }
}
