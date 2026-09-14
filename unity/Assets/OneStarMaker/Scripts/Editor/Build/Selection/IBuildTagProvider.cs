#nullable enable

using System.Collections.Generic;

namespace OneStarMaker.Build.Selection
{
    public interface IBuildTagProvider
    {
        string StableProviderKey { get; }
        IReadOnlyList<BuildTag> GetTags(BuildContentCandidate candidate);
    }
}
