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
            : this(schema, requirements, false, null)
        {
        }

        /// <param name="excludeUnrequired">
        /// true なら、requirement の無い logical key の候補を <see cref="BuildExclusionReasonCode.Unreachable"/> で、
        /// requirement はあるが表現が一致しない候補を <see cref="BuildExclusionReasonCode.RepresentationNotPreferred"/> で除外する。
        /// 宣言から到達した集合だけを採るために使い、tag の照合では落とせない候補を落とす。
        /// </param>
        /// <param name="representationDimension">
        /// 候補の表現を読む tag の dimension 名。表現付きの requirement を数えるときに必要。
        /// </param>
        public BuildSelectionPolicy(
            BuildTagSchema schema,
            IEnumerable<BuildContentRequirement>? requirements,
            bool excludeUnrequired,
            string? representationDimension)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
            _requirements = Array.AsReadOnly((requirements ?? Array.Empty<BuildContentRequirement>())
                .Select(requirement => requirement == null
                    ? throw new ArgumentException("Requirements cannot contain null.", nameof(requirements))
                    : new BuildContentRequirement(requirement.LogicalKey, requirement.Cardinality, requirement.Representation))
                .ToArray());
            if (representationDimension != null && !BuildRepresentationSelection.IsStable(representationDimension))
                throw new ArgumentException("Representation dimension must be null or stable.", nameof(representationDimension));
            // 表現で数える requirement は、候補のどの tag が表現かを知らないと数えられない。
            if (representationDimension == null && (_requirements.Any(requirement => requirement.Representation != null) || excludeUnrequired))
                throw new ArgumentException("A representation dimension is required to count representations or exclude unrequired candidates.", nameof(representationDimension));
            ExcludeUnrequired = excludeUnrequired;
            RepresentationDimension = representationDimension;
        }

        public BuildTagSchema Schema { get; }
        public IReadOnlyList<BuildContentRequirement> Requirements => _requirements;
        public bool ExcludeUnrequired { get; }
        public string? RepresentationDimension { get; }
    }
}
