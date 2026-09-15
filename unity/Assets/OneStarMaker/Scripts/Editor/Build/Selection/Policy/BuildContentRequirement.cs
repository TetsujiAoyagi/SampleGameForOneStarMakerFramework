#nullable enable

using System;

namespace OneStarMaker.Build.Selection
{
    /// <summary>Defines the supported cardinality rules for a required logical content group.</summary>
    public enum BuildContentCardinality
    {
        OneOrMore,
        ExactlyOne
    }

    /// <summary>
    /// Requires a logical content group to contain the specified number of selected candidates.
    /// </summary>
    public sealed class BuildContentRequirement
    {
        public BuildContentRequirement(string logicalKey, BuildContentCardinality cardinality)
        {
            if (string.IsNullOrEmpty(logicalKey) || !string.Equals(logicalKey, logicalKey.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("Logical key must be non-empty and have no surrounding whitespace.", nameof(logicalKey));
            LogicalKey = logicalKey;
            Cardinality = cardinality;
        }

        public string LogicalKey { get; }
        public BuildContentCardinality Cardinality { get; }
    }
}
