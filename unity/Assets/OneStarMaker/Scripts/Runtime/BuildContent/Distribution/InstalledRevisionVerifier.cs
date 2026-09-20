#nullable enable

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    internal sealed class VerifiedInstalledRevision
    {
        internal VerifiedInstalledRevision(string root, string content, string identity, string set, string target, string digest)
        {
            RevisionRoot = root;
            ContentPath = content;
            Identity = identity;
            ContentSet = set;
            Target = target;
            ManifestSha256 = digest;
        }
        internal string RevisionRoot { get; }
        internal string ContentPath { get; }
        internal string Identity { get; }
        internal string ContentSet { get; }
        internal string Target { get; }
        internal string ManifestSha256 { get; }
    }

    internal static class InstalledRevisionVerifier
    {
        internal static VerifiedInstalledRevision Verify(string revisionRoot, string expectedDigest,
            string expectedSet, string expectedRevision, string expectedTarget)
            => NormalizeFailures(() =>
            {
                var root = Path.GetFullPath(revisionRoot);
                var content = Path.Combine(root, "content");
                // 返す値は利用権ではない。検証中だけ read を所有し、登録側は Reserve 後に再検証する。
                using (ContentRevisionGate.AcquireRead(expectedRevision, expectedTarget, content))
                    return VerifyInsideLease(root, expectedDigest, expectedSet, expectedRevision, expectedTarget);
            });

        internal static VerifiedInstalledRevision VerifyInsideLease(string root, string expectedDigest,
            string expectedSet, string expectedRevision, string expectedTarget)
            => NormalizeFailures(() =>
            {
                ReadVerifiedManifest(root, expectedDigest, expectedSet, expectedRevision, expectedTarget);
                return new VerifiedInstalledRevision(root, Path.Combine(root, "content"), expectedRevision,
                    expectedSet, expectedTarget, expectedDigest);
            });

        internal static ValidatedContentManifest ReadValidatedManifestInsideLease(VerifiedInstalledRevision value)
            => NormalizeFailures(() =>
            {
                using (ContentRevisionGate.AcquireRead(value.Identity, value.Target, value.ContentPath))
                    return ReadVerifiedManifest(value.RevisionRoot, value.ManifestSha256, value.ContentSet, value.Identity, value.Target);
            });

        private static ValidatedContentManifest ReadVerifiedManifest(string root, string digest,
            string set, string revision, string target)
        {
            if (!IsPublishedRoot(root, set, revision))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InstallConflict,
                    "Only an installed revision root can be registered.");
            ContentLocalNtfs.Require(root, "Installed content requires local NTFS.");
            ContentDeliveryFiles.RejectReparseAncestors(Path.GetPathRoot(root)!, root);
            var receipt = ContentDeliveryFiles.ReadJson<ContentInstallReceipt>(Path.Combine(root, "receipt.json"));
            if (receipt.version != 1 || receipt.manifestSha256 != digest || receipt.contentSet != set
                || receipt.revision != revision || receipt.target != target
                || !DateTime.TryParse(receipt.installedUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out _))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.IntegrityMismatch,
                    "Installed receipt does not match the requested revision.");

            var path = Path.Combine(root, "transport.json");
            ContentDeliveryFiles.RejectReparseAncestors(Path.GetPathRoot(root)!, path);
            if (new FileInfo(path).Length > ContentManifestValidation.MaxManifestBytes)
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Installed manifest exceeds its byte limit.");
            var bytes = File.ReadAllBytes(path);
            if (ContentDeliveryFiles.Sha256(bytes) != digest)
                throw new ContentDeliveryException(ContentDeliveryFailureCode.IntegrityMismatch, "Installed manifest digest differs.");
            var json = new UTF8Encoding(false, true).GetString(bytes);
            ContentManifestValidation.ValidateRequiredFields(json);
            var dto = JsonUtility.FromJson<ContentTransportManifest>(json)
                ?? throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Installed manifest is invalid.");
            var manifest = ContentManifestValidation.Validate(dto, digest, new ContentInstallRequest(digest, set, revision,
                target, ContentManifestValidation.SupportedUnityVersion, ContentManifestValidation.SupportedRootSchemaVersion, long.MaxValue));
            ContentDeliveryFiles.VerifyTree(Path.Combine(root, "content"), manifest);
            return manifest;
        }

        private static T NormalizeFailures<T>(Func<T> operation)
        {
            try { return operation(); }
            catch (ContentDeliveryException) { throw; }
            catch (ContentDirectoryException ex)
            {
                var busy = ex.Code is ContentDirectoryFailureCode.RevisionBusy or ContentDirectoryFailureCode.DeletionInProgress;
                throw new ContentDeliveryException(busy ? ContentDeliveryFailureCode.Busy : ContentDeliveryFailureCode.LockUnavailable,
                    "Installed revision lock is unavailable.", ex);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Installed metadata is invalid.", ex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new ContentDeliveryException(ContentDeliveryFailureCode.IoFailure, "Installed revision verification failed.", ex);
            }
        }

        internal static bool LooksManaged(string contentPath)
        {
            var parent = Directory.GetParent(Path.GetFullPath(contentPath));
            return parent != null && (File.Exists(Path.Combine(parent.FullName, "receipt.json"))
                || File.Exists(Path.Combine(parent.FullName, "transport.json"))
                || string.Equals(parent.Parent?.Parent?.Name, "installed", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsPublishedRoot(string root, string set, string revision)
        {
            var directory = new DirectoryInfo(root);
            return ContentManifestValidation.Segment(set) && ContentManifestValidation.Segment(revision)
                && string.Equals(directory.Name, revision, StringComparison.OrdinalIgnoreCase)
                && string.Equals(directory.Parent?.Name, set, StringComparison.OrdinalIgnoreCase)
                && string.Equals(directory.Parent?.Parent?.Name, "installed", StringComparison.OrdinalIgnoreCase);
        }
    }
}
