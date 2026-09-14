#nullable enable

namespace OneStarMaker.Build.Selection
{
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
