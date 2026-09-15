#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// 1回の選択で使う独立したtag schemaとlogical content requirementをまとめます。
    /// </summary>
    public sealed class BuildSelectionPolicy
    {
        private readonly ReadOnlyCollection<BuildContentRequirement> _requirements;

        public BuildSelectionPolicy(BuildTagSchema schema, IEnumerable<BuildContentRequirement>? requirements = null)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _requirements = Array.AsReadOnly((requirements ?? Array.Empty<BuildContentRequirement>())
                .Select(requirement => requirement == null
                    ? throw new ArgumentException("Requirements cannot contain null.", nameof(requirements))
                    : new BuildContentRequirement(requirement.LogicalKey, requirement.Cardinality))
                .ToArray());
        }

        public BuildTagSchema Schema { get; }
        public IReadOnlyList<BuildContentRequirement> Requirements => _requirements;
    }
}
