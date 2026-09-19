#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    public enum ContentDeleteStatus { Deleted, DeferredBusy, DeleteFailed, Protected }

    public sealed class ContentDeleteResult
    {
        internal ContentDeleteResult(ContentDeleteStatus status, string key, string reason)
        {
            Status = status;
            Key = key;
            Reason = reason;
        }
        public ContentDeleteStatus Status { get; }
        public string Key { get; }
        public string Reason { get; }
    }

    public sealed class ContentCacheSnapshot
    {
        internal ContentCacheSnapshot(IReadOnlyList<ContentCacheEntry> entries, IReadOnlyList<string> corruptPaths)
        {
            Entries = entries;
            CorruptPaths = corruptPaths;
        }
        public IReadOnlyList<ContentCacheEntry> Entries { get; }
        public IReadOnlyList<string> CorruptPaths { get; }
    }

    public sealed class ContentCacheEntry
    {
        internal ContentCacheEntry(string root, ContentInstallReceipt receipt, long bytes)
        {
            RevisionRoot = root;
            ContentSet = receipt.contentSet;
            Revision = receipt.revision;
            Target = receipt.target;
            ManifestSha256 = receipt.manifestSha256;
            Bytes = bytes;
            InstalledUtc = DateTime.Parse(receipt.installedUtc, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind);
        }
        public string RevisionRoot { get; }
        public string ContentSet { get; }
        public string Revision { get; }
        public string Target { get; }
        public string ManifestSha256 { get; }
        public long Bytes { get; }
        public DateTime InstalledUtc { get; }
    }

    public sealed class ContentCacheStore
    {
        private readonly string _root;
        private const string SupportedTarget = "StandaloneWindows64-Player";

        // store は root 値だけを保持する。各操作は transaction → revision lease の順で取り、
        // session 側に transaction を要求しないので、利用者の終了を待つ循環は作らない。
        public ContentCacheStore(string cacheRoot)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot) || !Path.IsPathRooted(cacheRoot))
                throw new ArgumentException("Cache root must be absolute.", nameof(cacheRoot));
            _root = Path.GetFullPath(cacheRoot).TrimEnd(Path.DirectorySeparatorChar);
            if (_root.Length < Path.GetPathRoot(cacheRoot)!.Length) _root = Path.GetPathRoot(cacheRoot)!;
            ContentDeliveryFiles.RejectReparseAncestors(Path.GetPathRoot(_root)!, _root);
            var drive = new DriveInfo(Path.GetPathRoot(_root)!);
            if (drive.DriveType == DriveType.Network || !string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.CompatibilityMismatch, "DIST cache requires local NTFS.");
        }

        public string RootPath => _root;

        public ContentInstallResult ValidateInstalled(string revisionRoot, string digest, string contentSet, string revision, string target)
            => Execute(() => ValidateInstalledUnsafe(revisionRoot, digest, contentSet, revision, target));

        private ContentInstallResult ValidateInstalledUnsafe(string revisionRoot, string digest, string contentSet, string revision, string target)
        {
            var expectedRoot = RevisionRoot(contentSet, revision);
            if (!SamePath(expectedRoot, revisionRoot))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InstallConflict, "Installed revision is outside this cache root.");
            var verified = InstalledRevisionVerifier.Verify(expectedRoot, digest, contentSet, revision, target);
            var manifest = InstalledRevisionVerifier.ReadValidatedManifestInsideLease(verified);
            return new ContentInstallResult(verified.RevisionRoot, verified.ContentPath, manifest, true);
        }

        internal string RevisionRoot(string set, string revision)
        {
            if (!ContentManifestValidation.Segment(set) || !ContentManifestValidation.Segment(revision))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Invalid cache identity segment.");
            return Path.Combine(_root, "installed", set, revision);
        }

        internal IDisposable AcquireTransaction()
        {
            try
            {
                var directory = Path.Combine(_root, "control");
                var path = Path.Combine(directory, "transaction.lock");
                ContentDeliveryFiles.RejectReparseAncestors(Path.GetPathRoot(_root)!, path);
                Directory.CreateDirectory(directory);
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex)
            {
                var code = (ex.HResult & 0xffff) is 32 or 33 ? ContentDeliveryFailureCode.Busy : ContentDeliveryFailureCode.LockUnavailable;
                throw new ContentDeliveryException(code, "Content cache lock is unavailable.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new ContentDeliveryException(ContentDeliveryFailureCode.LockUnavailable, "Content cache lock is unavailable.", ex);
            }
        }

        private T Execute<T>(Func<T> operation)
        {
            try { using (AcquireTransaction()) return operation(); }
            catch (ContentDeliveryException) { throw; }
            catch (ContentDirectoryException ex)
            {
                var busy = ex.Code is ContentDirectoryFailureCode.RevisionBusy or ContentDirectoryFailureCode.DeletionInProgress;
                throw new ContentDeliveryException(busy ? ContentDeliveryFailureCode.Busy : ContentDeliveryFailureCode.LockUnavailable,
                    "Revision lease is unavailable.", ex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or OverflowException)
            {
                throw new ContentDeliveryException(ContentDeliveryFailureCode.IoFailure, "Content cache operation failed.", ex);
            }
        }

        // snapshot は観測値であり削除許可ではない。候補確定後にも receipt と gate を再確認する。
        public ContentCacheSnapshot Inspect() => Execute(InspectUnsafe);

        internal ContentCacheSnapshot InspectUnsafe()
        {
            var entries = new List<ContentCacheEntry>();
            var corrupt = new List<string>();
            foreach (var setRoot in Directories(Path.Combine(_root, "installed")))
                foreach (var revisionRoot in Directories(setRoot))
                {
                    var receiptPath = Path.Combine(revisionRoot, "receipt.json");
                    try
                    {
                        var receipt = ContentDeliveryFiles.ReadJson<ContentInstallReceipt>(receiptPath);
                        ValidateReceipt(receipt, Path.GetFileName(setRoot), Path.GetFileName(revisionRoot));
                        entries.Add(new ContentCacheEntry(revisionRoot, receipt, MeasureTree(revisionRoot)));
                    }
                    catch (ContentDeliveryException) { corrupt.Add(receiptPath); }
                }
            return new ContentCacheSnapshot(entries, corrupt);
        }

        public void MarkKnownGood(ContentInstallResult result)
        {
            Execute(() =>
            {
                // 成功した load の判定自体は caller が所有する。ここでは他cacheの結果や
                // 検証後に削除・破損した結果を pin へ昇格させない。
                ValidateInstalledUnsafe(result.RevisionRoot, result.ManifestSha256, result.ContentSet, result.Revision, result.Target);
                var map = ReadPinFile();
                var items = map.items.Where(x => x.contentSet != result.ContentSet).ToList();
                items.Add(new KnownGood { contentSet = result.ContentSet, revision = result.Revision, digest = result.ManifestSha256 });
                map.items = items.ToArray();
                var path = Path.Combine(_root, "control", "known-good.json");
                var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    ContentDeliveryFiles.WriteJson(temporary, map);
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                return true;
            });
        }

        public IReadOnlyList<ContentDeleteResult> Evict(long budgetBytes)
            => Execute<IReadOnlyList<ContentDeleteResult>>(() => MakeRoom(0, budgetBytes, null));

        internal void EnsureCapacityUnsafe(long requestedBytes, long budgetBytes, string requestedSet, string requestedRevision)
            => MakeRoom(requestedBytes, budgetBytes, requestedSet + "\n" + requestedRevision);

        private List<ContentDeleteResult> MakeRoom(long requestedBytes, long budgetBytes, string? requestedKey)
        {
            if (requestedBytes < 0 || budgetBytes < 0)
                throw new ContentDeliveryException(ContentDeliveryFailureCode.BudgetUnsatisfied, "Disk budget cannot be negative.");
            var pins = new HashSet<string>(ReadPinFile().items.Select(x => x.contentSet + "\n" + x.revision), StringComparer.Ordinal);
            if (requestedKey != null) pins.Add(requestedKey);
            var results = new List<ContentDeleteResult>();
            CleanupStaging(results);
            CleanupTombstones(results);
            if (requestedKey != null)
                RejectRequestedTombstone(requestedKey);
            var snapshot = InspectUnsafe();
            if (snapshot.CorruptPaths.Count != 0)
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InstallConflict, "A corrupt receipt prevents safe eviction.");
            var used = MeasureManagedBytes();
            long required;
            try { required = checked(used + requestedBytes); }
            catch (OverflowException ex)
            {
                throw new ContentDeliveryException(ContentDeliveryFailureCode.BudgetUnsatisfied, "Cache byte total overflowed.", ex,
                    long.MaxValue, budgetBytes);
            }
            foreach (var entry in ContentEvictionPolicy.Select(snapshot.Entries, pins, required > budgetBytes ? required - budgetBytes : 0))
            {
                if (required <= budgetBytes) break;
                if (!ContentRevisionGate.TryAcquireDelete(entry.Revision, entry.Target, Path.Combine(entry.RevisionRoot, "content"),
                        out var lease, out var rejection))
                {
                    results.Add(Rejected(entry.Revision, rejection));
                    continue;
                }
                try
                {
                    DeleteRevision(entry);
                    results.Add(new ContentDeleteResult(ContentDeleteStatus.Deleted, entry.Revision, "Deleted."));
                }
                catch (Exception ex) { results.Add(new ContentDeleteResult(ContentDeleteStatus.DeleteFailed, entry.Revision, ex.Message)); }
                finally { lease!.Dispose(); }
                // 物理削除が部分失敗した場合にも、tombstone の残量を含む実 bytes で再計算する。
                required = checked(MeasureManagedBytes() + requestedBytes);
            }
            if (required > budgetBytes)
            {
                var failure = new ContentDeliveryException(ContentDeliveryFailureCode.BudgetUnsatisfied,
                    "Protected, busy, or undeletable content prevents the requested budget.", requiredBytes: required, availableBytes: budgetBytes);
                failure.Data["DeletionResults"] = results.ToArray();
                throw failure;
            }
            return results;
        }

        private void RejectRequestedTombstone(string requestedKey)
        {
            foreach (var directory in Directories(Path.Combine(_root, "tombstone")))
            {
                try
                {
                    var marker = ContentDeliveryFiles.ReadJson<Tombstone>(Path.Combine(directory, "marker.json"));
                    if (marker.version == 1 && marker.contentSet + "\n" + marker.revision == requestedKey)
                    {
                        // cleanup に失敗した旧 bytes が残る間は、同じ identity を別 digest で
                        // 再公開させない。cleanup 成功後は directory 自体が消えるため再試行できる。
                        throw new ContentDeliveryException(ContentDeliveryFailureCode.InstallConflict,
                            "A tombstone for the requested revision still requires cleanup.");
                    }
                }
                catch (ContentDeliveryException) { throw; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.InstallConflict,
                        "A remaining tombstone cannot be inspected safely.", ex);
                }
            }
        }

        private static ContentDeleteResult Rejected(string key, ContentDirectoryFailureCode rejection)
        {
            var status = rejection is ContentDirectoryFailureCode.RevisionBusy or ContentDirectoryFailureCode.DeletionInProgress
                or ContentDirectoryFailureCode.PathInUse ? ContentDeleteStatus.DeferredBusy : ContentDeleteStatus.DeleteFailed;
            return new ContentDeleteResult(status, key, rejection.ToString());
        }

        private long MeasureManagedBytes()
        {
            long total = 0;
            foreach (var name in new[] { "installed", "staging", "tombstone" })
                total = checked(total + MeasureTree(Path.Combine(_root, name)));
            return total;
        }

        private static long MeasureTree(string root)
        {
            ContentDeliveryFiles.RejectReparseAncestors(Path.GetPathRoot(Path.GetFullPath(root))!, root);
            if (!Directory.Exists(root)) return 0;
            long total = 0;
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count != 0)
                foreach (var path in Directory.GetFileSystemEntries(pending.Pop()))
                {
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new ContentDeliveryException(ContentDeliveryFailureCode.InstallConflict, "Cache contains a reparse point.");
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
                    else total = checked(total + new FileInfo(path).Length);
                }
            return total;
        }

        private static string[] Directories(string root)
        {
            ContentDeliveryFiles.RejectReparseAncestors(Path.GetPathRoot(Path.GetFullPath(root))!, root);
            if (!Directory.Exists(root)) return Array.Empty<string>();
            var directories = Directory.GetDirectories(root);
            foreach (var directory in directories)
                ContentDeliveryFiles.RejectReparseAncestors(root, directory);
            return directories;
        }

        private void CleanupStaging(List<ContentDeleteResult> results)
        {
            foreach (var directory in Directories(Path.Combine(_root, "staging")))
                try
                {
                    var marker = ContentDeliveryFiles.ReadJson<ContentStagingMarker>(Path.Combine(directory, "staging-owner.json"));
                    if (marker.version != 1 || !SamePath(marker.cacheRoot, _root) || !SamePath(marker.stagingRoot, directory)
                        || !ContentManifestValidation.Segment(marker.contentSet) || !ContentManifestValidation.Segment(marker.revision))
                        throw new InvalidDataException("Staging ownership marker is invalid.");
                    MeasureTree(directory);
                    Directory.Delete(directory, true);
                    results.Add(new ContentDeleteResult(ContentDeleteStatus.Deleted, marker.revision, "Staging cleanup completed."));
                }
                catch (Exception ex) { results.Add(new ContentDeleteResult(ContentDeleteStatus.DeleteFailed, directory, ex.Message)); }
        }

        private void CleanupTombstones(List<ContentDeleteResult> results)
        {
            // marker は元の installed identity/path を保持する。rename 後の path で lease を
            // 取ると、元 path への再登録を排除できないため、cleanup retry も元 path を使用する。
            foreach (var directory in Directories(Path.Combine(_root, "tombstone")))
                try
                {
                    var marker = ContentDeliveryFiles.ReadJson<Tombstone>(Path.Combine(directory, "marker.json"));
                    var original = RevisionRoot(marker.contentSet, marker.revision);
                    if (marker.version != 1 || !SamePath(marker.cacheRoot, _root) || !SamePath(marker.revisionRoot, original)
                        || !SamePath(marker.contentPath, Path.Combine(original, "content"))
                        || marker.target != SupportedTarget || !ContentManifestValidation.Hash(marker.digest))
                        throw new InvalidDataException("Tombstone ownership marker is invalid.");
                    if (!ContentRevisionGate.TryAcquireDelete(marker.revision, marker.target, marker.contentPath, out var lease, out var rejection))
                    {
                        results.Add(Rejected(marker.revision, rejection));
                        continue;
                    }
                    try
                    {
                        MeasureTree(directory);
                        DeleteTombstone(directory);
                        results.Add(new ContentDeleteResult(ContentDeleteStatus.Deleted, marker.revision, "Tombstone cleanup completed."));
                    }
                    finally { lease!.Dispose(); }
                }
                catch (Exception ex) { results.Add(new ContentDeleteResult(ContentDeleteStatus.DeleteFailed, directory, ex.Message)); }
        }

        private KnownGoodFile ReadPinFile()
        {
            var path = Path.Combine(_root, "control", "known-good.json");
            if (!File.Exists(path)) return new KnownGoodFile();
            var value = ContentDeliveryFiles.ReadJson<KnownGoodFile>(path);
            var sets = new HashSet<string>(StringComparer.Ordinal);
            if (value.version != 1 || value.items == null)
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InstallConflict, "Known-good metadata is invalid.");
            foreach (var item in value.items)
                if (item == null || !ContentManifestValidation.Segment(item.contentSet)
                    || !ContentManifestValidation.Segment(item.revision) || !ContentManifestValidation.Hash(item.digest)
                    || !sets.Add(item.contentSet))
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.InstallConflict, "Known-good metadata is semantically invalid.");
            return value;
        }

        private void DeleteRevision(ContentCacheEntry entry)
        {
            var receipt = ContentDeliveryFiles.ReadJson<ContentInstallReceipt>(Path.Combine(entry.RevisionRoot, "receipt.json"));
            ValidateReceipt(receipt, entry.ContentSet, entry.Revision);
            if (receipt.target != entry.Target || receipt.manifestSha256 != entry.ManifestSha256)
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InstallConflict, "Receipt changed after candidate selection.");
            MeasureTree(entry.RevisionRoot);
            var tombRoot = Path.Combine(_root, "tombstone", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tombRoot);
            var marker = new Tombstone
            {
                version = 1, cacheRoot = _root, revisionRoot = entry.RevisionRoot,
                contentPath = Path.Combine(entry.RevisionRoot, "content"), contentSet = entry.ContentSet,
                revision = entry.Revision, target = entry.Target, digest = entry.ManifestSha256,
            };
            ContentDeliveryFiles.WriteJson(Path.Combine(tombRoot, "marker.json"), marker);
            Directory.Move(entry.RevisionRoot, Path.Combine(tombRoot, "revision"));
            DeleteTombstone(tombRoot);
        }

        private static void DeleteTombstone(string directory)
        {
            // payload 削除に失敗しても marker を先に消さず、次回の所有確認を可能にする。
            var payload = Path.Combine(directory, "revision");
            if (Directory.Exists(payload)) Directory.Delete(payload, true);
            File.Delete(Path.Combine(directory, "marker.json"));
            Directory.Delete(directory);
        }

        private static bool SamePath(string left, string right)
            => string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

        private static void ValidateReceipt(ContentInstallReceipt receipt, string set, string revision)
        {
            if (receipt.version != 1 || receipt.contentSet != set || receipt.revision != revision
                || !ContentManifestValidation.Segment(set) || !ContentManifestValidation.Segment(revision)
                || receipt.target != SupportedTarget || !ContentManifestValidation.Hash(receipt.manifestSha256)
                || !DateTime.TryParse(receipt.installedUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out _))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Installed receipt is semantically invalid.");
        }

        [Serializable]
        private sealed class KnownGoodFile
        {
            public int version = 1;
            public KnownGood[] items = Array.Empty<KnownGood>();
        }

        [Serializable]
        private sealed class KnownGood
        {
            public string contentSet = "";
            public string revision = "";
            public string digest = "";
        }

        [Serializable]
        private sealed class Tombstone
        {
            public int version;
            public string cacheRoot = "";
            public string revisionRoot = "";
            public string contentPath = "";
            public string contentSet = "";
            public string revision = "";
            public string target = "";
            public string digest = "";
        }
    }
}
