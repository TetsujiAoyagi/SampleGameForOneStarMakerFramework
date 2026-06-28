#nullable enable

using System.Collections.Generic;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.AssetManagement
{
    [TestFixture]
    public class AssetManagementTests
    {
        private FakeAssetBackend _backend = null!;
        private Runtime.AssetManagement.AssetManagement _assetManagement = null!;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeAssetBackend();
            _assetManagement = new Runtime.AssetManagement.AssetManagement(_backend);
        }

        [UnityTest]
        public IEnumerator Asset_Load_Dedup() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/Enemy.prefab");

            var first = await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.Manual);
            var second = await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.Manual);

            Assert.That(_backend.LoadAssetCallCount, Is.EqualTo(1));

            _assetManagement.Release(first);
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(0));

            _assetManagement.Release(second);
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator Asset_Dedup_AcrossOwners() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/Shared.prefab");

            var appHandle = await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.App);
            var manualHandle = await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.Manual);

            // 異なる owner でも backend ロードは 1 回。refcount で共有される。
            Assert.That(_backend.LoadAssetCallCount, Is.EqualTo(1));

            // Manual 分を解放しても App が残るため backend Release は走らない。
            _assetManagement.Release(manualHandle);
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(0));
            Assert.That(appHandle.IsValid, Is.True);

            // App 分の解放で refcount 0 になり backend Release が 1 回走る。
            _assetManagement.ReleaseAll();
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator Asset_Variant_Distinct() => UniTask.ToCoroutine(async () =>
        {
            var desc = new TestAssetDescription();
            desc.AddPayload("Full", new AssetReference("Assets/Prefabs/Enemy.Full.prefab"));
            desc.AddPayload("Proxy", new AssetReference("Assets/Prefabs/Enemy.Proxy.prefab"));

            await _assetManagement.LoadAssetAsync<GameObject>(
                AssetKey.FromDescription(desc, "Full"),
                AssetOwner.Manual);
            await _assetManagement.LoadAssetAsync<GameObject>(
                AssetKey.FromDescription(desc, "Proxy"),
                AssetOwner.Manual);

            Assert.That(_backend.LoadAssetCallCount, Is.EqualTo(2));
        });

        [UnityTest]
        public IEnumerator Scene_Load_Unload() => UniTask.ToCoroutine(async () =>
        {
            var desc = CreateSceneDescription("TestScene", "Assets/Scenes/Test.unity");

            var handle = await _assetManagement.LoadSceneAsync(
                "TestScene",
                desc,
                string.Empty);
            await _assetManagement.UnloadSceneAsync("TestScene");

            Assert.That(handle.Identity, Is.EqualTo("TestScene"));
            Assert.That(handle.IsLoaded, Is.False);
            Assert.That(_backend.ReleaseCallCount, Is.GreaterThan(0));
        });

        [UnityTest]
        public IEnumerator App_ReleaseAll() => UniTask.ToCoroutine(async () =>
        {
            await _assetManagement.LoadAssetAsync<GameObject>(
                AssetKey.FromAddress("Assets/Prefabs/AppA.prefab"),
                AssetOwner.App);
            await _assetManagement.LoadAssetAsync<GameObject>(
                AssetKey.FromAddress("Assets/Prefabs/AppB.prefab"),
                AssetOwner.App);

            _assetManagement.ReleaseAll();
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(2));
        });

        [UnityTest]
        public IEnumerator Scene_SameKey_Twice_FullyReleased() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/SceneShared.prefab");

            await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.Scene("Battle"));
            await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.Scene("Battle"));

            // dedup により backend ロードは 1 回。
            Assert.That(_backend.LoadAssetCallCount, Is.EqualTo(1));

            _assetManagement.ReleaseScene("Battle");

            // 所有回数（2）ぶん Release が呼ばれ、最後の 1 回で backend.Release され refcount が残らない。
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(1));
            Assert.That(_assetManagement.LoadedAssetCountForTests, Is.EqualTo(0));
        });

        [UnityTest]
        public IEnumerator App_SameKey_Twice_ReleaseAll() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/AppShared.prefab");

            await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.App);
            await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.App);

            Assert.That(_backend.LoadAssetCallCount, Is.EqualTo(1));

            _assetManagement.ReleaseAll();

            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(1));
            Assert.That(_assetManagement.LoadedAssetCountForTests, Is.EqualTo(0));
        });

        [UnityTest]
        public IEnumerator Scene_Release_Scoped() => UniTask.ToCoroutine(async () =>
        {
            var appHandle = await _assetManagement.LoadAssetAsync<GameObject>(
                AssetKey.FromAddress("Assets/Prefabs/App.prefab"),
                AssetOwner.App);
            await _assetManagement.LoadAssetAsync<GameObject>(
                AssetKey.FromAddress("Assets/Prefabs/Scene.prefab"),
                AssetOwner.Scene("Battle"));

            _assetManagement.ReleaseScene("Battle");

            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(1));
            Assert.That(appHandle.IsValid, Is.True);

            _assetManagement.ReleaseAll();
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(2));
        });

        [UnityTest]
        public IEnumerator InstantiateAsync_ReleasesOnGameObjectDestroy() => UniTask.ToCoroutine(async () =>
        {
            var go = await _assetManagement.InstantiateAsync(
                AssetKey.FromAddress("Assets/Prefabs/Enemy.prefab"));

            Object.DestroyImmediate(go);
            await UniTask.Yield();

            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator Manual_Release() => UniTask.ToCoroutine(async () =>
        {
            var handle = await _assetManagement.LoadAssetAsync<GameObject>(
                AssetKey.FromAddress("Assets/Prefabs/Manual.prefab"),
                AssetOwner.Manual);

            _assetManagement.Release(handle);

            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(1));
            Assert.That(handle.IsValid, Is.False);
        });

        private static SceneAssetDescription CreateSceneDescription(string identity, string address)
        {
            var desc = new SceneAssetDescription();
            desc.SceneIdentity = identity;
            desc.AddPayload(string.Empty, new AssetReference(address));
            return desc;
        }

        private sealed class TestAssetDescription : AssetDescription
        {
            private readonly List<AssetPayload> _payloads = new();

            public override IReadOnlyList<AssetPayload> Payloads => _payloads;

            public void AddPayload(string variant, AssetReference reference)
            {
                _payloads.Add(new AssetPayload(variant, reference));
            }

            internal override AssetReference? ResolveReference(string variant)
            {
                foreach (var payload in _payloads)
                {
                    if (payload.Variant == variant)
                    {
                        return payload.Reference;
                    }
                }

                return null;
            }
        }
    }
}
