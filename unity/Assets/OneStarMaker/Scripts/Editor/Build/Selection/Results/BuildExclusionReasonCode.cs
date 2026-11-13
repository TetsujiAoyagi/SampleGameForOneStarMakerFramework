#nullable enable

namespace OneStarMaker.Build.Selection
{
    /// <summary>正常なcandidateが成功planから除外された理由を識別します。</summary>
    public enum BuildExclusionReasonCode
    {
        UnrequestedDimension,
        ValueNotSelected,
        /// <summary>logical key に requirement が無い。宣言の root から到達しない node（または到達しない node の付随 asset）の候補。</summary>
        Unreachable,
        /// <summary>logical key には到達したが、その表現は優先順で選ばれなかった。</summary>
        RepresentationNotPreferred,
        /// <summary>
        /// 選択された候補だったが、<see cref="BuildPlan.WithSelectedSubset"/> の指定した部分集合に入らなかった。
        /// この理由を持つ plan は宣言から導いた選択の一部だけを build するので、配布してよい成果物を作らない
        /// （publish する側はこの code を見て拒む）。末尾に足す（既存の値の整数を変えない）。
        /// </summary>
        NotInSubset
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
