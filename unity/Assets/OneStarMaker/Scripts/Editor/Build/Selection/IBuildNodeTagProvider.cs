#nullable enable

using System.Collections.Generic;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// graph の node 水準の tag（例: 季節、職種）を供給する。candidate 水準の tag（表現）とは分け、
    /// 到達規則が OnDemand の子へ filter を掛けるときだけ使う。語彙は Game が決め、Framework は値を解釈しない。
    /// </summary>
    public interface IBuildNodeTagProvider
    {
        /// <summary>issue の帰属に使う ordinal 比較の stable key。</summary>
        string StableProviderKey { get; }

        /// <summary>この provider が付ける dimension と許可する値。表現の dimension は含めない（resolver が足す）。</summary>
        BuildTagSchema Schema { get; }

        /// <summary>
        /// <paramref name="node"/> へ付ける tag を返す。<paramref name="graph"/> は検証済み（重複・片側リンク・多親・サイクル無し）の
        /// identity → node で、祖先から値を導くときに使う。
        /// </summary>
        IReadOnlyList<BuildTag> GetTags(BuildGraphNode node, IReadOnlyDictionary<string, BuildGraphNode> graph);
    }
}
