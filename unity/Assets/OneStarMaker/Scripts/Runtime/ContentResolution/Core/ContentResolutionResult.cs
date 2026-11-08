#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 1 要求の決定。採用した候補（または失敗の理由）と、probe した供給元の実績、探索の終わりの pin を持つ。
    /// policy（予定）とは別の型で、loader がロード前に候補を拒んでもこの値は書き換えない。
    /// </summary>
    public sealed class ContentResolutionResult
    {
        /// <param name="candidate">採用した候補。採用していなければ null。</param>
        /// <param name="failureCode">採用したなら <see cref="ContentResolutionFailureCode.None"/>、していなければそれ以外。</param>
        /// <param name="probes">probe した供給元の実績（probe した順）。複製して保持する。</param>
        /// <param name="pinnedIdentity">探索の終わりの pin。無ければ null。</param>
        public ContentResolutionResult(
            ContentCandidate? candidate,
            ContentResolutionFailureCode failureCode,
            IEnumerable<ProviderProbe> probes,
            ContentIdentity? pinnedIdentity)
        {
            if (!Enum.IsDefined(typeof(ContentResolutionFailureCode), failureCode))
            {
                throw new ArgumentException("Unknown failure code.", nameof(failureCode));
            }

            if ((candidate != null) != (failureCode == ContentResolutionFailureCode.None))
            {
                throw new ArgumentException("A result has a candidate exactly when its failure code is None.", nameof(failureCode));
            }

            if (probes == null)
            {
                throw new ArgumentNullException(nameof(probes));
            }

            var copy = new List<ProviderProbe>();
            foreach (var probe in probes)
            {
                copy.Add(probe ?? throw new ArgumentException("Probes must not contain null.", nameof(probes)));
            }

            ContentIdentity.ValidateOptionalPin(pinnedIdentity, nameof(pinnedIdentity));

            Candidate = candidate;
            FailureCode = failureCode;
            Probes = new ReadOnlyCollection<ProviderProbe>(copy);
            PinnedIdentity = pinnedIdentity;
        }

        public bool IsResolved => Candidate != null;

        public ContentCandidate? Candidate { get; }

        public ContentResolutionFailureCode FailureCode { get; }

        /// <summary>probe した供給元の実績。採用した時点で探索を止めるので、採用後の供給元は含まない。</summary>
        public IReadOnlyList<ProviderProbe> Probes { get; }

        /// <summary>
        /// 探索の終わりの pin（要求の expected identity、または identity 付きの Rejected が与えた値）。無ければ null。
        /// pin はこの要求の中だけで持続する。再試行をまたいで同じ pin を保つのは呼び出し側・後続の作業の責任。
        /// </summary>
        public ContentIdentity? PinnedIdentity { get; }
    }
}
