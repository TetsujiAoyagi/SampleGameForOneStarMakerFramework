#nullable enable

using System;

namespace OneStarMaker.Build.Selection
{
    public sealed class BuildContentCandidate
    {
        public BuildContentCandidate(
            string stableKey,
            string logicalKey,
            string physicalKey,
            BuildProvenance provenance)
        {
            StableKey = RequireStable(stableKey, nameof(stableKey));
            LogicalKey = RequireStable(logicalKey, nameof(logicalKey));
            PhysicalKey = RequireStable(physicalKey, nameof(physicalKey));
            Provenance = provenance?.Snapshot() ?? throw new ArgumentNullException(nameof(provenance));
        }

        public string StableKey { get; }
        public string LogicalKey { get; }
        public string PhysicalKey { get; }
        public BuildProvenance Provenance { get; }

        internal BuildContentCandidate Snapshot() =>
            new BuildContentCandidate(StableKey, LogicalKey, PhysicalKey, Provenance);

        private static string RequireStable(string? value, string parameterName)
        {
            if (string.IsNullOrEmpty(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("Candidate keys must be non-empty and have no surrounding whitespace.", parameterName);
            return value;
        }
    }
}
