#nullable enable

using System;

namespace OneStarMaker.Build.Selection
{
    /// <summary>必須logical content groupに指定できるcardinality規則を定義します。</summary>
    public enum BuildContentCardinality
    {
        OneOrMore,
        ExactlyOne
    }

    /// <summary>
    /// logical content groupに必要な選択candidate数を宣言します。
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
