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
    /// <see cref="Representation"/> を持つ requirement は (logical key, 表現) の組で数え、
    /// 持たない requirement は従来どおり logical key だけで数えます。
    /// </summary>
    public sealed class BuildContentRequirement
    {
        public BuildContentRequirement(string logicalKey, BuildContentCardinality cardinality, string? representation = null)
        {
            if (string.IsNullOrEmpty(logicalKey) || !string.Equals(logicalKey, logicalKey.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("Logical key must be non-empty and have no surrounding whitespace.", nameof(logicalKey));
            if (representation != null && !BuildRepresentationSelection.IsStable(representation))
                throw new ArgumentException("Representation must be null or non-empty without surrounding whitespace.", nameof(representation));
            LogicalKey = logicalKey;
            Cardinality = cardinality;
            Representation = representation;
        }

        public string LogicalKey { get; }
        public BuildContentCardinality Cardinality { get; }

        /// <summary>数える表現。null なら logical key だけで数える（表現を区別しない従来の requirement）。</summary>
        public string? Representation { get; }
    }
}
