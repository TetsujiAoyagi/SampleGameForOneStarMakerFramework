#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Tests.BuildContent.Distribution
{
    public sealed class ContentDeliveryFailureMatrixTests
    {
        private const string Target = "StandaloneWindows64-Player";
        private const string PlaceholderHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [TestCase("set", "other", Target, "6000.6.0f1", ContentDeliveryFailureCode.IdentityMismatch)]
        [TestCase("set", "revision", "OtherTarget", "6000.6.0f1", ContentDeliveryFailureCode.TargetMismatch)]
        [TestCase("set", "revision", Target, "other", ContentDeliveryFailureCode.CompatibilityMismatch)]
        public void ManifestMismatch_ReturnsStructuredFailure(
            string set, string revision, string target, string unity, ContentDeliveryFailureCode expected)
        {
            var request = new ContentInstallRequest(PlaceholderHash, set, revision, target, unity, 2, 100);
            var error = Assert.Throws<ContentDeliveryException>(
                () => ContentManifestValidation.Validate(CreateManifest(), PlaceholderHash, request));
            Assert.That(error!.Code, Is.EqualTo(expected));
        }

        [Test]
        public void CacheTransaction_ConcurrentInspectorReturnsBusy()
        {
            var root = CreateTemporaryDirectory();
            try
            {
                var store = new ContentCacheStore(root);
                using (store.AcquireTransaction())
                {
                    var error = Assert.Throws<ContentDeliveryException>(() => store.Inspect());
                    Assert.That(error!.Code, Is.EqualTo(ContentDeliveryFailureCode.Busy));
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void CorruptReceipt_StopsEvictionInsteadOfBecomingADeleteCandidate()
        {
            var root = CreateTemporaryDirectory();
            try
            {
                var revisionRoot = Path.Combine(root, "installed", "set", "revision");
                Directory.CreateDirectory(revisionRoot);
                File.WriteAllText(Path.Combine(revisionRoot, "receipt.json"), "{");
                var error = Assert.Throws<ContentDeliveryException>(() => new ContentCacheStore(root).Evict(0));
                Assert.That(error!.Code, Is.EqualTo(ContentDeliveryFailureCode.InstallConflict));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void ReadLease_BlocksDeleteAndReleaseAllowsRetry()
        {
            var parent = CreateTemporaryDirectory();
            var contentPath = Path.Combine(parent, "content");
            Directory.CreateDirectory(contentPath);
            try
            {
                using (ContentRevisionGate.AcquireRead("revision", Target, contentPath))
                {
                    Assert.That(
                        ContentRevisionGate.TryAcquireDelete("revision", Target, contentPath, out _, out var rejection),
                        Is.False);
                    Assert.That(rejection, Is.EqualTo(ContentDirectoryFailureCode.RevisionBusy));
                }

                Assert.That(
                    ContentRevisionGate.TryAcquireDelete("revision", Target, contentPath, out var lease, out _),
                    Is.True);
                lease!.Dispose();
            }
            finally
            {
                Directory.Delete(parent, true);
            }
        }

        [Test]
        public void DeleteFailure_LeavesOwnedTombstoneAndNextEvictionRetriesCleanup()
        {
            var root = CreateTemporaryDirectory();
            try
            {
                var revisionRoot = Path.Combine(root, "installed", "set", "revision");
                var contentPath = Path.Combine(revisionRoot, "content");
                Directory.CreateDirectory(contentPath);
                var heldPath = Path.Combine(contentPath, "held.bin");
                File.WriteAllText(heldPath, "bytes");
                ContentDeliveryFiles.WriteJson(
                    Path.Combine(revisionRoot, "receipt.json"),
                    new ContentInstallReceipt
                    {
                        contentSet = "set",
                        revision = "revision",
                        target = Target,
                        manifestSha256 = PlaceholderHash,
                        installedUtc = DateTime.UtcNow.ToString("O"),
                    });
                var store = new ContentCacheStore(root);

                using (new FileStream(heldPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var failure = Assert.Throws<ContentDeliveryException>(() => store.Evict(0));
                    Assert.That(failure!.Code, Is.EqualTo(ContentDeliveryFailureCode.BudgetUnsatisfied));

                    using (store.AcquireTransaction())
                    {
                        var conflict = Assert.Throws<ContentDeliveryException>(() =>
                            store.EnsureCapacityUnsafe(0, long.MaxValue, "set", "revision"));
                        Assert.That(conflict!.Code, Is.EqualTo(ContentDeliveryFailureCode.InstallConflict));
                    }
                }

                var retry = store.Evict(0);
                Assert.That(retry, Has.Some.Property("Status").EqualTo(ContentDeleteStatus.Deleted));
                Assert.That(Directory.GetDirectories(Path.Combine(root, "tombstone")), Is.Empty);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [TestCase("wrong-cache", "digest", "set")]
        [TestCase("cache", "wrong-digest", "set")]
        [TestCase("cache", "digest", "wrong-set")]
        public void ValidateInstalled_WrongAuthorityInput_IsRejected(
            string cacheChoice, string digestChoice, string setChoice)
        {
            var parent = CreateTemporaryDirectory();
            try
            {
                var cacheRoot = Path.Combine(parent, "cache");
                var fixture = CreateInstalled(cacheRoot, Array.Empty<ContentSourceFile>());
                var selectedRoot = cacheChoice == "cache" ? cacheRoot : Path.Combine(parent, "other-cache");
                var digest = digestChoice == "digest" ? fixture.Digest : new string('f', 64);
                var contentSet = setChoice == "set" ? "set" : "other";

                var error = Assert.Throws<ContentDeliveryException>(
                    () => new ContentCacheStore(selectedRoot).ValidateInstalled(
                        fixture.Root, digest, contentSet, "revision", Target));

                Assert.That(
                    error!.Code,
                    Is.EqualTo(ContentDeliveryFailureCode.InstallConflict)
                        .Or.EqualTo(ContentDeliveryFailureCode.IntegrityMismatch));
            }
            finally
            {
                Directory.Delete(parent, true);
            }
        }

        [Test]
        public async Task InstallFailure_WithStagingCleanupFailure_PreservesPrimaryAndReportsPath()
        {
            var cacheRoot = CreateTemporaryDirectory();
            var payload = Encoding.UTF8.GetBytes("actual");
            var manifest = CreateManifest();
            manifest.files[0].size = payload.Length;
            manifest.files[0].sha256 = new string('0', 64);
            var manifestBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest));
            var digest = Hash(manifestBytes);
            using var source = new CleanupBlockingSource(cacheRoot, manifestBytes, payload);
            try
            {
                var request = new ContentInstallRequest(digest, "set", "revision", Target,
                    "6000.6.0f1", 2, 1024 * 1024);

                var error = await CaptureException<ContentDeliveryException>(async () =>
                    await new ContentInstaller(new ContentCacheStore(cacheRoot)).InstallAsync(
                        request, source, CancellationToken.None));

                Assert.That(error!.Code, Is.EqualTo(ContentDeliveryFailureCode.IntegrityMismatch));
                Assert.That(error.Data["ContentDeliveryCleanupFailure"], Is.TypeOf<IOException>());
                var staging = error.Data["ContentDeliveryStagingPath"] as string;
                Assert.That(staging, Is.Not.Null.And.Not.Empty);
                Assert.That(Directory.Exists(staging!), Is.True);
            }
            finally
            {
                source.Dispose();
                if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
            }
        }

        [Test]
        public void ValidateInstalled_ResultDoesNotAuthorizeRegistrationAfterDeletion()
        {
            var cacheRoot = CreateTemporaryDirectory();
            try
            {
                var fixture = CreateInstalled(cacheRoot, Array.Empty<ContentSourceFile>());
                var installed = new ContentCacheStore(cacheRoot).ValidateInstalled(
                    fixture.Root, fixture.Digest, "set", "revision", Target);
                Directory.Delete(installed.RevisionRoot, true);

                var error = Assert.Throws<ContentDirectoryException>(
                    () => ContentDirectorySession.Register(installed.ContentPath, "revision", Target, null!));
                Assert.That(error!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));
            }
            finally
            {
                if (Directory.Exists(cacheRoot))
                {
                    Directory.Delete(cacheRoot, true);
                }
            }
        }

        [Test]
        public void InspectInstalled_ReportsMissingAndChangedWithoutInvalidatingInstall()
        {
            var cacheRoot = CreateTemporaryDirectory();
            var projectRoot = CreateTemporaryDirectory();
            try
            {
                Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
                File.WriteAllText(Path.Combine(projectRoot, "Assets", "changed.txt"), "new");
                var sources = new[]
                {
                    new ContentSourceFile
                    {
                        path = "Assets/missing.txt",
                        sha256 = Hash(Encoding.UTF8.GetBytes("missing")),
                    },
                    new ContentSourceFile
                    {
                        path = "Assets/changed.txt",
                        sha256 = Hash(Encoding.UTF8.GetBytes("old")),
                    },
                };
                var fixture = CreateInstalled(cacheRoot, sources);
                var installed = new ContentCacheStore(cacheRoot).ValidateInstalled(
                    fixture.Root, fixture.Digest, "set", "revision", Target);

                var advice = ContentSourceAdvisor.InspectInstalled(installed, projectRoot);
                Assert.That(advice.Status, Is.EqualTo(ContentSourceStatus.Missing));
                Assert.That(advice.Paths, Does.Contain("Assets/missing.txt"));
                Assert.That(Directory.Exists(installed.RevisionRoot), Is.True, "source 診断は成果物を破棄しない");

                File.WriteAllText(Path.Combine(projectRoot, "Assets", "missing.txt"), "missing");
                advice = ContentSourceAdvisor.InspectInstalled(installed, projectRoot);
                Assert.That(advice.Status, Is.EqualTo(ContentSourceStatus.Changed));
                Assert.That(advice.Paths, Does.Contain("Assets/changed.txt"));
            }
            finally
            {
                Directory.Delete(cacheRoot, true);
                Directory.Delete(projectRoot, true);
            }
        }

        [Test]
        public void InspectInstalled_TamperedSourceFiles_IsRejectedBeforeSourceInspection()
        {
            var cacheRoot = CreateTemporaryDirectory();
            var projectRoot = CreateTemporaryDirectory();
            try
            {
                var fixture = CreateInstalled(
                    cacheRoot,
                    new[] { new ContentSourceFile { path = "Assets/source.txt", sha256 = PlaceholderHash } });
                var installed = new ContentCacheStore(cacheRoot).ValidateInstalled(
                    fixture.Root, fixture.Digest, "set", "revision", Target);
                var manifest = CreateManifest();
                manifest.sourceFiles = new[]
                {
                    new ContentSourceFile { path = "../outside.txt", sha256 = PlaceholderHash },
                };
                File.WriteAllText(Path.Combine(fixture.Root, "transport.json"), JsonUtility.ToJson(manifest));

                var error = Assert.Throws<ContentDeliveryException>(
                    () => ContentSourceAdvisor.InspectInstalled(installed, projectRoot));
                Assert.That(error!.Code, Is.EqualTo(ContentDeliveryFailureCode.IntegrityMismatch));
            }
            finally
            {
                Directory.Delete(cacheRoot, true);
                Directory.Delete(projectRoot, true);
            }
        }

        [Test]
        public void InspectInstalled_InvalidSourceFilesCannotProduceInstalledAuthority()
        {
            var cacheRoot = CreateTemporaryDirectory();
            try
            {
                var fixture = CreateInstalled(
                    cacheRoot,
                    new[] { new ContentSourceFile { path = "../outside.txt", sha256 = PlaceholderHash } });

                var error = Assert.Throws<ContentDeliveryException>(
                    () => new ContentCacheStore(cacheRoot).ValidateInstalled(
                        fixture.Root, fixture.Digest, "set", "revision", Target));

                Assert.That(error!.Code, Is.EqualTo(ContentDeliveryFailureCode.InvalidManifest));
            }
            finally
            {
                Directory.Delete(cacheRoot, true);
            }
        }

        [Test]
        public void VerificationToReservationGap_DeleteCanWinButNativeRegistrationDoesNotStart()
        {
            var root = CreateTemporaryDirectory();
            try
            {
                var fixture = CreateInstalled(root, Array.Empty<ContentSourceFile>());
                var verified = InstalledRevisionVerifier.Verify(
                    fixture.Root, fixture.Digest, "set", "revision", Target);
                Assert.That(
                    ContentRevisionGate.TryAcquireDelete("revision", Target, verified.ContentPath, out var deletion, out _),
                    Is.True);
                Directory.Delete(fixture.Root, true);
                deletion!.Dispose();

                Assert.Throws<ContentDeliveryException>(() => ContentDirectorySession.RegisterVerified(verified, null!));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static InstalledFixture CreateInstalled(string cacheRoot, ContentSourceFile[] sources)
        {
            var revisionRoot = Path.Combine(cacheRoot, "installed", "set", "revision");
            var contentPath = Path.Combine(revisionRoot, "content");
            Directory.CreateDirectory(contentPath);
            var payload = new byte[] { 1 };
            File.WriteAllBytes(Path.Combine(contentPath, "file"), payload);
            var manifest = CreateManifest();
            manifest.files[0].sha256 = Hash(payload);
            manifest.sourceFiles = sources;
            var manifestBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest));
            var digest = Hash(manifestBytes);
            File.WriteAllBytes(Path.Combine(revisionRoot, "transport.json"), manifestBytes);
            ContentDeliveryFiles.WriteJson(
                Path.Combine(revisionRoot, "receipt.json"),
                new ContentInstallReceipt
                {
                    version = 1,
                    manifestSha256 = digest,
                    contentSet = "set",
                    revision = "revision",
                    target = Target,
                    installedUtc = DateTime.UtcNow.ToString("O"),
                });
            return new InstalledFixture(revisionRoot, digest);
        }

        private static ContentTransportManifest CreateManifest()
        {
            return new ContentTransportManifest
            {
                version = 1,
                product = "OneStarMaker",
                contentSet = "set",
                revision = "revision",
                target = Target,
                unityVersion = "6000.6.0f1",
                rootSchemaVersion = 2,
                playerConfigSchemaVersion = 1,
                files = new[]
                {
                    new ContentTransportFile { path = "file", size = 1, sha256 = PlaceholderHash },
                },
                sourceFiles = Array.Empty<ContentSourceFile>(),
            };
        }

        private static string Hash(byte[] bytes)
        {
            return ContentDeliveryFiles.Sha256(bytes);
        }

        private static async Task<TException> CaptureException<TException>(Func<Task> action)
            where TException : Exception
        {
            Exception? failure = null;
            try { await action(); }
            catch (Exception exception) { failure = exception; }
            Assert.That(failure, Is.InstanceOf<TException>());
            return (TException)failure!;
        }

        private static string CreateTemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-dist-matrix", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private readonly struct InstalledFixture
        {
            internal InstalledFixture(string root, string digest)
            {
                Root = root;
                Digest = digest;
            }

            internal string Root { get; }

            internal string Digest { get; }
        }

        private sealed class CleanupBlockingSource : IContentArtifactSource
        {
            private readonly string _cacheRoot;
            private readonly IReadOnlyDictionary<string, byte[]> _values;
            private FileStream? _heldMarker;

            internal CleanupBlockingSource(string cacheRoot, byte[] manifest, byte[] payload)
            {
                _cacheRoot = cacheRoot;
                _values = new Dictionary<string, byte[]>
                {
                    { "transport.json", manifest },
                    { "content/file", payload },
                };
            }

            public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (relativePath == "content/file")
                {
                    var stagingRoot = Path.Combine(_cacheRoot, "staging");
                    var staging = Directory.GetDirectories(stagingRoot)[0];
                    _heldMarker = new FileStream(Path.Combine(staging, "staging-owner.json"),
                        FileMode.Open, FileAccess.Read, FileShare.Read);
                }
                return Task.FromResult<Stream>(new MemoryStream(_values[relativePath], writable: false));
            }

            public void Dispose()
            {
                _heldMarker?.Dispose();
                _heldMarker = null;
            }
        }
    }
}
