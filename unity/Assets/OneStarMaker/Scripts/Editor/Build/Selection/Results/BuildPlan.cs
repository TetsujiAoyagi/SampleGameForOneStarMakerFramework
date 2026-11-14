#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// 後続のbuild backendが消費できる、immutableかつcanonicalな選択結果を提供します。
    /// </summary>
    public sealed class BuildPlan
    {
        private readonly ReadOnlyCollection<BuildContentCandidate> _selectedContent;
        private readonly ReadOnlyCollection<BuildContentExclusion> _excludedContent;

        internal BuildPlan(
            BuildRequest request,
            IEnumerable<BuildContentCandidate> selectedContent,
            IEnumerable<BuildContentExclusion> excludedContent)
        {
            Request = new BuildRequest(request.Selections);
            _selectedContent = Array.AsReadOnly(selectedContent
                .Select(candidate => candidate.Snapshot())
                .OrderBy(candidate => candidate.StableKey, StringComparer.Ordinal)
                .ToArray());
            _excludedContent = Array.AsReadOnly(excludedContent
                .Select(exclusion => new BuildContentExclusion(
                    exclusion.Candidate,
                    exclusion.ReasonCode,
                    exclusion.Dimension,
                    exclusion.Value))
                .OrderBy(exclusion => exclusion.Candidate.StableKey, StringComparer.Ordinal)
                .ToArray());
        }

        public BuildRequest Request { get; }
        public IReadOnlyList<BuildContentCandidate> SelectedContent => _selectedContent;
        public IReadOnlyList<BuildContentExclusion> ExcludedContent => _excludedContent;

        /// <summary>
        /// selected のうち <paramref name="stableKeys"/> の候補だけを残した plan を返す。外れた候補は
        /// <see cref="BuildExclusionReasonCode.NotInSubset"/> の除外になり、元の除外は理由ごとそのまま残る。
        /// 候補の 3 つの key は変えないので、元の plan と同じ snapshot がそのまま対になる。
        /// </summary>
        /// <remarks>
        /// plan の外から候補を足したり、除外済みの候補を戻したりはできない（selected の部分集合だけ）。
        /// internal のコンストラクタを他の assembly へ開かずに plan を作り直せる唯一の道で、
        /// selected と excluded が snapshot の候補を過不足なく分ける不変条件を保ったまま絞るため、plan 自身に置く。
        /// 絞った plan は宣言の選択の一部しか持たないので、その成果物は publish で拒まれる。
        /// </remarks>
        /// <exception cref="ArgumentException">集合が空、または selected に無い stable key を含む。</exception>
        public BuildPlan WithSelectedSubset(IEnumerable<string> stableKeys)
        {
            if (stableKeys == null) throw new ArgumentNullException(nameof(stableKeys));
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in stableKeys)
            {
                if (key == null) throw new ArgumentException("Stable keys must not contain null.", nameof(stableKeys));
                keep.Add(key);
            }
            if (keep.Count == 0)
                throw new ArgumentException("The subset must name at least one selected candidate.", nameof(stableKeys));
            var selectedKeys = new HashSet<string>(_selectedContent.Select(x => x.StableKey), StringComparer.Ordinal);
            var unknown = keep.Where(x => !selectedKeys.Contains(x)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (unknown.Length != 0)
                throw new ArgumentException("The subset names candidates that are not selected: " + string.Join(", ", unknown), nameof(stableKeys));
            var dropped = _selectedContent.Where(x => !keep.Contains(x.StableKey))
                .Select(x => new BuildContentExclusion(x, BuildExclusionReasonCode.NotInSubset, "StableKey", x.StableKey));
            return new BuildPlan(Request, _selectedContent.Where(x => keep.Contains(x.StableKey)), _excludedContent.Concat(dropped));
        }
    }
}
