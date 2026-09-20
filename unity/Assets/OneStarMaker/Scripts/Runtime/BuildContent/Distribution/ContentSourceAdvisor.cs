#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    public enum ContentSourceStatus { Complete, Missing, Changed }

    public sealed class ContentSourceAdvice
    {
        internal ContentSourceAdvice(ContentSourceStatus status, IReadOnlyList<string> paths)
        { Status = status; Paths = paths; }
        public ContentSourceStatus Status { get; }
        public IReadOnlyList<string> Paths { get; }
    }

    public static class ContentSourceAdvisor
    {
        public static ContentSourceAdvice InspectInstalled(ContentInstallResult installed, string projectRoot)
        {
            if (installed == null) throw new ArgumentNullException(nameof(installed));
            try
            {
                // InstallResult の固定 digest/identity を authority として再検証する。
                // 任意 path の manifest を自己承認させず、診断中の削除とも read lease で排他する。
                var verified = InstalledRevisionVerifier.Verify(installed.RevisionRoot,
                    installed.ManifestSha256, installed.ContentSet, installed.Revision, installed.Target);
                var manifest = InstalledRevisionVerifier.ReadValidatedManifestInsideLease(verified);
                return Inspect(projectRoot, manifest.SourceFiles);
            }
            catch (ContentDeliveryException) { throw; }
            catch (ContentDirectoryException ex)
            {
                var code = ex.Code is ContentDirectoryFailureCode.RevisionBusy or ContentDirectoryFailureCode.DeletionInProgress
                    ? ContentDeliveryFailureCode.Busy : ContentDeliveryFailureCode.LockUnavailable;
                throw new ContentDeliveryException(code, "Installed source metadata lock is unavailable.", ex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new ContentDeliveryException(ContentDeliveryFailureCode.IoFailure,
                    "Source closure diagnosis failed.", ex);
            }
        }

        public static ContentSourceAdvice Inspect(string projectRoot, IEnumerable<ContentSourceFile> files)
        {
            try { return InspectCore(projectRoot, files); }
            catch (ContentDeliveryException) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new ContentDeliveryException(ContentDeliveryFailureCode.IoFailure, "Source closure diagnosis failed.", ex);
            }
        }

        private static ContentSourceAdvice InspectCore(string projectRoot, IEnumerable<ContentSourceFile> files)
        {
            var missing = new List<string>();
            var changed = new List<string>();
            foreach (var file in files)
            {
                if (file == null || string.IsNullOrEmpty(file.path)
                    || !(file.path.StartsWith("Assets/", StringComparison.Ordinal) || file.path.StartsWith("Packages/", StringComparison.Ordinal))
                    || !ContentManifestValidation.Hash(file.sha256))
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Source metadata is invalid.");
                var path = ContentDeliveryFiles.Resolve(projectRoot, file.path);
                ContentDeliveryFiles.RejectReparseAncestors(projectRoot, path);
                if (!File.Exists(path)) missing.Add(file.path);
                else if (ContentDeliveryFiles.Sha256(path) != file.sha256) changed.Add(file.path);
            }
            if (missing.Count > 0) return new ContentSourceAdvice(ContentSourceStatus.Missing, missing);
            return changed.Count > 0
                ? new ContentSourceAdvice(ContentSourceStatus.Changed, changed)
                : new ContentSourceAdvice(ContentSourceStatus.Complete, Array.Empty<string>());
        }
    }
}
