#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;
namespace OneStarMaker.Tests.BuildContent.Distribution
{
    public sealed class ContentInstallerTests
    {
        [Test]
        public async Task Install_HashMismatch_DoesNotPublishRevision()
        { var root = Path.Combine(Path.GetTempPath(), "osm-dist", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); try { var bytes = Encoding.UTF8.GetBytes("actual"); var manifest = new ContentTransportManifest { version = 1, product = "OneStarMaker", contentSet = "set", revision = "rev", target = "StandaloneWindows64-Player", unityVersion = "6000.6.0f1", rootSchemaVersion = 2, playerConfigSchemaVersion = 1, files = new[] { new ContentTransportFile { path = "payload.bin", size = bytes.Length, sha256 = new string('0', 64) } } }; var manifestBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest)); var digest = ContentDeliveryFiles.Sha256(manifestBytes); using var source = new MemorySource(new Dictionary<string, byte[]> { { "transport.json", manifestBytes }, { "content/payload.bin", bytes } }); var request = new ContentInstallRequest(digest, "set", "rev", "StandaloneWindows64-Player", "6000.6.0f1", 2, 1024); Exception? failure = null; try { await new ContentInstaller(new ContentCacheStore(root)).InstallAsync(request, source, CancellationToken.None); } catch (Exception exception) { failure = exception; } Assert.That(failure, Is.TypeOf<ContentDeliveryException>()); var ex = (ContentDeliveryException)failure!; Assert.That(ex.Code, Is.EqualTo(ContentDeliveryFailureCode.IntegrityMismatch)); Assert.That(Directory.Exists(Path.Combine(root, "installed", "set", "rev")), Is.False); } finally { Directory.Delete(root, true); } }
        [Test]
        public async Task Install_ExistingRevisionDifferentDigest_LeavesFinalUnchanged()
        {
            // 同じ revision に別 digest を載せようとしても final は上書きせず、
            // 既存 receipt の再検証が IntegrityMismatch で閉じる。
            var root = Path.Combine(Path.GetTempPath(), "osm-dist", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var payload = Encoding.UTF8.GetBytes("good");
                var first = await InstallRevision(root, payload, "rev");
                var installed = Path.Combine(root, "installed", "set", "rev", "content", "payload.bin");
                var original = File.ReadAllBytes(installed);
                Assert.That(first.AlreadyInstalled, Is.False);

                var mutated = Encoding.UTF8.GetBytes("evil");
                var error = await CaptureInstallFailure(root, mutated, "rev");
                Assert.That(error.Code, Is.EqualTo(ContentDeliveryFailureCode.IntegrityMismatch));
                Assert.That(File.ReadAllBytes(installed), Is.EqualTo(original));
                var receipt = File.ReadAllText(Path.Combine(root, "installed", "set", "rev", "receipt.json"));
                Assert.That(receipt, Does.Contain(first.ManifestSha256));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public async Task Install_ExistingRevisionSameDigest_IgnoresMutatedSourceBytes()
        {
            // pin 済み digest の再利用は source の content 差し替えを読まず、installed bytes を残す。
            var root = Path.Combine(Path.GetTempPath(), "osm-dist", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var payload = Encoding.UTF8.GetBytes("good");
                var first = await InstallRevision(root, payload, "rev");
                var installed = Path.Combine(root, "installed", "set", "rev", "content", "payload.bin");
                var original = File.ReadAllBytes(installed);

                var originalTransport = File.ReadAllBytes(Path.Combine(root, "installed", "set", "rev", "transport.json"));
                using var mutated = new MemorySource(new Dictionary<string, byte[]>
                {
                    { "transport.json", originalTransport },
                    { "content/payload.bin", Encoding.UTF8.GetBytes("evil") },
                });
                var request = new ContentInstallRequest(first.ManifestSha256, "set", "rev",
                    "StandaloneWindows64-Player", "6000.6.0f1", 2, 1024 * 1024);
                var second = await new ContentInstaller(new ContentCacheStore(root))
                    .InstallAsync(request, mutated, CancellationToken.None);

                Assert.That(second.AlreadyInstalled, Is.True);
                Assert.That(File.ReadAllBytes(installed), Is.EqualTo(original));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static async Task<ContentInstallResult> InstallRevision(string root, byte[] payload, string revision)
        {
            var manifest = CreateManifest(revision, payload);
            var manifestBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest));
            var digest = ContentDeliveryFiles.Sha256(manifestBytes);
            using var source = new MemorySource(new Dictionary<string, byte[]>
            {
                { "transport.json", manifestBytes },
                { "content/payload.bin", payload },
            });
            var request = new ContentInstallRequest(digest, "set", revision,
                "StandaloneWindows64-Player", "6000.6.0f1", 2, 1024 * 1024);
            return await new ContentInstaller(new ContentCacheStore(root))
                .InstallAsync(request, source, CancellationToken.None);
        }

        private static async Task<ContentDeliveryException> CaptureInstallFailure(
            string root, byte[] payload, string revision)
        {
            var manifest = CreateManifest(revision, payload);
            var manifestBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest));
            var digest = ContentDeliveryFiles.Sha256(manifestBytes);
            using var source = new MemorySource(new Dictionary<string, byte[]>
            {
                { "transport.json", manifestBytes },
                { "content/payload.bin", payload },
            });
            var request = new ContentInstallRequest(digest, "set", revision,
                "StandaloneWindows64-Player", "6000.6.0f1", 2, 1024 * 1024);
            try
            {
                await new ContentInstaller(new ContentCacheStore(root))
                    .InstallAsync(request, source, CancellationToken.None);
            }
            catch (ContentDeliveryException exception)
            {
                return exception;
            }

            throw new AssertionException("expected ContentDeliveryException");
        }

        private static ContentTransportManifest CreateManifest(string revision, byte[] payload)
        {
            return new ContentTransportManifest
            {
                version = 1,
                product = "OneStarMaker",
                contentSet = "set",
                revision = revision,
                target = "StandaloneWindows64-Player",
                unityVersion = "6000.6.0f1",
                rootSchemaVersion = 2,
                playerConfigSchemaVersion = 1,
                files = new[]
                {
                    new ContentTransportFile
                    {
                        path = "payload.bin",
                        size = payload.Length,
                        sha256 = ContentDeliveryFiles.Sha256(payload),
                    },
                },
            };
        }

        private sealed class MemorySource : IContentArtifactSource
        {
            private readonly IReadOnlyDictionary<string, byte[]> _values;
            internal MemorySource(IReadOnlyDictionary<string, byte[]> values) => _values = values;
            public Task<Stream> OpenReadAsync(string path, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<Stream>(new MemoryStream(_values[path], false));
            }
            public void Dispose() { }
        }
    }
}
