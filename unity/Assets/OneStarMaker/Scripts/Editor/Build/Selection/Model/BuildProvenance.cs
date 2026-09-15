#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneStarMaker.Build.Selection
{
    public sealed class BuildProvenance
    {
        private readonly ReadOnlyCollection<KeyValuePair<string, string>> _properties;

        public BuildProvenance(
            string sourceKind,
            string sourceId,
            IEnumerable<KeyValuePair<string, string>>? properties = null)
        {
            SourceKind = RequireStable(sourceKind, nameof(sourceKind));
            SourceId = RequireStable(sourceId, nameof(sourceId));
            var snapshot = (properties ?? Array.Empty<KeyValuePair<string, string>>())
                .Select(pair => new KeyValuePair<string, string>(
                    RequireStable(pair.Key, nameof(properties)),
                    RequireStable(pair.Value, nameof(properties))))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ThenBy(pair => pair.Value, StringComparer.Ordinal)
                .ToArray();
            _properties = Array.AsReadOnly(snapshot);
        }

        public string SourceKind { get; }
        public string SourceId { get; }
        public IReadOnlyList<KeyValuePair<string, string>> Properties => _properties;

        internal BuildProvenance Snapshot() => new BuildProvenance(SourceKind, SourceId, _properties);

        private static string RequireStable(string? value, string parameterName)
        {
            if (string.IsNullOrEmpty(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("Stable provenance strings must be non-empty and have no surrounding whitespace.", parameterName);
            return value;
        }
    }
}
