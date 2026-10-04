#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class ContentInstallTestOperationTests
    {
        private const string Parent = "Assets/OneStarMakerGenerated/BS3Tests";
        private readonly List<BuildContentDirectoryIntegrationTests> _fixtures = new();
        private readonly List<Action> _releases = new();
        private readonly List<string> _unrelatedFolders = new();

        [UnityTearDown]
        public IEnumerator ReleaseFixturesAfterAssertionFailure()
        {
            foreach (var release in _releases) release();
            foreach (var fixture in _fixtures)
            {
                var cleanup = fixture.CleanupOwnedResources();
                while (cleanup.MoveNext()) yield return cleanup.Current;
            }
            foreach (var folder in _unrelatedFolders)
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            var generated = Path.Combine(Application.dataPath, "OneStarMakerGenerated");
            if (Directory.Exists(generated) && Directory.GetFileSystemEntries(generated).Length == 0)
                AssetDatabase.DeleteAsset("Assets/OneStarMakerGenerated");
        }

        [UnityTest]
        public IEnumerator SignalThenCancel_DrainsInstallerBeforeDeletingDelivery()
        {
            var test = new BuildContentDirectoryIntegrationTests();
            var paths = Prepare(test);
            var unrelated = "Assets/OneStarMakerGenerated/Unrelated" + Guid.NewGuid().ToString("N");
            EnsureFolder(unrelated);
            _unrelatedFolders.Add(unrelated);
            var (manifest, payload, digest) = Content();
            var source = new SignalSource(manifest, payload);
            var store = new ContentCacheStore(Path.Combine(paths.delivery, "cache"));
            var request = Request(digest);
            test.StartInstallForTest(async token =>
            {
                using (source) await new ContentInstaller(store).InstallAsync(request, source, token);
            });
            yield return UniTask.ToCoroutine(async () => await source.Reached);
            var inspected = false;
            test.AfterDrainForTest = () =>
            {
                Assert.That(store.Inspect().Entries, Is.Empty, "transaction must be released before Inspect");
                Assert.That(Directory.Exists(Path.Combine(store.RootPath, "installed", "set", "rev")), Is.False);
                AssertStagingEmpty(store.RootPath);
                Assert.That(source.Disposed, Is.True);
                Assert.That(source.StreamDisposed, Is.True);
                inspected = true;
            };
            yield return test.CleanupOwnedResources();
            Assert.That(test.LastCleanupFailure, Is.Null);
            Assert.That(test.LastOperationDrained, Is.True);
            Assert.That(inspected, Is.True);
            AssertClean(paths);
            Assert.That(AssetDatabase.IsValidFolder(unrelated), Is.True,
                "cleanup must preserve a nonempty unrelated parent");
            AssetDatabase.DeleteAsset(unrelated);
            var generated = Path.Combine(Application.dataPath, "OneStarMakerGenerated");
            if (Directory.Exists(generated) && Directory.GetFileSystemEntries(generated).Length == 0)
                AssetDatabase.DeleteAsset("Assets/OneStarMakerGenerated");
        }

        [UnityTest]
        public IEnumerator SignalThenSourceFault_PreservesTheObservedFaultAndCleans()
        {
            var test = new BuildContentDirectoryIntegrationTests();
            var paths = Prepare(test);
            var (manifest, payload, digest) = Content();
            var source = new SignalSource(manifest, payload);
            var store = new ContentCacheStore(Path.Combine(paths.delivery, "cache"));
            test.StartInstallForTest(async token =>
            {
                using (source) await new ContentInstaller(store).InstallAsync(Request(digest), source, token);
            });
            yield return UniTask.ToCoroutine(async () => await source.Reached);
            source.Fault(new IOException("injected source failure"));
            Exception? observed = null;
            yield return new WaitUntil(() => test.InstallCompletedForTest);
            try { test.ObserveInstallForTest(); }
            catch (Exception failure) { observed = failure; }
            Assert.That(observed, Is.TypeOf<ContentDeliveryException>());
            test.AfterDrainForTest = () =>
            {
                Assert.That(store.Inspect().Entries, Is.Empty);
                AssertStagingEmpty(store.RootPath);
                Assert.That(source.StreamDisposed, Is.True);
            };
            yield return test.CleanupOwnedResources();
            Assert.That(test.LastCleanupFailure, Is.Null, "the already observed source fault must not be reported twice");
            AssertClean(paths);
        }

        [UnityTest]
        public IEnumerator UnobservedSourceFault_IsReportedAfterDrainWithoutLeakingLock()
        {
            var test = new BuildContentDirectoryIntegrationTests();
            var paths = Prepare(test);
            var (manifest, payload, digest) = Content();
            var source = new SignalSource(manifest, payload);
            var store = new ContentCacheStore(Path.Combine(paths.delivery, "cache"));
            test.StartInstallForTest(async token =>
            {
                using (source) await new ContentInstaller(store).InstallAsync(Request(digest), source, token);
            });
            yield return UniTask.ToCoroutine(async () => await source.Reached);
            source.Fault(new IOException("unobserved source failure"));
            yield return new WaitUntil(() => test.InstallCompletedForTest);
            test.AfterDrainForTest = () =>
            {
                Assert.That(store.Inspect().Entries, Is.Empty);
                AssertStagingEmpty(store.RootPath);
            };
            yield return test.CleanupOwnedResources();
            Assert.That(test.LastCleanupFailure, Is.TypeOf<ContentDeliveryException>());
            Assert.That(test.LastOperationDrained, Is.True);
            AssertClean(paths);
        }

        [UnityTest]
        public IEnumerator ActualHttpResponseInterrupt_ClosesResponseAndWorker()
        {
            var test = new BuildContentDirectoryIntegrationTests();
            var paths = Prepare(test);
            var (manifest, payload, digest) = Content();
            var published = Path.Combine(paths.delivery, "published");
            Directory.CreateDirectory(Path.Combine(published, "content"));
            File.WriteAllBytes(Path.Combine(published, "transport.json"), manifest);
            File.WriteAllBytes(Path.Combine(published, "content", "payload.bin"), payload);
            var store = new ContentCacheStore(Path.Combine(paths.delivery, "cache"));
            LoopbackArtifactServer? server = null;
            test.StartInstallForTest(async token =>
            {
                server = new LoopbackArtifactServer(published, token, "content/payload.bin");
                await ContentInstallTestOperation.RunWithShutdownAsync(async () =>
                {
                    using var source = new HttpContentArtifactSource(server.BaseUri);
                    await new ContentInstaller(store).InstallAsync(Request(digest), source, token);
                }, server.StopAsync);
            });
            yield return UniTask.ToCoroutine(async () => await server!.ResponseStarted);
            test.AfterDrainForTest = () =>
            {
                Assert.That(server!.RequestCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(server.ResponseClosed.IsCompleted, Is.True);
                Assert.That(server.Completion.IsCompleted, Is.True);
                Assert.That(server.HasActiveResponse, Is.False);
                Assert.That(store.Inspect().Entries, Is.Empty);
                AssertStagingEmpty(store.RootPath);
            };
            yield return test.CleanupOwnedResources();
            Assert.That(test.LastCleanupFailure, Is.Null);
            AssertClean(paths);
        }

        [UnityTest]
        public IEnumerator CallbackSeesRegisteredOwner_AndSynchronousFailureIsDrained()
        {
            var test = new BuildContentDirectoryIntegrationTests();
            var paths = Prepare(test);
            var registeredAtStart = false;
            test.StartInstallForTest(_ =>
            {
                registeredAtStart = test.InstallRegisteredForTest;
                throw new InvalidOperationException("synchronous install failure");
            });
            Assert.That(registeredAtStart, Is.True);
            Assert.That(test.InstallCompletedForTest, Is.True);
            yield return test.CleanupOwnedResources();
            Assert.That(test.LastCleanupFailure, Is.TypeOf<InvalidOperationException>());
            AssertClean(paths);
        }

        [UnityTest]
        public IEnumerator OperationAndShutdownFaults_BothReachActualCleanup()
        {
            var test = new BuildContentDirectoryIntegrationTests();
            var paths = Prepare(test);
            var primary = new IOException("source fault");
            var shutdown = new InvalidOperationException("server shutdown fault");
            test.StartInstallForTest(_ => ContentInstallTestOperation.RunWithShutdownAsync(
                () => Task.FromException(primary), () => Task.FromException(shutdown)));
            yield return new WaitUntil(() => test.InstallCompletedForTest);
            yield return test.CleanupOwnedResources();
            Assert.That(test.LastCleanupFailure, Is.TypeOf<AggregateException>());
            var failures = ((AggregateException)test.LastCleanupFailure!).InnerExceptions;
            Assert.That(failures.Count, Is.EqualTo(2));
            Assert.That(failures[0], Is.SameAs(primary));
            Assert.That(failures[1], Is.SameAs(shutdown));
            AssertClean(paths);
        }

        [UnityTest]
        public IEnumerator DeadlineBeforeTerminal_PreservesDeliveryUntilLaterDrain()
        {
            var test = new BuildContentDirectoryIntegrationTests();
            var paths = Prepare(test);
            var held = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _releases.Add(() => held.TrySetResult(true));
            var reached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            test.StartInstallForTest(async _ => { reached.TrySetResult(true); await held.Task; });
            yield return UniTask.ToCoroutine(async () => await reached.Task);
            using var deadline = new CancellationTokenSource();
            deadline.Cancel();
            yield return test.CleanupOwnedResources(deadline.Token);
            Assert.That(test.LastOperationDrained, Is.False);
            Assert.That(test.LastCleanupFailure, Is.TypeOf<TimeoutException>());
            Assert.That(Directory.Exists(paths.delivery), Is.True);
            Assert.That(Directory.Exists(paths.copy), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(paths.folder), Is.False);
            held.TrySetResult(true);
            yield return new WaitUntil(() => test.InstallCompletedForTest);
            test.ObserveInstallForTest();
            yield return test.CleanupOwnedResources();
            Assert.That(test.LastCleanupFailure, Is.Null);
            AssertClean(paths);
        }

        private (string folder, string copy, string delivery) Prepare(BuildContentDirectoryIntegrationTests test)
        {
            _fixtures.Add(test);
            EnsureFolder(Parent);
            var folder = Parent + "/" + Guid.NewGuid().ToString("N");
            EnsureFolder(folder);
            var suffix = Guid.NewGuid().ToString("N");
            var copy = Path.Combine(Path.GetTempPath(), "osm-dist-copy-" + suffix);
            var delivery = Path.Combine(Path.GetTempPath(), "osm-dist-delivery-" + suffix);
            Directory.CreateDirectory(copy);
            Directory.CreateDirectory(delivery);
            test.ConfigureCleanupForTest(folder, copy, delivery);
            return (folder, copy, delivery);
        }

        private static void AssertClean((string folder, string copy, string delivery) paths)
        {
            Assert.That(AssetDatabase.IsValidFolder(paths.folder), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(Parent), Is.False);
            var ownedFolder = Path.Combine(Application.dataPath, paths.folder.Substring("Assets/".Length));
            var parentFolder = Path.Combine(Application.dataPath, Parent.Substring("Assets/".Length));
            Assert.That(Directory.Exists(ownedFolder), Is.False);
            Assert.That(File.Exists(ownedFolder + ".meta"), Is.False);
            Assert.That(Directory.Exists(parentFolder), Is.False);
            Assert.That(File.Exists(parentFolder + ".meta"), Is.False);
            Assert.That(Directory.Exists(paths.copy), Is.False);
            Assert.That(Directory.Exists(paths.delivery), Is.False);
        }

        private static void AssertStagingEmpty(string cacheRoot)
        {
            var staging = Path.Combine(cacheRoot, "staging");
            Assert.That(!Directory.Exists(staging) || Directory.GetFileSystemEntries(staging).Length == 0, Is.True);
        }

        private static ContentInstallRequest Request(string digest) => new(digest, "set", "rev",
            "StandaloneWindows64-Player", Application.unityVersion, 2, 1024 * 1024);

        private static (byte[] manifest, byte[] payload, string digest) Content()
        {
            var payload = Encoding.UTF8.GetBytes("installed bytes");
            var manifest = new ContentTransportManifest
            {
                version = 1, product = "OneStarMaker", contentSet = "set", revision = "rev",
                target = "StandaloneWindows64-Player", unityVersion = Application.unityVersion,
                rootSchemaVersion = 2, playerConfigSchemaVersion = 1,
                files = new[] { new ContentTransportFile
                    { path = "payload.bin", size = payload.Length, sha256 = Sha(payload) } }
            };
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest));
            return (bytes, payload, Sha(bytes));
        }

        private static string Sha(byte[] value)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(value)).Replace("-", "").ToLowerInvariant();
        }

        private static void EnsureFolder(string path)
        {
            var parent = "Assets";
            foreach (var segment in path.Substring("Assets/".Length).Split('/'))
            {
                var next = parent + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, segment);
                parent = next;
            }
        }

        private sealed class SignalSource : IContentArtifactSource
        {
            private readonly byte[] _manifest;
            private readonly byte[] _payload;
            private readonly TaskCompletionSource<bool> _reached =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _release =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Task Reached => _reached.Task;
            internal bool Disposed { get; private set; }
            internal bool StreamDisposed { get; private set; }
            internal SignalSource(byte[] manifest, byte[] payload) { _manifest = manifest; _payload = payload; }
            internal void Fault(Exception failure) => _release.TrySetException(failure);
            public Task<Stream> OpenReadAsync(string relativePath, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult<Stream>(relativePath == "transport.json"
                    ? new MemoryStream(_manifest, false)
                    : new SignalStream(_payload, token, _reached, _release, () => StreamDisposed = true));
            }
            public void Dispose() => Disposed = true;
        }

        private sealed class SignalStream : MemoryStream
        {
            private readonly CancellationToken _operation;
            private readonly TaskCompletionSource<bool> _reached;
            private readonly TaskCompletionSource<bool> _release;
            private readonly Action _disposed;
            internal SignalStream(byte[] bytes, CancellationToken operation,
                TaskCompletionSource<bool> reached, TaskCompletionSource<bool> release, Action disposed)
                : base(bytes, false)
            { _operation = operation; _reached = reached; _release = release; _disposed = disposed; }
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                _reached.TrySetResult(true);
                using (token.Register(() => _release.TrySetCanceled()))
                using (_operation.Register(() => _release.TrySetCanceled()))
                    await _release.Task;
                return await base.ReadAsync(buffer, offset, count, token);
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) _disposed();
                base.Dispose(disposing);
            }
        }
    }
}
