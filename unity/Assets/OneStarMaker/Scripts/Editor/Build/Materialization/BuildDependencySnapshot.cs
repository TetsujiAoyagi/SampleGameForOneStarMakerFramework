#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneStarMaker.Editor.Build.Materialization
{
    public sealed class BuildDependencyEntry
    {
        public BuildDependencyEntry(string guid, string path) { Guid = guid; Path = path; }
        public string Guid { get; }
        public string Path { get; }
    }

    public sealed class BuildDependencySnapshot
    {
        private readonly ReadOnlyCollection<BuildDependencyEntry> _entries;
        public BuildDependencySnapshot(string rootGuid, string rootPath, IEnumerable<BuildDependencyEntry> entries)
        {
            RootGuid = rootGuid;
            RootPath = rootPath;
            _entries = Array.AsReadOnly(entries.Select(x => new BuildDependencyEntry(x.Guid, x.Path))
                .OrderBy(x => x.Path, StringComparer.Ordinal).ThenBy(x => x.Guid, StringComparer.Ordinal).ToArray());
        }
        public string RootGuid { get; }
        public string RootPath { get; }
        public IReadOnlyList<BuildDependencyEntry> Entries => _entries;
    }
}
