#nullable enable

namespace OneStarMaker.Build.Selection
{
    public enum BuildExclusionReasonCode
    {
        UnrequestedDimension,
        ValueNotSelected
    }

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
