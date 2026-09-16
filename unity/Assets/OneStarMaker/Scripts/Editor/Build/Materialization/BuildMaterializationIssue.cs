#nullable enable

using System;

namespace OneStarMaker.Editor.Build.Materialization
{
    public enum BuildMaterializationIssueCode
    {
        NullMap, NullResource, InvalidResourceIdentity, DuplicateLogicalKey, MissingDescription,
        NullPayload, InvalidVariant, MissingReference, InvalidRootGuid, UnresolvedRootGuid,
        RootGuidRoundTripMismatch, MissingRootAsset, InvalidDependencyPath, UnresolvedDependencyGuid,
        MissingDependencyAsset, DependencyIdentityCollision, DuplicateStableKey, PhysicalKeyCollision,
        GatewayFailure
    }

    public enum BuildMaterializationSubject { Map, Resource, Payload, Dependency, Gateway }

    public sealed class BuildMaterializationIssue : IEquatable<BuildMaterializationIssue>
    {
        public BuildMaterializationIssue(BuildMaterializationIssueCode code, BuildMaterializationSubject subject,
            string subjectKey, string? rootGuid = null, string? path = null, string? detailKey = null, string? message = null)
        {
            Code = code;
            Subject = subject;
            SubjectKey = subjectKey ?? string.Empty;
            RootGuid = rootGuid;
            Path = path;
            DetailKey = detailKey;
            Message = message ?? string.Empty;
        }

        public BuildMaterializationIssueCode Code { get; }
        public BuildMaterializationSubject Subject { get; }
        public string SubjectKey { get; }
        public string? RootGuid { get; }
        public string? Path { get; }
        public string? DetailKey { get; }
        public string Message { get; }

        public bool Equals(BuildMaterializationIssue? other) => other != null && Code == other.Code && Subject == other.Subject
            && string.Equals(SubjectKey, other.SubjectKey, StringComparison.Ordinal)
            && string.Equals(RootGuid, other.RootGuid, StringComparison.Ordinal)
            && string.Equals(Path, other.Path, StringComparison.Ordinal)
            && string.Equals(DetailKey, other.DetailKey, StringComparison.Ordinal);
        public override bool Equals(object? obj) => Equals(obj as BuildMaterializationIssue);
        public override int GetHashCode() => HashCode.Combine(Code, Subject, SubjectKey, RootGuid, Path, DetailKey);
    }
}
