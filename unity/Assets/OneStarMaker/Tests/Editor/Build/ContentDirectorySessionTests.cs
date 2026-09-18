#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Cache;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using UnityEditor;
using Unity.Loading;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class ContentDirectorySessionTests
    {
        private const string ScenePath = "Assets/OneStarMaker/Scenes/UISystem/UIScene.unity";

        [Test]
        public async Task CancelledSceneLoad_DrainsNativeCompletionBeforeUnregister()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var root = CreateRoot();
            var native = new FakeNativeDirectory(root);
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                using var cts = new CancellationTokenSource();
                var load = session.LoadSceneAsync("scene", "Full", default, cts.Token).AsTask();
                cts.Cancel();
                try { await load; Assert.Fail("取消した caller の待機が成功した"); }
                catch (OperationCanceledException) { }

                var close = session.CloseAsync().AsTask();
                Assert.That(close.IsCompleted, Is.False, "caller の取消は native terminal ではない");
                Assert.That(native.UnregisterCount, Is.Zero);

                native.SceneResult.TrySetResult(new FakeScene());
                await close;
                Assert.That(native.UnloadCount, Is.EqualTo(1));
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                Directory.Delete(path);
            }
        }

        [Test]
        public void InvalidRoot_RollsBackAndAllowsSamePathRetry()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var wrong = CreateRoot("wrong");
            var correct = CreateRoot();
            try
            {
                var first = new FakeNativeDirectory(wrong);
                var failure = Assert.Throws<ContentDirectoryException>(() =>
                    ContentDirectorySession.Register(path, "build", "StandaloneWindows64", first));
                Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.IdentityMismatch));
                Assert.That(first.UnregisterCount, Is.EqualTo(1));

                var next = new FakeNativeDirectory(correct);
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", next);
                session.CloseAsync().GetAwaiter().GetResult();
                Assert.That(next.UnregisterCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wrong);
                UnityEngine.Object.DestroyImmediate(correct);
                Directory.Delete(path);
            }
        }

        [Test]
        public async Task LiveInstance_RejectsCloseUntilOwnerReleasesIt()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/TutorialInfo/Icons/URP.png");
            Assert.That(asset, Is.Not.Null);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId), Is.True);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize("build", "StandaloneWindows64", new[] {
                new BuildContentEntry("object", "stable", "Full",
                    new Loadable<UnityEngine.Object>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset)), guid, localId)
            });
            var native = new FakeNativeDirectory(root) { InstanceResult = new FakeInstance() };
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                var instance = await session.InstantiateAsync("object", "Full", null, false, CancellationToken.None);
                try
                {
                    await session.CloseAsync();
                    Assert.Fail("生存中の instance を持つ directory が閉じられた");
                }
                catch (ContentDirectoryException ex)
                {
                    Assert.That(ex.Code, Is.EqualTo(ContentDirectoryFailureCode.ResourcesInUse));
                }
                Assert.That(native.UnregisterCount, Is.Zero);

                session.Release((IBackendAsset)instance);
                await session.CloseAsync();
                Assert.That(native.ReleaseCount, Is.EqualTo(1));
                Assert.That(native.UnregisterCount, Is.EqualTo(1));
            }
            finally
            {
                if (native.InstanceResult?.Instance != null)
                    UnityEngine.Object.DestroyImmediate(native.InstanceResult.Instance);
                UnityEngine.Object.DestroyImmediate(root);
                Directory.Delete(path);
            }
        }

        [Test]
        public async Task UnregisterFailure_KeepsDeleteBlockedAndCloseCanRetry()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var root = CreateRoot();
            var native = new FakeNativeDirectory(root) { FailUnregisterCount = 1 };
            try
            {
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", native);
                var failure = Assert.ThrowsAsync<ContentDirectoryException>(async () => await session.CloseAsync());
                Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.OperationFailed));
                Assert.That(ContentRevisionGate.TryAcquireDelete("build", "StandaloneWindows64", path,
                    out var blocked, out var reason), Is.False);
                Assert.That(blocked, Is.Null);
                Assert.That(reason, Is.EqualTo(ContentDirectoryFailureCode.RevisionBusy));

                await session.CloseAsync();
                Assert.That(native.UnregisterCount, Is.EqualTo(2));
                Assert.That(ContentRevisionGate.TryAcquireDelete("build", "StandaloneWindows64", path,
                    out var lease, out _), Is.True);
                lease!.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                Directory.Delete(path);
            }
        }

        [Test]
        public void FailedRegistrationRollback_IsRetriedOnNextRegistration()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-content-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var wrong = CreateRoot("wrong");
            var right = CreateRoot();
            try
            {
                var first = new FakeNativeDirectory(wrong) { FailUnregisterCount = 1 };
                var failure = Assert.Throws<ContentDirectoryException>(() =>
                    ContentDirectorySession.Register(path, "build", "StandaloneWindows64", first));
                Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.RegistrationFailed));
                Assert.That(ContentRevisionGate.TryAcquireDelete("build", "StandaloneWindows64", path,
                    out var blocked, out _), Is.False);
                Assert.That(blocked, Is.Null);

                var second = new FakeNativeDirectory(right);
                var session = ContentDirectorySession.Register(path, "build", "StandaloneWindows64", second);
                session.CloseAsync().GetAwaiter().GetResult();
                Assert.That(first.UnregisterCount, Is.EqualTo(2));
                Assert.That(second.UnregisterCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(wrong);
                UnityEngine.Object.DestroyImmediate(right);
                Directory.Delete(path);
            }
        }

        [Test]
        public async Task ConcurrentCallers_ShareOneAssetAndOneSceneToken()
        {
            var directory = new FakeDirectoryPort();
            var assets = new AssetManagement();
            assets.InstallContentDirectory(directory);
            var texture = new Texture2D(1, 1);
            try
            {
                var firstAsset = assets.LoadContentAssetAsync<Texture2D>("object", "Full", AssetOwner.Manual).AsTask();
                var secondAsset = assets.LoadContentAssetAsync<Texture2D>("object", "Full", AssetOwner.Manual).AsTask();
                Assert.That(directory.AssetLoads, Is.EqualTo(1));
                directory.AssetResult.TrySetResult(new FakeAsset(texture));
                var handleA = await firstAsset;
                var handleB = await secondAsset;
                Assert.That(handleA.Value, Is.SameAs(handleB.Value));
                assets.Release(handleA);
                assets.Release(handleB);
                Assert.That(directory.AssetReleases, Is.EqualTo(1));

                var firstScene = assets.LoadContentSceneAsync("scene", "Full").AsTask();
                var secondScene = assets.LoadContentSceneAsync("scene", "Full").AsTask();
                Assert.That(directory.SceneLoads, Is.EqualTo(1));
                directory.SceneResult.TrySetResult(new FakeScene());
                Assert.That((await firstScene).IsLoaded, Is.True);
                Assert.That((await secondScene).IsLoaded, Is.True);
                await assets.UnloadSceneAsync("scene");
                assets.ReleaseScene("scene");
                Assert.That(directory.SceneUnloads, Is.EqualTo(1));
                await assets.CloseContentDirectoryAsync();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Test]
        public async Task ResidentCache_ReleasesDirectoryTokenAtClose()
        {
            var directory = new FakeDirectoryPort();
            var cache = new AssetResidentCache(new FixedBudget(), 300f, _ => Assert.Fail("directory token を Addressables 側へ返した"), _ => 1L, () => 0d);
            var assets = new AssetManagement(new AddressableBackend(), cache);
            assets.InstallContentDirectory(directory);
            var texture = new Texture2D(1, 1);
            try
            {
                directory.AssetResult.TrySetResult(new FakeAsset(texture));
                var handle = await assets.LoadContentAssetAsync<Texture2D>("object", "Full", AssetOwner.Manual);
                assets.Release(handle);
                Assert.That(directory.AssetReleases, Is.Zero, "resident cache も directory の利用中 token として保持する");

                await assets.CloseContentDirectoryAsync();
                Assert.That(directory.AssetReleases, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static BuildContentRoot CreateRoot(string identity = "build")
        {
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize(identity, "StandaloneWindows64", new[] {
                new BuildContentEntry("scene", "stable", "Full", LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath))
            });
            return root;
        }

        private sealed class FakeNativeDirectory : IContentNativeDirectory
        {
            private readonly BuildContentRoot _root;
            internal FakeNativeDirectory(BuildContentRoot root) => _root = root;
            internal readonly UniTaskCompletionSource<IBackendScene> SceneResult = new();
            internal int UnloadCount;
            internal int UnregisterCount;
            internal int ReleaseCount;
            internal int FailUnregisterCount;
            internal IBackendInstance? InstanceResult;
            public bool Register(string path) => true;
            public BuildContentRoot[] GetRoots() => new[] { _root };
            public void Unregister()
            {
                UnregisterCount++;
                if (FailUnregisterCount-- > 0) throw new InvalidOperationException("fake unregister failure");
            }
            public UniTask<IBackendScene> LoadSceneAsync(BuildContentEntry entry, SceneLoadOptions options) => SceneResult.Task;
            public UniTask UnloadSceneAsync(IBackendScene scene) { UnloadCount++; return UniTask.CompletedTask; }
            public UniTask<IBackendAsset> LoadObjectAsync<T>(BuildContentEntry entry) where T : UnityEngine.Object => throw new NotSupportedException();
            public UniTask<IBackendInstance> InstantiateAsync(BuildContentEntry entry, Transform? parent, bool worldSpace)
                => InstanceResult != null ? UniTask.FromResult(InstanceResult!) : throw new NotSupportedException();
            public void Release(IBackendAsset asset)
            {
                ReleaseCount++;
                if (asset is FakeInstance instance && instance.Instance != null)
                    UnityEngine.Object.DestroyImmediate(instance.Instance);
            }
        }

        private sealed class FakeInstance : IBackendInstance, IBackendAsset
        {
            public GameObject? Instance { get; } = new("fake-content-instance");
            public UnityEngine.Object? Asset => Instance;
            public bool IsValid => Instance != null;
        }

        private sealed class FakeScene : IBackendScene, IContentDirectoryToken
        {
            public bool IsLoaded => true;
            public string Name => "fake";
            public GameObject[] GetRootGameObjects() => Array.Empty<GameObject>();
        }

        private sealed class FakeAsset : IBackendAsset, IContentDirectoryToken
        {
            internal FakeAsset(Texture2D asset) => Asset = asset;
            public UnityEngine.Object? Asset { get; }
            public bool IsValid => Asset != null;
        }

        private sealed class FakeDirectoryPort : IContentDirectoryBackend
        {
            internal readonly UniTaskCompletionSource<IBackendAsset> AssetResult = new();
            internal readonly UniTaskCompletionSource<IBackendScene> SceneResult = new();
            internal int AssetLoads;
            internal int SceneLoads;
            internal int AssetReleases;
            internal int SceneUnloads;
            private Action? _evict;
            public string BuildIdentity => "build";
            public string Target => "StandaloneWindows64";
            public string CachePrefix => "directory:test:";
            public AssetKey GetObjectKey(string logicalKey, string representation)
                => AssetKey.FromContentDirectory(CachePrefix + logicalKey + ":" + representation, AssetType.Other);
            public UniTask<IBackendAsset> LoadAssetAsync<T>(string logicalKey, string representation, CancellationToken ct) where T : UnityEngine.Object
            { AssetLoads++; return AssetResult.Task; }
            public UniTask<IBackendScene> LoadSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options, CancellationToken ct)
            { SceneLoads++; return SceneResult.Task; }
            public UniTask<IBackendInstance> InstantiateAsync(string logicalKey, string representation, Transform? parent, bool worldSpace, CancellationToken ct)
                => throw new NotSupportedException();
            public UniTask UnloadSceneAsync(IBackendScene scene) { SceneUnloads++; return UniTask.CompletedTask; }
            public void ReleaseSceneAfterUnityShutdown(IBackendScene scene) { }
            public void Release(IBackendAsset asset) => AssetReleases++;
            public void ConfigureCacheEviction(Action evictRevisionEntries) => _evict = evictRevisionEntries;
            public UniTask StopAndDrainAsync() => UniTask.CompletedTask;
            public UniTask CloseAsync() { _evict?.Invoke(); return UniTask.CompletedTask; }
            public void BeginSynchronousShutdown() { }
        }

        private sealed class FixedBudget : IBudgetProvider
        {
            public long GetBudgetBytes(AssetType type) => 1024;
        }
    }
}
