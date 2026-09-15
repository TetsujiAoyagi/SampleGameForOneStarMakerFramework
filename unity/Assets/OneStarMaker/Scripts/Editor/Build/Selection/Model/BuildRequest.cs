#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// Captures the requested dimension/value selections as an immutable canonical snapshot.
    /// Multiple values within one dimension represent an OR selection.
    /// </summary>
    public sealed class BuildRequest
    {
        private readonly ReadOnlyCollection<BuildTag> _selections;

        public BuildRequest(IEnumerable<BuildTag> selections)
        {
            if (selections == null) throw new ArgumentNullException(nameof(selections));
            _selections = Array.AsReadOnly(selections.Select(Clone)
                .Distinct()
                .OrderBy(tag => tag.Dimension, StringComparer.Ordinal)
                .ThenBy(tag => tag.Value, StringComparer.Ordinal)
                .ToArray());
        }

        public IReadOnlyList<BuildTag> Selections => _selections;

        private static BuildTag Clone(BuildTag tag)
        {
            if (tag == null) throw new ArgumentException("Selections cannot contain null.", nameof(tag));
            return new BuildTag(tag.Dimension, tag.Value);
        }
    }
}
