#nullable enable

using System;

namespace OneStarMaker.Build.Selection
{
    public sealed class BuildTag : IEquatable<BuildTag>
    {
        public BuildTag(string? dimension, string? value)
        {
            Dimension = dimension;
            Value = value;
        }

        public string? Dimension { get; }
        public string? Value { get; }

        public bool Equals(BuildTag? other)
        {
            return other != null
                && string.Equals(Dimension, other.Dimension, StringComparison.Ordinal)
                && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => Equals(obj as BuildTag);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Dimension != null ? StringComparer.Ordinal.GetHashCode(Dimension) : 0) * 397)
                    ^ (Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0);
            }
        }
    }
}
