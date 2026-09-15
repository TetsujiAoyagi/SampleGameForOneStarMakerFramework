#nullable enable

namespace OneStarMaker.Build.Selection
{
    /// <summary>Identifies why a valid candidate was omitted from a successful plan.</summary>
    public enum BuildExclusionReasonCode
    {
        UnrequestedDimension,
        ValueNotSelected
    }

    /// <summary>
    /// Records a candidate excluded by matching, together with its structured deterministic reason.
    /// </summary>
    public sealed class BuildContentExclusion
    {
        public BuildContentExclusion(
            BuildContentCandidate candidate,
            BuildExclusionReasonCode reasonCode,
            string dimension,
            string value)
        {
            Candidate = candidate.Snapshot();
            ReasonCode = reasonCode;
            Dimension = dimension;
            Value = value;
        }

        public BuildContentCandidate Candidate { get; }
        public BuildExclusionReasonCode ReasonCode { get; }
        public string Dimension { get; }
        public string Value { get; }
    }
}
