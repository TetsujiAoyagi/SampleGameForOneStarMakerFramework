#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    /// <summary>一回の取得、検証、公開を所有し、成功時だけ immutable revision を可視化する。</summary>
    public sealed class ContentInstaller
    {
        private readonly ContentCacheStore _store;
        public ContentInstaller(ContentCacheStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

        public async Task<ContentInstallResult> InstallAsync(ContentInstallRequest request,
            IContentArtifactSource source, CancellationToken cancellationToken)
        {
            try { return await InstallCore(request, source, cancellationToken); }
            catch (OperationCanceledException) { throw; }
            catch (ContentDeliveryException) { throw; }
            catch (ContentDirectoryException ex)
            {
                var busy = ex.Code is ContentDirectoryFailureCode.RevisionBusy or ContentDirectoryFailureCode.DeletionInProgress;
                throw new ContentDeliveryException(busy ? ContentDeliveryFailureCode.Busy : ContentDeliveryFailureCode.LockUnavailable,
                    "Install revision lease is unavailable.", ex);
            }
            catch (OverflowException ex)
            {
                throw new ContentDeliveryException(ContentDeliveryFailureCode.BudgetUnsatisfied, "Install size overflowed.", ex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new ContentDeliveryException(ContentDeliveryFailureCode.IoFailure, "Content install I/O failed.", ex);
            }
        }

        private async Task<ContentInstallResult> InstallCore(ContentInstallRequest request,
            IContentArtifactSource source, CancellationToken cancellationToken)
        {
            // admission から rename まで直列化し、同じ空き容量の二重予約を防ぐ。
            using (_store.AcquireTransaction())
            {
                var manifestBytes = await ReadBounded(source, "transport.json",
                    ContentManifestValidation.MaxManifestBytes, cancellationToken);
                var digest = ContentDeliveryFiles.Sha256(manifestBytes);
                ContentTransportManifest dto;
                try
                {
                    var json = new UTF8Encoding(false, true).GetString(manifestBytes);
                    ContentManifestValidation.ValidateRequiredFields(json);
                    dto = JsonUtility.FromJson<ContentTransportManifest>(json)
                          ?? throw new FormatException();
                }
                catch (Exception ex)
                {
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest,
                        "Transport manifest JSON is invalid.", ex);
                }
                var manifest = ContentManifestValidation.Validate(dto, digest, request);
                var final = _store.RevisionRoot(manifest.ContentSet, manifest.Revision);
                if (Directory.Exists(final))
                {
                    // final は上書きせず、同一 digest の完全再検証だけを再利用とする。
                    using (ContentRevisionGate.AcquireRead(manifest.Revision, manifest.Target,
                               Path.Combine(final, "content")))
                    {
                        InstalledRevisionVerifier.Verify(final, digest, manifest.ContentSet,
                            manifest.Revision, manifest.Target);
                        return new ContentInstallResult(final, Path.Combine(final, "content"), manifest, true);
                    }
                }
                var receipt = new ContentInstallReceipt
                {
                    manifestSha256 = digest,
                    contentSet = manifest.ContentSet,
                    revision = manifest.Revision,
                    target = manifest.Target,
                    installedUtc = DateTime.UtcNow.ToString("O")
                };
                var staging = Path.Combine(_store.RootPath, "staging", Guid.NewGuid().ToString("N"));
                var marker = new ContentStagingMarker
                {
                    cacheRoot = _store.RootPath, stagingRoot = staging,
                    contentSet = manifest.ContentSet, revision = manifest.Revision,
                };
                var metadataBytes = checked((long)manifestBytes.Length
                    + Encoding.UTF8.GetByteCount(JsonUtility.ToJson(receipt, true))
                    + Encoding.UTF8.GetByteCount(JsonUtility.ToJson(marker, true)));
                var reservedBytes = checked(manifest.TotalBytes + metadataBytes);
                _store.EnsureCapacityUnsafe(reservedBytes, request.DiskBudgetBytes,
                    manifest.ContentSet, manifest.Revision);
                try
                {
                    var content = Path.Combine(staging, "content");
                    Directory.CreateDirectory(content);
                    ContentDeliveryFiles.WriteJson(Path.Combine(staging, "staging-owner.json"), marker);
                    File.WriteAllBytes(Path.Combine(staging, "transport.json"), manifestBytes);
                    foreach (var file in manifest.Files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var path = ContentDeliveryFiles.Resolve(content, file.path);
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        using var input = await source.OpenReadAsync("content/" + file.path, cancellationToken);
                        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        await CopyExact(input, output, file.size, cancellationToken);
                    }
                    ContentDeliveryFiles.VerifyTree(content, manifest);
                    ContentDeliveryFiles.WriteJson(Path.Combine(staging, "receipt.json"), receipt);
                    Directory.CreateDirectory(Path.GetDirectoryName(final)!);
                    // 同じ volume の rename だけを公開点にし、final の部分更新を作らない。
                    Directory.Move(staging, final);
                    // ownership marker は opaque な install metadata として残す。
                    // 公開後の cleanup failure で成功した immutable install を失敗へ変えない。
                    return new ContentInstallResult(final, Path.Combine(final, "content"), manifest, false);
                }
                catch (OperationCanceledException ex) { CleanupAfterFailure(staging, ex); throw; }
                catch (ContentDeliveryException ex) { CleanupAfterFailure(staging, ex); throw; }
                catch (Exception ex)
                {
                    try { CleanupAfterFailure(staging, ex); }
                    catch (ContentDeliveryException wrapped) { throw wrapped; }
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.IoFailure,
                        "Content install failed before publication.", ex);
                }
            }
        }

        private static async Task<byte[]> ReadBounded(IContentArtifactSource source, string path,
            int limit, CancellationToken cancellationToken)
        {
            using var stream = await source.OpenReadAsync(path, cancellationToken);
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (read == 0) break;
                if (memory.Length + read > limit)
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest,
                        "Manifest exceeds its byte limit.");
                memory.Write(buffer, 0, read);
            }
            return memory.ToArray();
        }

        private static async Task CopyExact(Stream input, Stream output, long expected,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[81920]; long total = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (read == 0) break;
                total += read;
                if (total > expected) throw new ContentDeliveryException(
                    ContentDeliveryFailureCode.IntegrityMismatch, "Artifact exceeded its declared size.");
                await output.WriteAsync(buffer, 0, read, cancellationToken);
            }
            if (total != expected) throw new ContentDeliveryException(
                ContentDeliveryFailureCode.IntegrityMismatch, "Artifact was shorter than declared.");
        }

        private static void CleanupAfterFailure(string staging, Exception primary)
        {
            if (!Directory.Exists(staging)) return;
            try { Directory.Delete(staging, true); }
            catch (Exception cleanup)
            {
                // cancellation は標準例外のまま返し、cleanup 診断も Data に保持する。
                if (primary is OperationCanceledException)
                { primary.Data["ContentDeliveryCleanupFailure"] = cleanup; return; }
                var code = primary is ContentDeliveryException delivery
                    ? delivery.Code : ContentDeliveryFailureCode.IoFailure;
                throw new ContentDeliveryException(code, primary.Message,
                    new AggregateException(primary, cleanup));
            }
        }
    }
}
