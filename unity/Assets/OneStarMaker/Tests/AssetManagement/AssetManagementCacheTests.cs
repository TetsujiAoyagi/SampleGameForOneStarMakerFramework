#nullable enable

using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Cache;
using OneStarMaker.Runtime.AssetManagement.Internal;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.AssetManagement
{
    /// <summary>
    /// 常駐キャッシュを挟んだときの asset 寿命契約を検証する。
    ///
    /// <para>
    /// キャッシュの有無で「backend へ release が届くタイミング」が変わるのが本質。
    /// hit した再ロードは backend を叩き直さず、キャッシュ無効時は release が即座に貫通する。
    /// Scene owner の解放はキャッシュへの退避であって backend release ではない。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AssetManagementCacheTests
    {
        private FakeAssetBackend _backend = null!;
        private Runtime.AssetManagement.AssetManagement _assetManagement = null!;
        private Dictionary<AssetType, long> _budgets = null!;
        private double _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = 0.0;
            _backend = new FakeAssetBackend();
            _budgets = new Dictionary<AssetType, long>
            {
                [AssetType.Prefab] = 1024 * 1024,
                [AssetType.Texture] = 1024 * 1024,
            };
            _assetManagement = CreateAssetManagementWithCache();
        }

        [UnityTest]
        public IEnumerator Cache_LoadReleaseLoad_ReusesBackendWithoutRelease() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/Cached.prefab");

            var first = await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.Manual);
            Assert.That(_backend.LoadAssetCallCount, Is.EqualTo(1));

            _assetManagement.Release(first);
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(0));

            var second = await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.Manual);
            Assert.That(_backend.LoadAssetCallCount, Is.EqualTo(1));
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(0));
            Assert.That(second.IsValid, Is.True);
        });

        [UnityTest]
        public IEnumerator NoCache_ReleaseImmediatelyCallsBackend() => UniTask.ToCoroutine(async () =>
        {
            _assetManagement = new Runtime.AssetManagement.AssetManagement(_backend, cache: null);

            var key = AssetKey.FromAddress("Assets/Prefabs/Uncached.prefab");
            var handle = await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.Manual);

            _assetManagement.Release(handle);

            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator InstantiateAsync_ReleasesImmediatelyWithoutCaching() => UniTask.ToCoroutine(async () =>
        {
            var go = await _assetManagement.InstantiateAsync(
                AssetKey.FromAddress("Assets/Prefabs/Enemy.prefab"));

            // batchmode(-nographics)では OnDestroy 経由の破棄通知が発火しないため、
            // 破棄通知を直接呼んで「インスタンスは Store されず即 backend.Release される」ことを決定論的に検証する。
            var instanceId = EntityId.ToULong(go.GetEntityId());
            _assetManagement.NotifyGameObjectDestroyed(instanceId);

            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(1));

            Object.DestroyImmediate(go);
        });

        [UnityTest]
        public IEnumerator SceneOwnedAsset_StoredOnReleaseScene_AndReusedOnNextLoad() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/SceneShared.prefab");

            await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.Scene("Battle"));
            Assert.That(_backend.LoadAssetCallCount, Is.EqualTo(1));

            _assetManagement.ReleaseScene("Battle");
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(0));

            await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.Scene("Next"));
            Assert.That(_backend.LoadAssetCallCount, Is.EqualTo(1));
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        public IEnumerator ReleaseAll_ReleasesCachedAssetsViaClear() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/AppA.prefab");
            var handle = await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.Manual);

            _assetManagement.Release(handle);
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(0));

            _assetManagement.ReleaseAll();
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator Cache_OverBudget_EvictsToBackendReleasedAssets() => UniTask.ToCoroutine(async () =>
        {
            _budgets[AssetType.Texture] = 100;
            _assetManagement = CreateAssetManagementWithCache(fixedBytes: 60);

            var keyA = AssetKey.FromAddress("Assets/Textures/A.png");
            var keyB = AssetKey.FromAddress("Assets/Textures/B.png");

            var handleA = await _assetManagement.LoadAssetAsync<Texture2D>(keyA, AssetOwner.Manual);
            var handleB = await _assetManagement.LoadAssetAsync<Texture2D>(keyB, AssetOwner.Manual);
            Assert.That(_backend.LoadAssetCallCount, Is.EqualTo(2));

            _assetManagement.Release(handleA);
            _assetManagement.Release(handleB);

            Assert.That(_backend.ReleasedAssets, Has.Count.EqualTo(1));
        });

        private Runtime.AssetManagement.AssetManagement CreateAssetManagementWithCache(long fixedBytes = 50)
        {
            var cache = new AssetResidentCache(
                new FakeBudgetProvider(_budgets),
                halfLifeSeconds: 300f,
                _backend.Release,
                _ => fixedBytes,
                () => _clock);

            return new Runtime.AssetManagement.AssetManagement(_backend, cache);
        }

        private sealed class FakeBudgetProvider : IBudgetProvider
        {
            private readonly Dictionary<AssetType, long> _budgets;

            public FakeBudgetProvider(Dictionary<AssetType, long> budgets)
            {
                _budgets = budgets;
            }

            public long GetBudgetBytes(AssetType type)
            {
                return _budgets.TryGetValue(type, out var bytes) ? bytes : 0;
            }
        }
    }
}
