#nullable enable

using System.Collections.Generic;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// candidate探索を行わず、1つのpure candidateへbuild tagを付与します。
    /// issueの決定性を保つため、実装はstable keyで自身を識別します。
    /// </summary>
    public interface IBuildTagProvider
    {
        /// <summary>provider attributionに使うordinal比較のstable keyを取得します。</summary>
        string StableProviderKey { get; }

        /// <summary><paramref name="candidate"/>へ明示的に付与するtagを返します。</summary>
        IReadOnlyList<BuildTag> GetTags(BuildContentCandidate candidate);
    }
}
