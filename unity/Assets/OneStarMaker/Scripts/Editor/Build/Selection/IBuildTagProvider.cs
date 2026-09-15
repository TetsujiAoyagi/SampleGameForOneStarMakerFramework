#nullable enable

using System.Collections.Generic;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// Attributes build tags to one pure candidate without performing candidate discovery.
    /// Implementations identify themselves with a stable key so issues remain deterministic.
    /// </summary>
    public interface IBuildTagProvider
    {
        /// <summary>Gets the stable ordinal key used for provider attribution.</summary>
        string StableProviderKey { get; }

        /// <summary>Returns the tags explicitly attributed to <paramref name="candidate"/>.</summary>
        IReadOnlyList<BuildTag> GetTags(BuildContentCandidate candidate);
    }
}
