#nullable enable

namespace OneStarMaker.Build.Selection
{
    /// <summary>Identifies the closed set of validation outcomes produced by BS1 selection.</summary>
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
