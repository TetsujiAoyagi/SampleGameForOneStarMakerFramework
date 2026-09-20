#nullable enable

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Tests.BuildContent.Distribution
{
    public sealed class ContentHttpArtifactSourceTests
    {
        private const string Target = "StandaloneWindows64-Player";
        private const string UnityVersion = "6000.6.0f1";

        [Test]
        public async Task ResponseBodyStalls_CallerCancellationStopsInstall()
        {
            var headersSent = Signal();
            var releaseServer = Signal();
            using var server = new LoopbackServer(async stream =>
            {
                await WriteAscii(stream, "HTTP/1.1 200 OK\r\nContent-Length: 100\r\nConnection: close\r\n\r\n");
                headersSent.TrySetResult(true);
                await releaseServer.Task;
            });
            using var source = new HttpContentArtifactSource(server.BaseUri);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {
                var readTask = ReadToEnd(source, "transport.json", cancellation.Token);
                await headersSent.Task;
                cancellation.Cancel();

                await CaptureException<OperationCanceledException>(async () => await readTask);
            }
            finally
            {
                cancellation.Cancel();
                releaseServer.TrySetResult(true);
                await server.Completion;
            }
        }

        [Test]
        public async Task DisconnectedShortBody_ReturnsIntegrityMismatchAndDoesNotPublish()
        {
            var payload = Encoding.UTF8.GetBytes("short");
            var manifest = CreateManifest("revision", payload, declaredSize: payload.Length + 4);
            var manifestBytes = Serialize(manifest);
            var root = Temp();
            try
            {
                using var server = new LoopbackServer(async (path, stream) =>
                {
                    var body = path == "/transport.json" ? manifestBytes : payload;
                    await WriteResponse(stream, 200, body, body.Length);
                }, expectedRequests: 2);
                using var source = new HttpContentArtifactSource(server.BaseUri);

                var exception = await CaptureException<ContentDeliveryException>(async () =>
                    await Install(root, manifest, manifestBytes, source));

                Assert.That(exception!.Code, Is.EqualTo(ContentDeliveryFailureCode.IntegrityMismatch));
                Assert.That(Directory.Exists(Path.Combine(root, "installed", "set", "revision")), Is.False);
                await server.Completion;
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public async Task NotFound_ReturnsTransportFailureAndDoesNotPublish()
        {
            var root = Temp();
            try
            {
                using var server = new LoopbackServer((_, stream) => WriteResponse(stream, 404, Array.Empty<byte>(), 0));
                using var source = new HttpContentArtifactSource(server.BaseUri);
                var request = new ContentInstallRequest(new string('a', 64), "set", "revision", Target,
                    UnityVersion, 2, 1024 * 1024);

                var exception = await CaptureException<ContentDeliveryException>(async () =>
                    await new ContentInstaller(new ContentCacheStore(root)).InstallAsync(
                        request, source, CancellationToken.None));

                Assert.That(exception!.Code, Is.EqualTo(ContentDeliveryFailureCode.TransportFailure));
                Assert.That(Directory.Exists(Path.Combine(root, "installed")), Is.False);
                await server.Completion;
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public async Task FailedNextRevision_PreservesKnownGoodBytesAndPin_ThenRetrySucceeds()
        {
            var root = Temp();
            try
            {
                var store = new ContentCacheStore(root);
                var goodPayload = Encoding.UTF8.GetBytes("known-good");
                var goodManifest = CreateManifest("good", goodPayload);
                var goodManifestBytes = Serialize(goodManifest);
                var good = await InstallFromServer(root, goodManifest, goodManifestBytes, goodPayload);
                store.MarkKnownGood(good);

                var goodBytesBefore = File.ReadAllBytes(Path.Combine(good.ContentPath, "payload.bin"));
                var pinPath = Path.Combine(root, "control", "known-good.json");
                var pinBefore = File.ReadAllBytes(pinPath);
                var nextPayload = Encoding.UTF8.GetBytes("next-revision");
                var badManifest = CreateManifest("next", nextPayload, sha256: new string('0', 64));
                var badManifestBytes = Serialize(badManifest);

                var failure = await CaptureException<ContentDeliveryException>(async () =>
                    await InstallFromServer(root, badManifest, badManifestBytes, nextPayload));

                Assert.That(failure!.Code, Is.EqualTo(ContentDeliveryFailureCode.IntegrityMismatch));
                Assert.That(File.ReadAllBytes(Path.Combine(good.ContentPath, "payload.bin")), Is.EqualTo(goodBytesBefore));
                Assert.That(File.ReadAllBytes(pinPath), Is.EqualTo(pinBefore));
                Assert.That(Directory.Exists(Path.Combine(root, "installed", "set", "next")), Is.False);

                var retryManifest = CreateManifest("next", nextPayload);
                var retry = await InstallFromServer(root, retryManifest, Serialize(retryManifest), nextPayload);
                Assert.That(File.ReadAllBytes(Path.Combine(retry.ContentPath, "payload.bin")), Is.EqualTo(nextPayload));
                Assert.That(File.ReadAllBytes(pinPath), Is.EqualTo(pinBefore),
                    "取得成功だけでは明示的な known-good 昇格を行わない。");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static async Task<ContentInstallResult> InstallFromServer(
            string root, ContentTransportManifest manifest, byte[] manifestBytes, byte[] payload)
        {
            using var server = new LoopbackServer(async (path, stream) =>
            {
                var body = path == "/transport.json" ? manifestBytes : payload;
                await WriteResponse(stream, 200, body, body.Length);
            }, expectedRequests: 2);
            using var source = new HttpContentArtifactSource(server.BaseUri);
            var result = await Install(root, manifest, manifestBytes, source);
            await server.Completion;
            return result;
        }

        private static Task<ContentInstallResult> Install(string root, ContentTransportManifest manifest,
            byte[] manifestBytes, IContentArtifactSource source)
        {
            var request = new ContentInstallRequest(ContentDeliveryFiles.Sha256(manifestBytes),
                manifest.contentSet, manifest.revision, manifest.target, manifest.unityVersion,
                manifest.rootSchemaVersion, 1024 * 1024);
            return new ContentInstaller(new ContentCacheStore(root)).InstallAsync(
                request, source, CancellationToken.None);
        }

        private static ContentTransportManifest CreateManifest(string revision, byte[] payload,
            long? declaredSize = null, string? sha256 = null) => new()
        {
            version = 1,
            product = "OneStarMaker",
            contentSet = "set",
            revision = revision,
            target = Target,
            unityVersion = UnityVersion,
            rootSchemaVersion = 2,
            playerConfigSchemaVersion = 1,
            files = new[]
            {
                new ContentTransportFile
                {
                    path = "payload.bin",
                    size = declaredSize ?? payload.Length,
                    sha256 = sha256 ?? ContentDeliveryFiles.Sha256(payload)
                }
            }
        };

        private static byte[] Serialize(ContentTransportManifest manifest) =>
            Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest));

        private static async Task<byte[]> ReadToEnd(IContentArtifactSource source, string path,
            CancellationToken cancellationToken)
        {
            using var stream = await source.OpenReadAsync(path, cancellationToken);
            using var output = new MemoryStream();
            await stream.CopyToAsync(output, 81920, cancellationToken);
            return output.ToArray();
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

        private static async Task WriteResponse(Stream stream, int status, byte[] body, int contentLength)
        {
            var reason = status == 200 ? "OK" : "Not Found";
            await WriteAscii(stream, $"HTTP/1.1 {status} {reason}\r\nContent-Length: {contentLength}\r\nConnection: close\r\n\r\n");
            if (body.Length > 0) await stream.WriteAsync(body, 0, body.Length);
        }

        private static async Task WriteAscii(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        }

        private static TaskCompletionSource<bool> Signal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static string Temp()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-dist-http", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class LoopbackServer : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly CancellationTokenSource _deadline = new(TimeSpan.FromSeconds(10));

            internal LoopbackServer(Func<NetworkStream, Task> response)
                : this((_, stream) => response(stream), 1)
            {
            }

            internal LoopbackServer(Func<string, NetworkStream, Task> response, int expectedRequests = 1)
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                BaseUri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
                Completion = Serve(response, expectedRequests);
            }

            internal Uri BaseUri { get; }
            internal Task Completion { get; }

            private async Task Serve(Func<string, NetworkStream, Task> response, int expectedRequests)
            {
                using var stopOnDeadline = _deadline.Token.Register(_listener.Stop);
                for (var index = 0; index < expectedRequests; index++)
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    var request = await ReadHeaders(stream, _deadline.Token);
                    var firstLine = request.Substring(0, request.IndexOf("\r\n", StringComparison.Ordinal));
                    var parts = firstLine.Split(' ');
                    await response(parts[1], stream);
                }
            }

            private static async Task<string> ReadHeaders(Stream stream, CancellationToken cancellationToken)
            {
                using var buffer = new MemoryStream();
                var value = new byte[1];
                while (buffer.Length < 16 * 1024)
                {
                    var read = await stream.ReadAsync(value, 0, 1, cancellationToken);
                    if (read == 0) throw new EndOfStreamException("HTTP request ended before its headers.");
                    buffer.WriteByte(value[0]);
                    if (buffer.Length >= 4)
                    {
                        var bytes = buffer.GetBuffer();
                        var end = buffer.Length;
                        if (bytes[end - 4] == '\r' && bytes[end - 3] == '\n' &&
                            bytes[end - 2] == '\r' && bytes[end - 1] == '\n')
                            return Encoding.ASCII.GetString(bytes, 0, (int)end);
                    }
                }
                throw new InvalidDataException("HTTP request headers exceeded the fixture limit.");
            }

            public void Dispose()
            {
                _listener.Stop();
                _deadline.Cancel();
                _deadline.Dispose();
            }
        }
    }
}
