#nullable enable

namespace OneStarMaker.Build.Selection
{
    /// <summary>BS1 selectionが生成するvalidation結果の閉じた集合を識別します。</summary>
    public enum BuildValidationCode
    {
        InvalidRequestSelection,
        UnknownDimension,
        UnknownValue,
        DuplicateTag,
        ConflictingCandidateValues,
        DuplicateCandidateKey,
        PhysicalKeyCollision,
        DuplicateRequirement,
        ConflictingRequirement,
        RequiredGroupMissing,
        CardinalityViolation,
        // 以下は宣言からの到達（BuildPurposeResolver）が graph を検査して出す。1 件でもあれば plan を公開しない。
        GraphDuplicateNode,
        GraphMissingRoot,
        GraphOrphan,
        GraphLinkMismatch,
        GraphCycle
    }
}
