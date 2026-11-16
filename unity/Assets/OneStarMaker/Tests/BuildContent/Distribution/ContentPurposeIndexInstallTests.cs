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
    /// <summary>
    /// 索引を持つ manifest の install と再検証。install は索引の JSON の型だけを見て、版と kind の値は見ない
    /// （索引の版を上げても古い runtime が manifest を install・再検証できる）。型が壊れた索引だけを拒む。
    /// </summary>
    public sealed class ContentPurposeIndexInstallTests
    {
        private static readonly byte[] Payload = Encoding.UTF8.GetBytes("payload");

        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\"Shared\",\"kind\":\"Scene\",\"representation\":\"Full\"}]}")]
        [TestCase("{\"version\":0,\"entries\":[]}")]
        // 型の規則を保つ版 2 と、未知の kind の値。選択の読み口は飛ばす・拒むが、install と再検証は通す。
        [TestCase("{\"version\":2,\"entries\":[{\"logicalKey\":\"Shared\",\"kind\":\"Scene\",\"representation\":\"Full\",\"extra\":1}]}")]
        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\"Shared\",\"kind\":\"Prefab\",\"representation\":\"Full\"}]}")]
        public async Task Install_IndexWithValidTypes_IsInstalledAndReverified(string rawIndex)
        {
            var root = NewRoot();
            try
            {
                var json = PurposeManifests.WithRawIndex(rawIndex, Manifest());
                var (result, digest) = await Install(root, json);
                Assert.That(result.AlreadyInstalled, Is.False);
                Assert.That(File.ReadAllBytes(Path.Combine(result.RevisionRoot, "transport.json")), Is.EqualTo(Encoding.UTF8.GetBytes(json)));
                Assert.DoesNotThrow(() => InstalledRevisionVerifier.Verify(result.RevisionRoot, digest, "set", "rev",
                    "StandaloneWindows64-Player"));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public async Task Install_ManifestWithoutIndex_IsInstalledAsBefore()
        {
            var root = NewRoot();
            try
            {
                var (result, digest) = await Install(root, PurposeManifests.WithoutIndex(Manifest()));
                Assert.DoesNotThrow(() => InstalledRevisionVerifier.Verify(result.RevisionRoot, digest, "set", "rev",
                    "StandaloneWindows64-Player"));
            }
            finally { Directory.Delete(root, true); }
        }

        [TestCase("[]")]
        [TestCase("{\"version\":\"1\",\"entries\":[]}")]
        [TestCase("{\"version\":1,\"entries\":{}}")]
        [TestCase("{\"version\":1,\"entries\":[{\"logicalKey\":\"A\",\"kind\":0,\"representation\":\"Full\"}]}")]
        public async Task Install_IndexWithBrokenTypes_IsRejected(string rawIndex)
        {
            var json = PurposeManifests.WithRawIndex(rawIndex, Manifest());
            Assert.Throws<ContentDeliveryException>(() => ContentManifestValidation.ValidateRequiredFields(json));
            var root = NewRoot();
            try
            {
                Exception? failure = null;
                try { await Install(root, json); }
                catch (Exception exception) { failure = exception; }
                Assert.That(failure, Is.TypeOf<ContentDeliveryException>());
                Assert.That(((ContentDeliveryException)failure!).Code, Is.EqualTo(ContentDeliveryFailureCode.InvalidManifest));
                Assert.That(Directory.Exists(Path.Combine(root, "installed", "set", "rev")), Is.False);
            }
            finally { Directory.Delete(root, true); }
        }

        private static ContentTransportManifest Manifest()
        {
            var manifest = PurposeManifests.Manifest("set", "rev");
            manifest.files = new[]
            {
                new ContentTransportFile { path = "payload.bin", size = Payload.Length, sha256 = ContentDeliveryFiles.Sha256(Payload) },
            };
            return manifest;
        }

        private static async Task<(ContentInstallResult result, string digest)> Install(string root, string json)
        {
            var manifestBytes = Encoding.UTF8.GetBytes(json);
            var digest = ContentDeliveryFiles.Sha256(manifestBytes);
            using var source = new MemorySource(new Dictionary<string, byte[]>
            {
                { "transport.json", manifestBytes },
                { "content/payload.bin", Payload },
            });
            var request = new ContentInstallRequest(digest, "set", "rev", "StandaloneWindows64-Player", "6000.6.9f1", 2, 1024 * 1024);
            var result = await new ContentInstaller(new ContentCacheStore(root)).InstallAsync(request, source, CancellationToken.None);
            return (result, digest);
        }

        private static string NewRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "osm-dist", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
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
