#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// Provides the immutable, canonical selection result that a later build backend may consume.
    /// </summary>
    public sealed class BuildPlan
    {
        private readonly ReadOnlyCollection<BuildContentCandidate> _selectedContent;
        private readonly ReadOnlyCollection<BuildContentExclusion> _excludedContent;

        internal BuildPlan(
            BuildRequest request,
            IEnumerable<BuildContentCandidate> selectedContent,
            IEnumerable<BuildContentExclusion> excludedContent)
        {
            Request = new BuildRequest(request.Selections);
            _selectedContent = Array.AsReadOnly(selectedContent
                .Select(candidate => candidate.Snapshot())
                .OrderBy(candidate => candidate.StableKey, StringComparer.Ordinal)
                .ToArray());
            _excludedContent = Array.AsReadOnly(excludedContent
                .Select(exclusion => new BuildContentExclusion(
                    exclusion.Candidate,
                    exclusion.ReasonCode,
                    exclusion.Dimension,
                    exclusion.Value))
                .OrderBy(exclusion => exclusion.Candidate.StableKey, StringComparer.Ordinal)
                .ToArray());
        }

        public BuildRequest Request { get; }
        public IReadOnlyList<BuildContentCandidate> SelectedContent => _selectedContent;
        public IReadOnlyList<BuildContentExclusion> ExcludedContent => _excludedContent;
    }
}
