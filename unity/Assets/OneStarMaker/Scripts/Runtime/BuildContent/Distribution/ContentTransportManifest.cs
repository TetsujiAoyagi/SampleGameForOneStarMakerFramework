#nullable enable

using System;
using System.Collections.Generic;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    [Serializable] public sealed class ContentTransportManifest
    {
        public int version;
        public string product = "";
        public string contentSet = "";
        public string revision = "";
        public string target = "";
        public string unityVersion = "";
        public int rootSchemaVersion;
        public int playerConfigSchemaVersion;
        public ContentTransportFile[] files = Array.Empty<ContentTransportFile>();
        public ContentSourceFile[] sourceFiles = Array.Empty<ContentSourceFile>();
    }

    [Serializable] public sealed class ContentTransportFile { public string path=""; public long size; public string sha256=""; }
    [Serializable] public sealed class ContentSourceFile { public string path=""; public string sha256=""; }

    public sealed class ContentInstallRequest
    {
        public ContentInstallRequest(string manifestSha256, string contentSet, string revision, string target,
            string unityVersion, int rootSchemaVersion, long diskBudgetBytes)
        { ManifestSha256=manifestSha256; ContentSet=contentSet; Revision=revision; Target=target;
          UnityVersion=unityVersion; RootSchemaVersion=rootSchemaVersion; DiskBudgetBytes=diskBudgetBytes; }
        public string ManifestSha256 { get; }
        public string ContentSet { get; }
        public string Revision { get; }
        public string Target { get; }
        public string UnityVersion { get; }
        public int RootSchemaVersion { get; }
        public long DiskBudgetBytes { get; }
    }

    public sealed class ContentInstallResult
    {
        internal ContentInstallResult(string root, string content, ValidatedContentManifest manifest, bool existing)
        { RevisionRoot=root; ContentPath=content; ManifestSha256=manifest.Digest; ContentSet=manifest.ContentSet;
          Revision=manifest.Revision; Target=manifest.Target; AlreadyInstalled=existing; }
        public string RevisionRoot { get; }
        public string ContentPath { get; }
        public string ManifestSha256 { get; }
        public string ContentSet { get; }
        public string Revision { get; }
        public string Target { get; }
        public bool AlreadyInstalled { get; }
    }

    internal sealed class ValidatedContentManifest
    {
        internal ValidatedContentManifest(string digest, ContentTransportManifest source, IReadOnlyList<ContentTransportFile> files, long total)
        { Digest=digest; ContentSet=source.contentSet; Revision=source.revision; Target=source.target;
          UnityVersion=source.unityVersion; RootSchemaVersion=source.rootSchemaVersion;
          var copiedFiles=new List<ContentTransportFile>(files.Count);foreach(var file in files)copiedFiles.Add(new ContentTransportFile{path=file.path,size=file.size,sha256=file.sha256});Files=copiedFiles;
          var copiedSources=new List<ContentSourceFile>(source.sourceFiles.Length);foreach(var file in source.sourceFiles)copiedSources.Add(new ContentSourceFile{path=file.path,sha256=file.sha256});SourceFiles=copiedSources; TotalBytes=total; }
        internal string Digest { get; }
        internal string ContentSet { get; }
        internal string Revision { get; }
        internal string Target { get; }
        internal string UnityVersion { get; }
        internal int RootSchemaVersion { get; }
        internal IReadOnlyList<ContentTransportFile> Files { get; }
        internal IReadOnlyList<ContentSourceFile> SourceFiles { get; }
        internal long TotalBytes { get; }
    }

    public enum ContentDeliveryFailureCode { InvalidManifest, IntegrityMismatch, IdentityMismatch, TargetMismatch,
        CompatibilityMismatch, MissingFile, TransportFailure, Busy, LockUnavailable, BudgetUnsatisfied, InstallConflict, IoFailure }

    public sealed class ContentDeliveryException : InvalidOperationException
    {
        public ContentDeliveryException(ContentDeliveryFailureCode code, string message, Exception? inner=null,
            long requiredBytes=0, long availableBytes=0) : base(message, inner)
        { Code=code; RequiredBytes=requiredBytes; AvailableBytes=availableBytes; }
        public ContentDeliveryFailureCode Code { get; }
        public long RequiredBytes { get; }
        public long AvailableBytes { get; }
    }
}
