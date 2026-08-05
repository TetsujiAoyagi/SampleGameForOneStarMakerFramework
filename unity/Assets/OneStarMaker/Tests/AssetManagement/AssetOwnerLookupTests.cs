#nullable enable

using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.AssetManagement
{
    [TestFixture]
    public class AssetOwnerLookupTests
    {
        private FakeAssetBackend _backend = null!;
        private Runtime.AssetManagement.AssetManagement _assetManagement = null!;
        private IAssetDiagnostics _diagnostics = null!;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeAssetBackend();
            _assetManagement = new Runtime.AssetManagement.AssetManagement(_backend);
            _diagnostics = _assetManagement;
        }

        [UnityTest]
        public IEnumerator SceneOwner_GetOwnersAndReleaseScene() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/SceneOwned.prefab");
            var owner = AssetOwner.Scene("Title");

            await _assetManagement.LoadAssetAsync<GameObject>(key, owner);

            Assert.That(_diagnostics.GetOwners(key), Has.Count.EqualTo(1));
            Assert.That(_diagnostics.GetOwners(key)[0], Is.EqualTo(owner));
            Assert.That(_diagnostics.GetOwnedAssets(owner), Has.Count.EqualTo(1));
            Assert.That(_diagnostics.GetOwnedAssets(owner)[0], Is.EqualTo(key));

            _assetManagement.ReleaseScene("Title");

            Assert.That(_diagnostics.GetOwners(key), Is.Empty);
            Assert.That(_diagnostics.GetOwnedAssets(owner), Is.Empty);
        });

        [UnityTest]
        public IEnumerator GameObjectOwner_GetOwnersAndDestroyRelease() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/GoOwned.prefab");
            var go = new GameObject("GoOwned");
            var owner = AssetOwner.Bind(go);

            await _assetManagement.LoadAssetAsync<GameObject>(key, owner);

            Assert.That(_diagnostics.GetOwners(key), Has.Count.EqualTo(1));
            Assert.That(_diagnostics.GetOwners(key)[0], Is.EqualTo(owner));
            Assert.That(_diagnostics.GetOwnedAssets(owner), Has.Count.EqualTo(1));
            Assert.That(_diagnostics.GetOwnedAssets(owner)[0], Is.EqualTo(key));

            var instanceId = EntityId.ToULong(go.GetEntityId());
            _assetManagement.NotifyGameObjectDestroyed(instanceId);

            Assert.That(_diagnostics.GetOwners(key), Is.Empty);
            Assert.That(_diagnostics.GetOwnedAssets(owner), Is.Empty);

            Object.DestroyImmediate(go);
        });

        [UnityTest]
        public IEnumerator SameOwner_DoubleAcquire_PartialRelease() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/DedupOwner.prefab");
            var owner = AssetOwner.Manual;

            var first = await _assetManagement.LoadAssetAsync<GameObject>(key, owner);
            await _assetManagement.LoadAssetAsync<GameObject>(key, owner);

            Assert.That(_diagnostics.GetOwners(key), Has.Count.EqualTo(2));

            _assetManagement.Release(first);

            Assert.That(_diagnostics.GetOwners(key), Has.Count.EqualTo(1));
            Assert.That(_diagnostics.GetOwnedAssets(owner), Has.Count.EqualTo(1));
            Assert.That(_diagnostics.GetOwnedAssets(owner)[0], Is.EqualTo(key));
        });

        [UnityTest]
        public IEnumerator UnloadedKey_GetOwners_ReturnsEmptyList() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/NeverLoaded.prefab");

            var owners = _diagnostics.GetOwners(key);

            Assert.That(owners, Is.Not.Null);
            Assert.That(owners, Is.Empty);
            await UniTask.CompletedTask;
        });
    }
}
