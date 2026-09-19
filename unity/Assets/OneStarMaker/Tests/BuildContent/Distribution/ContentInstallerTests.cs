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
        private sealed class MemorySource : IContentArtifactSource { private readonly IReadOnlyDictionary<string, byte[]> _values; internal MemorySource(IReadOnlyDictionary<string, byte[]> values) => _values = values; public Task<Stream> OpenReadAsync(string path, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult<Stream>(new MemoryStream(_values[path], false)); } public void Dispose() { } }
    }
}
