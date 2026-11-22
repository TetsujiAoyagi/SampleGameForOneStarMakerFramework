#nullable enable

using System;
using System.Collections.Generic;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    [Serializable]
    public sealed class ContentTransportManifest
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
        // 任意。directory を作った宣言の写し。無い manifest（旧 build）は name が空のまま読まれ、validation は見ない。
        public ContentManifestDeclaration declaration = new();
        // 任意。目的 build が含む entry の索引で、manifest の digest に覆われる。JsonUtility は入れ子の欄を省けないので、
        // 索引を持たない publish も version 0 の空の索引を書く。欄が無い manifest（索引の追加より前の build）と version 0 は
        // どちらも「索引なし」。install と再検証は JSON の型だけを見て、版と値の意味は ContentPurposeIndex だけが見る
        // （索引の版を上げても、古い runtime が manifest 全体を install・再検証できるようにするため）。
        public ContentTransportIndex index = new();
        // 任意。base の build の時点の root ごとの依存閉包（手元の編集から上書きを決める Editor の計画だけが読む）。
        // index と同じく JsonUtility が欄を省けないので、記録を持たない publish も version 0 の空の欄を書く。
        // install と再検証はこの欄を見ない（版を上げても古い runtime が install できるように、型の検査も足さない）。
        public ContentSourceClosure sourceClosure = new();
    }

    // 閉包の記録。root の表、asset の表、所属の組の表からなる。asset は自分を閉包に持つ root の集合（所属の組）を番号で指し、
    // 同じ集合は組の表に 1 回だけ置く（広く共有された asset の分だけ大きさが閉包の大きさの和にならないように）。
    // hash は持たず、asset の path で manifest の sourceFiles と突き合わせる。値の規則は Editor の読み口だけが持つ。
    [Serializable]
    public sealed class ContentSourceClosure
    {
        // 0 は記録なし、1 はこの形。2 以上は読み手が「未対応」として使わない。
        public int version;
        public ContentSourceClosureRoot[] roots = Array.Empty<ContentSourceClosureRoot>();
        public ContentSourceClosureAsset[] assets = Array.Empty<ContentSourceClosureAsset>();
        public ContentSourceClosureGroup[] groups = Array.Empty<ContentSourceClosureGroup>();
    }

    // index の entry と同じ (logical key, kind, 表現) に、root の asset の GUID を足したもの。
    [Serializable]
    public sealed class ContentSourceClosureRoot
    {
        public string logicalKey = "";
        public string kind = "";
        public string representation = "";
        public string guid = "";
    }

    // 閉包の asset 1 つ。group は groups の番号。
    [Serializable]
    public sealed class ContentSourceClosureAsset
    {
        public string guid = "";
        public string path = "";
        public int group;
    }

    // この組を指す asset を閉包に持つ root の番号（roots の番号）の集合。
    [Serializable]
    public sealed class ContentSourceClosureGroup
    {
        public int[] roots = Array.Empty<int>();
    }

    [Serializable]
    public sealed class ContentTransportIndex
    {
        // 0 は索引なし、1 は ContentTransportIndexEntry の形。2 以上は読み手が「未対応の索引」として飛ばす。
        public int version;
        public ContentTransportIndexEntry[] entries = Array.Empty<ContentTransportIndexEntry>();
    }

    // directory の BuildContentRoot の entry と同じ生の値。表現は Representation の tag の無い root では空のままで、
    // 空を Full と読み替えるのは読み手（ContentEntryMatch）だけ。stable key と Unity の locator は配布の protocol にしないので持たない。
    [Serializable]
    public sealed class ContentTransportIndexEntry
    {
        public string logicalKey = "";
        // BuildContentKind の名前（"Scene" / "Object"）。
        public string kind = "";
        public string representation = "";
    }

    [Serializable]
    public sealed class ContentManifestDeclaration
    {
        public string name = "";
        public string representation = "";
        public ContentManifestDeclarationValue[] runtimeDefaults = Array.Empty<ContentManifestDeclarationValue>();
    }

    [Serializable]
    public sealed class ContentManifestDeclarationValue
    {
        public string key = "";
        public string value = "";
    }

    [Serializable]
    public sealed class ContentTransportFile
    {
        public string path = "";
        public long size;
        public string sha256 = "";
    }

    [Serializable]
    public sealed class ContentSourceFile
    {
        public string path = "";
        public string sha256 = "";
    }

    public sealed class ContentInstallRequest
    {
        public ContentInstallRequest(string manifestSha256, string contentSet, string revision, string target,
            string unityVersion, int rootSchemaVersion, long diskBudgetBytes)
        {
            ManifestSha256 = manifestSha256;
            ContentSet = contentSet;
            Revision = revision;
            Target = target;
            UnityVersion = unityVersion;
            RootSchemaVersion = rootSchemaVersion;
            DiskBudgetBytes = diskBudgetBytes;
        }
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
        {
            RevisionRoot = root;
            ContentPath = content;
            ManifestSha256 = manifest.Digest;
            ContentSet = manifest.ContentSet;
            Revision = manifest.Revision;
            Target = manifest.Target;
            AlreadyInstalled = existing;
        }
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
        {
            Digest = digest;
            ContentSet = source.contentSet;
            Revision = source.revision;
            Target = source.target;
            UnityVersion = source.unityVersion;
            RootSchemaVersion = source.rootSchemaVersion;
            // caller 所有の serialization DTO を保持すると、検証後の変更が install に流入する。
            // 入れ子 entry も複製し、実行側だけが所有する snapshot とする。
            var copiedFiles = new List<ContentTransportFile>(files.Count);
            foreach (var file in files)
                copiedFiles.Add(new ContentTransportFile { path = file.path, size = file.size, sha256 = file.sha256 });
            Files = copiedFiles;
            var copiedSources = new List<ContentSourceFile>(source.sourceFiles.Length);
            foreach (var file in source.sourceFiles)
                copiedSources.Add(new ContentSourceFile { path = file.path, sha256 = file.sha256 });
            SourceFiles = copiedSources;
            TotalBytes = total;
        }
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

    public enum ContentDeliveryFailureCode
    {
        InvalidManifest, IntegrityMismatch, IdentityMismatch, TargetMismatch,
        CompatibilityMismatch, MissingFile, TransportFailure, Busy, LockUnavailable, BudgetUnsatisfied, InstallConflict, IoFailure
    }

    public sealed class ContentDeliveryException : InvalidOperationException
    {
        public ContentDeliveryException(ContentDeliveryFailureCode code, string message, Exception? inner = null,
            long requiredBytes = 0, long availableBytes = 0) : base(message, inner)
        {
            Code = code;
            RequiredBytes = requiredBytes;
            AvailableBytes = availableBytes;
        }
        public ContentDeliveryFailureCode Code { get; }
        public long RequiredBytes { get; }
        public long AvailableBytes { get; }
    }
}
