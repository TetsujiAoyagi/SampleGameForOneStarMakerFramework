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
        CardinalityViolation
    }
}
