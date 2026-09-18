#nullable enable

using System;

namespace OneStarMaker.Runtime.BuildContent
{
    public enum ContentDirectoryFailureCode
    {
        InvalidConfiguration, DirectoryNotRegistered, RegistrationFailed, InvalidRoot,
        UnsupportedSchema, IdentityMismatch, TargetMismatch, EntryMissing, EntryAmbiguous,
        TypeMismatch, OperationFailed, ResourcesInUse, RevisionBusy, PathInUse, DeletionInProgress,
    }

    /// <summary>Content Directory の公開境界で返す、再試行可否を判断できる失敗情報。</summary>
    public sealed class ContentDirectoryException : InvalidOperationException
    {
        public ContentDirectoryException(ContentDirectoryFailureCode code, string message,
            string? buildIdentity = null, string? target = null, string? logicalKey = null,
            string? representation = null, Exception? innerException = null)
            : base(message, innerException)
        {
            Code = code; BuildIdentity = buildIdentity; Target = target;
            LogicalKey = logicalKey; Representation = representation;
        }

        public ContentDirectoryFailureCode Code { get; }
        public string? BuildIdentity { get; }
        public string? Target { get; }
        public string? LogicalKey { get; }
        public string? Representation { get; }
    }
}
