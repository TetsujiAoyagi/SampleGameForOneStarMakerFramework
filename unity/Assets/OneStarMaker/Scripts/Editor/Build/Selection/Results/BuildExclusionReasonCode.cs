#nullable enable

namespace OneStarMaker.Build.Selection
{
    /// <summary>正常なcandidateが成功planから除外された理由を識別します。</summary>
    public enum BuildExclusionReasonCode
    {
        UnrequestedDimension,
        ValueNotSelected
    }

    /// <summary>
    /// 照合で除外されたcandidateと、その構造化された決定的な理由を保持します。
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
