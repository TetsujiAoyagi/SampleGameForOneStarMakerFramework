#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Cache;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Tests.AssetManagement
{
    /// <summary>
    /// directory mode の fail-closed（AC16、I7）。封鎖の証明は負例: 封じた後の取得系は backend の member 全部と
    /// 公開面の全入口で失敗し、封じる前の handle の解放と Scene の unload は通り、registry・cache・in-flight の
    /// 迂回経路が閉じている。directory の読み口はそのまま動く。
    /// </summary>
    [TestFixture]
    public sealed class SealedAssetBackendTests
    {
        private static readonly AssetKey ConfigKey = AssetKey.FromAddress("Assets/Config/app-config.json");
        private static readonly AssetKey OtherKey = AssetKey.FromAddress("Assets/Prefabs/Other.prefab");

        // reflection で IAssetBackend の member を列挙する。Release / UnloadSceneAsync 以外は全て取得系とみなし、
        // 将来 member が増えたときに分類漏れ（封じ漏れ）をこのテストが拾う。
        [Test]
        public void EveryAcquisitionMember_OfIAssetBackend_IsRejectedBySealedBackend()
        {
            var teardown = new[] { nameof(IAssetBackend.Release), nameof(IAssetBackend.UnloadSceneAsync) };
            var inner = new FakeAssetBackend();
            var sealedBackend = new SealedAssetBackend(inner);
            var acquisition = typeof(IAssetBackend).GetMethods().Where(method => !teardown.Contains(method.Name)).ToArray();
            Assert.That(acquisition.Select(method => method.Name), Is.EquivalentTo(new[]
            {
                nameof(IAssetBackend.LoadAssetAsync), nameof(IAssetBackend.LoadAssetSync),
                nameof(IAssetBackend.LoadSceneAsync), nameof(IAssetBackend.InstantiateAsync),
            }), "IAssetBackend に新しい member がある。取得系なら SealedAssetBackend で拒否し、解放系ならこの一覧に足す");

            foreach (var method in acquisition)
            {
                var target = method.IsGenericMethodDefinition ? method.MakeGenericMethod(typeof(TextAsset)) : method;
                var arguments = target.GetParameters().Select(parameter =>
                    parameter.ParameterType == typeof(string) ? "address"
                    : parameter.ParameterType == typeof(CancellationToken) ? CancellationToken.None
                    : parameter.ParameterType == typeof(SceneLoadOptions) ? default(SceneLoadOptions)
                    : parameter.ParameterType == typeof(bool) ? false
                    : (object?)null).ToArray();
                var thrown = Assert.Throws<TargetInvocationException>(() => target.Invoke(sealedBackend, arguments), method.Name);
                Assert.That(thrown!.InnerException, Is.TypeOf<InvalidOperationException>(), method.Name);
            }
            Assert.That(inner.LoadAssetCallCount, Is.Zero);
            Assert.That(inner.LoadSceneCallCount, Is.Zero);
        }

        [Test]
        public void TeardownMembers_PassThroughToInner()
        {
            var inner = new FakeAssetBackend();
            var sealedBackend = new SealedAssetBackend(inner);
            var scene = inner.LoadSceneAsync("scene", default, CancellationToken.None).GetAwaiter().GetResult();
            var asset = inner.LoadAssetAsync<TextAsset>("asset", CancellationToken.None).GetAwaiter().GetResult();

            sealedBackend.UnloadSceneAsync(scene, CancellationToken.None).GetAwaiter().GetResult();
            sealedBackend.Release(asset);

            Assert.That(inner.UnloadSceneCallCount, Is.EqualTo(1));
            Assert.That(inner.ReleasedAssets, Does.Contain(asset));
        }

        // ═══ 公開面: 封じた後は記録した key 以外の取得が全て失敗し、封じる前の handle と Scene は片付けられる ═══

        [Test]
        public void PublicSurface_AfterSeal_RejectsAcquisition_AndAllowsReleaseOfEarlierHandles()
        {
            var backend = new FakeAssetBackend();
            var assets = new Runtime.AssetManagement.AssetManagement(backend);
            var config = assets.LoadAppAssetSync<TextAsset>(ConfigKey);
            var sceneDescription = new SceneAssetDescription("Earlier", LoadType.OnDemand, new List<AssetPayload> { new(string.Empty, new AssetReference("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")) });
            var earlierScene = assets.LoadSceneAsync("Earlier", sceneDescription).GetAwaiter().GetResult();
            Assert.That(earlierScene.IsLoaded, Is.True);

            assets.SealAddressables();
            Assert.That(assets.IsAddressablesSealed, Is.True);
            assets.SealAddressables();
            var loadsBefore = backend.LoadAssetCallCount;

            // 封じる前に registry に無い address key は、別の所有者が頼んでも取得できない（registry・cache・backend のどこからも）。
            Assert.ThrowsAsync<InvalidOperationException>(async () => await assets.LoadAssetAsync<GameObject>(OtherKey, AssetOwner.Manual));
            Assert.Throws<InvalidOperationException>(() => assets.LoadAppAssetSync<GameObject>(OtherKey));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await assets.LoadAssetAsync<GameObject>(
                AssetKey.FromDescription(new SceneAssetDescription("Later", LoadType.OnDemand, new List<AssetPayload> { new(string.Empty, new AssetReference("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")) })), AssetOwner.Manual));
            var laterScene = new SceneAssetDescription("Later", LoadType.OnDemand, new List<AssetPayload> { new(string.Empty, new AssetReference("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")) });
            Assert.ThrowsAsync<InvalidOperationException>(async () => await assets.LoadSceneAsync("Later", laterScene));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await assets.InstantiateAsync(OtherKey));
            Assert.That(backend.LoadAssetCallCount, Is.EqualTo(loadsBefore), "封じた後に backend のロードが呼ばれた");
            Assert.That(backend.LoadSceneCallCount, Is.EqualTo(1));

            // 封じる前に registry にある key（config の TextAsset）は、別の所有者も引き続き取得できる（許可した集合）。
            var again = assets.LoadAssetAsync<TextAsset>(ConfigKey, AssetOwner.Manual).GetAwaiter().GetResult();
            Assert.That(again.IsValid, Is.True);
            Assert.That(backend.LoadAssetCallCount, Is.EqualTo(loadsBefore), "記録した key は registry から返り、backend は呼ばれない");

            // 封じる前の handle は有効なまま所有者の解放で返せ、封じる前の Scene は unload できる。
            Assert.That(config.IsValid, Is.True);
            assets.Release(again);
            assets.UnloadSceneAsync("Earlier").GetAwaiter().GetResult();
            Assert.That(backend.UnloadSceneCallCount, Is.EqualTo(1));
            assets.ReleaseAll();
            Assert.That(backend.ReleaseCallCount, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Seal_WithInFlightLoad_IsRejected()
        {
            var backend = new HoldingBackend();
            var assets = new Runtime.AssetManagement.AssetManagement(backend);
            var pending = assets.LoadAssetAsync<TextAsset>(OtherKey, AssetOwner.Manual);

            Assert.Throws<InvalidOperationException>(() => assets.SealAddressables());
            Assert.That(assets.IsAddressablesSealed, Is.False);

            backend.Complete();
            pending.GetAwaiter().GetResult();
            Assert.DoesNotThrow(() => assets.SealAddressables());
        }

        [Test]
        public void Seal_DropsAddressablesEntriesFromResidentCache()
        {
            var backend = new FakeAssetBackend();
            var cache = new AssetResidentCache(new FixedBudget(), 60f, backend.Release, _ => 1, () => 0);
            var assets = new Runtime.AssetManagement.AssetManagement(backend, cache);
            var handle = assets.LoadAssetAsync<TextAsset>(OtherKey, AssetOwner.Manual).GetAwaiter().GetResult();
            assets.Release(handle);
            Assert.That(cache.GetSnapshot().ResidentBytes.Values.Sum(), Is.EqualTo(1), "前提: 解放した asset は cache に戻る");

            assets.SealAddressables();

            Assert.That(cache.TryTake(OtherKey.Canonical, out _), Is.False, "封じた時点で cache から落ちる");
            Assert.That(backend.ReleaseCallCount, Is.EqualTo(1), "落とした entry は内側の backend で解放される");
            Assert.ThrowsAsync<InvalidOperationException>(async () => await assets.LoadAssetAsync<TextAsset>(OtherKey, AssetOwner.Manual));
            Assert.That(backend.LoadAssetCallCount, Is.EqualTo(1));
        }

        [Test]
        public void DirectoryLoads_StillWorkAfterSeal()
        {
            var backend = new FakeAssetBackend();
            var assets = new Runtime.AssetManagement.AssetManagement(backend);
            assets.SealAddressables();
            var directory = new FakeDirectoryPort();
            assets.InstallContentDirectory(directory);
            var texture = new Texture2D(1, 1);
            try
            {
                directory.AssetResult.TrySetResult(new DirectoryAsset(texture));
                var handle = assets.LoadContentAssetAsync<Texture2D>("logical", "Full", AssetOwner.Manual).GetAwaiter().GetResult();
                Assert.That(handle.Value, Is.SameAs(texture));
                Assert.That(directory.AssetLoads, Is.EqualTo(1));
                directory.SceneResult.TrySetResult(new DirectoryScene());
                var scene = assets.LoadContentSceneAsync("scene", "Full", new SceneLoadOptions(UnityEngine.SceneManagement.LoadSceneMode.Additive)).GetAwaiter().GetResult();
                Assert.That(scene.IsLoaded, Is.True);
                Assert.That(directory.SceneLoads, Is.EqualTo(1));
                assets.Release(handle);
                Assert.That(backend.LoadAssetCallCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private sealed class FixedBudget : IBudgetProvider
        {
            public long GetBudgetBytes(AssetType type) => 1024;
        }

        private sealed class HoldingBackend : IAssetBackend
        {
            private readonly UniTaskCompletionSource<IBackendAsset> _pending = new();
            public void Complete() => _pending.TrySetResult(new HeldAsset());
            public UniTask<IBackendAsset> LoadAssetAsync<T>(string address, CancellationToken ct) where T : UnityEngine.Object => _pending.Task;
            public IBackendAsset LoadAssetSync<T>(string address) where T : UnityEngine.Object => throw new NotSupportedException();
            public UniTask<IBackendScene> LoadSceneAsync(string address, SceneLoadOptions options, CancellationToken ct) => throw new NotSupportedException();
            public UniTask UnloadSceneAsync(IBackendScene scene, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<IBackendInstance> InstantiateAsync(string address, Transform? parent, bool worldSpace, CancellationToken ct) => throw new NotSupportedException();
            public void Release(IBackendAsset asset) { }

            private sealed class HeldAsset : IBackendAsset
            {
                public UnityEngine.Object? Asset => null;
                public bool IsValid => true;
            }
        }

        private sealed class DirectoryAsset : IBackendAsset, IContentDirectoryToken
        {
            internal DirectoryAsset(UnityEngine.Object asset) => Asset = asset;
            public UnityEngine.Object? Asset { get; }
            public bool IsValid => Asset != null;
        }

        private sealed class DirectoryScene : IBackendScene, IContentDirectoryToken
        {
            public bool IsLoaded => true;
            public string Name => "directory-scene";
            public GameObject[] GetRootGameObjects() => Array.Empty<GameObject>();
        }

        private sealed class FakeDirectoryPort : IContentDirectoryBackend
        {
            internal readonly UniTaskCompletionSource<IBackendAsset> AssetResult = new();
            internal readonly UniTaskCompletionSource<IBackendScene> SceneResult = new();
            internal int AssetLoads;
            internal int SceneLoads;
            private Action? _evict;
            public string BuildIdentity => "build";
            public string Target => "StandaloneWindows64-Player";
            public string CachePrefix => "directory:test:";
            public AssetKey GetObjectKey(string logicalKey, string representation)
                => AssetKey.FromContentDirectory(CachePrefix + logicalKey + ":" + representation, AssetType.Other);
            public UniTask<IBackendAsset> LoadAssetAsync<T>(string logicalKey, string representation, CancellationToken ct) where T : UnityEngine.Object
            { AssetLoads++; return AssetResult.Task; }
            public UniTask<IBackendScene> LoadSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options, CancellationToken ct)
            { SceneLoads++; return SceneResult.Task; }
            public UniTask<IBackendInstance> InstantiateAsync(string logicalKey, string representation, Transform? parent, bool worldSpace, CancellationToken ct)
                => throw new NotSupportedException();
            public UniTask UnloadSceneAsync(IBackendScene scene) => UniTask.CompletedTask;
            public void ReleaseSceneAfterUnityShutdown(IBackendScene scene) { }
            public void ReleaseSceneTokenAfterPlayStop(IBackendScene scene) { }
            public void Release(IBackendAsset asset) { }
            public void ConfigureCacheEviction(Action evictRevisionEntries) => _evict = evictRevisionEntries;
            public UniTask StopAndDrainAsync() => UniTask.CompletedTask;
            public void EnsureAccepting() { }
            public UniTask CloseAsync() { _evict?.Invoke(); return UniTask.CompletedTask; }
            public void CompletePlayStop() { _evict?.Invoke(); }
            public void BeginSynchronousShutdown() { }
        }
    }
}
