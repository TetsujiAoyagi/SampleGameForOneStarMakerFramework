#nullable enable

using System;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// materialize済みの選択候補1件を、stableなlogical identityとphysical identityで表します。
    /// </summary>
    public sealed class BuildContentCandidate
    {
        public BuildContentCandidate(
            string stableKey,
            string logicalKey,
            string physicalKey,
            BuildProvenance provenance)
            : this(stableKey, logicalKey, physicalKey, provenance, null)
        {
        }

        /// <param name="owner">
        /// 付随 asset の候補で、その asset を所有する node の identity。selector の照合には使わない metadata。
        /// tag にすると宣言が要求しない dimension として落ちるため、tag ではなく候補の属性として持つ。
        /// </param>
        public BuildContentCandidate(
            string stableKey,
            string logicalKey,
            string physicalKey,
            BuildProvenance provenance,
            string? owner)
        {
            StableKey = RequireStable(stableKey, nameof(stableKey));
            LogicalKey = RequireStable(logicalKey, nameof(logicalKey));
            PhysicalKey = RequireStable(physicalKey, nameof(physicalKey));
            Provenance = provenance?.Snapshot() ?? throw new ArgumentNullException(nameof(provenance));
            Owner = owner == null ? null : RequireStable(owner, nameof(owner));
        }

        public string StableKey { get; }
        public string LogicalKey { get; }
        public string PhysicalKey { get; }
        public BuildProvenance Provenance { get; }

        /// <summary>付随 asset の所有 node の identity。Scene payload の候補では null。</summary>
        public string? Owner { get; }

        internal BuildContentCandidate Snapshot() =>
            new BuildContentCandidate(StableKey, LogicalKey, PhysicalKey, Provenance, Owner);

        private static string RequireStable(string? value, string parameterName)
        {
            if (string.IsNullOrEmpty(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("Candidate keys must be non-empty and have no surrounding whitespace.", parameterName);
            return value;
        }
    }
}
