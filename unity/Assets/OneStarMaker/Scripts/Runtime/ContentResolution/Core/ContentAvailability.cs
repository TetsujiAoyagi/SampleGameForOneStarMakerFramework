#nullable enable

using System;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>1 回の probe に対する provider の回答の種別。</summary>
    public enum ContentAvailabilityStatus
    {
        /// <summary>無効。constructor を通らない default の回答だけがこの値になり、resolver は fault として扱う。</summary>
        None = 0,

        /// <summary>候補がある。</summary>
        Available = 1,

        /// <summary>この供給元には無い。次の供給元へ進む。</summary>
        NotFound = 2,

        /// <summary>未接続・timeout 等で答えられない。次の供給元へ進む。</summary>
        Unavailable = 3,

        /// <summary>
        /// hash 不正・version 不整合・権限拒否。修復の基準になる完全な identity を伴えば探索を続け、
        /// 伴わなければ resolver はその場で止める。
        /// </summary>
        Rejected = 4,
    }

    /// <summary>
    /// 1 回の probe の回答。形の壊れた組（候補の無い Available、Available 以外の候補、
    /// Rejected 以外の expected identity、一部の欄だけの expected identity）は constructor が拒むので、
    /// resolver に届く壊れた回答は constructor を通らない default だけになる。
    /// </summary>
    public readonly struct ContentAvailability
    {
        private readonly string? _reason;

        /// <param name="expectedIdentity">
        /// <see cref="ContentAvailabilityStatus.Rejected"/> のときだけ渡せる、修復の基準（同じ identity の正規コピーなら使ってよい）。
        /// 4 欄がそろった値だけを受け付ける。
        /// </param>
        public ContentAvailability(
            ContentAvailabilityStatus status,
            ContentCandidate? candidate = null,
            ContentIdentity? expectedIdentity = null,
            string? reason = null)
        {
            if (status == ContentAvailabilityStatus.None || !Enum.IsDefined(typeof(ContentAvailabilityStatus), status))
            {
                throw new ArgumentException("Availability status is required.", nameof(status));
            }

            if (status == ContentAvailabilityStatus.Available && candidate == null)
            {
                throw new ArgumentException("An available answer must carry a candidate.", nameof(candidate));
            }

            if (status != ContentAvailabilityStatus.Available && candidate != null)
            {
                throw new ArgumentException("Only an available answer may carry a candidate.", nameof(candidate));
            }

            if (status != ContentAvailabilityStatus.Rejected && expectedIdentity.HasValue)
            {
                throw new ArgumentException("Only a rejected answer may carry an expected identity.", nameof(expectedIdentity));
            }

            ContentIdentity.ValidateOptionalPin(expectedIdentity, nameof(expectedIdentity));

            Status = status;
            Candidate = candidate;
            ExpectedIdentity = expectedIdentity;
            _reason = reason;
        }

        public ContentAvailabilityStatus Status { get; }

        public ContentCandidate? Candidate { get; }

        /// <summary>Rejected の修復基準。無ければ null。</summary>
        public ContentIdentity? ExpectedIdentity { get; }

        public string Reason => _reason ?? string.Empty;
    }
}
