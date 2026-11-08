#nullable enable

using System;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>探索 1 回の実績: どの供給元に聞き、何と答えられ、なぜ採った・採らなかったか。</summary>
    public sealed class ProviderProbe
    {
        public ProviderProbe(
            string providerId,
            ContentSourceKind sourceKind,
            ProbeOutcome outcome,
            ContentCandidate? candidate,
            string? reason)
        {
            if (outcome == ProbeOutcome.None || !Enum.IsDefined(typeof(ProbeOutcome), outcome))
            {
                throw new ArgumentException("Probe outcome is required.", nameof(outcome));
            }

            ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
            SourceKind = sourceKind;
            Outcome = outcome;
            Candidate = candidate;
            Reason = reason ?? string.Empty;
        }

        /// <summary>probe した provider の id（候補が名乗った id ではない）。</summary>
        public string ProviderId { get; }

        /// <summary>probe した provider の由来。</summary>
        public ContentSourceKind SourceKind { get; }

        public ProbeOutcome Outcome { get; }

        /// <summary>provider が返した候補。回答に候補が無ければ null。</summary>
        public ContentCandidate? Candidate { get; }

        public string Reason { get; }
    }
}
