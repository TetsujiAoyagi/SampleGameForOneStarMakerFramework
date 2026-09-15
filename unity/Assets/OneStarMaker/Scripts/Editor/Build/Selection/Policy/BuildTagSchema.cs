#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// domain固有の意味を持たず、project定義の各tag dimensionで許可するvalueを宣言します。
    /// </summary>
    public sealed class BuildTagSchema
    {
        private readonly ReadOnlyDictionary<string, IReadOnlyList<string>> _dimensions;

        public BuildTagSchema(IEnumerable<KeyValuePair<string, IEnumerable<string>>> dimensions)
        {
            if (dimensions == null) throw new ArgumentNullException(nameof(dimensions));
            var copy = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var pair in dimensions)
            {
                if (!IsStable(pair.Key)) throw new ArgumentException("Schema dimensions must be non-empty and have no surrounding whitespace.", nameof(dimensions));
                if (copy.ContainsKey(pair.Key)) throw new ArgumentException("Schema dimensions must be unique.", nameof(dimensions));
                var values = pair.Value == null
                    ? Array.Empty<string>()
                    : pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray();
                if (values.Any(value => !IsStable(value)) || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                    throw new ArgumentException("Schema values must be unique, non-empty, and have no surrounding whitespace.", nameof(dimensions));
                copy.Add(pair.Key, Array.AsReadOnly(values));
            }
            _dimensions = new ReadOnlyDictionary<string, IReadOnlyList<string>>(copy);
        }

        public IReadOnlyDictionary<string, IReadOnlyList<string>> Dimensions => _dimensions;

        internal bool ContainsDimension(string dimension) => _dimensions.ContainsKey(dimension);

        internal bool ContainsValue(string dimension, string value)
        {
            return _dimensions.TryGetValue(dimension, out var values)
                && values.Contains(value, StringComparer.Ordinal);
        }

        private static bool IsStable(string? value) =>
            !string.IsNullOrEmpty(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }
}
