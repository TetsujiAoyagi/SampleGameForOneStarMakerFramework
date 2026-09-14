#nullable enable

using System;

namespace OneStarMaker.Build.Selection
{
    public enum BuildContentCardinality
    {
        OneOrMore,
        ExactlyOne
    }

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
