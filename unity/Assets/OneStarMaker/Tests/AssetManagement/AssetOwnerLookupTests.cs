#nullable enable

using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.AssetManagement
{
    /// <summary>
    /// asset key と owner の相互逆引き契約を検証する。
    ///
    /// <para>
    /// 「誰がこの asset を掴んでいるか」「この owner は何を掴んでいるか」の双方が
    /// 解放後に正しく縮むことが主眼。未ロード key の問い合わせは例外ではなく空を返す。
    /// </para>
    /// </summary>
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

        // description: 形式の canonical key は文字列から復元できない。
        // 以前は GetOwnedAssets が InvalidOperationException を投げていた（PR #7 Critical）。
        [UnityTest]
        public IEnumerator DescriptionKey_GetOwnedAssets_ReturnsKeyWithoutThrowing() => UniTask.ToCoroutine(async () =>
        {
            var desc = new TestAssetDescription();
            desc.AddPayload("Full", new AssetReference("Assets/Prefabs/Enemy.Full.prefab"));
            var key = AssetKey.FromDescription(desc, "Full");
            var owner = AssetOwner.Scene("Battle");

            await _assetManagement.LoadAssetAsync<GameObject>(key, owner);

            var owned = _diagnostics.GetOwnedAssets(owner);

            Assert.That(owned, Has.Count.EqualTo(1));
            Assert.That(owned[0], Is.EqualTo(key));
            Assert.That(owned[0].Canonical, Is.EqualTo(key.Canonical));
            Assert.That(_diagnostics.GetOwners(key), Has.Count.EqualTo(1));
        });

        // address: と description: が混在していても、address 側だけ返して落ちる／
        // 片方が欠けることが無いこと。
        [UnityTest]
        public IEnumerator MixedKeyForms_GetOwnedAssets_ReturnsBoth() => UniTask.ToCoroutine(async () =>
        {
            var desc = new TestAssetDescription();
            desc.AddPayload(string.Empty, new AssetReference("Assets/Prefabs/FromDescription.prefab"));
            var descKey = AssetKey.FromDescription(desc);
            var addressKey = AssetKey.FromAddress("Assets/Prefabs/FromAddress.prefab");
            var owner = AssetOwner.Scene("Mixed");

            await _assetManagement.LoadAssetAsync<GameObject>(addressKey, owner);
            await _assetManagement.LoadAssetAsync<GameObject>(descKey, owner);

            var owned = _diagnostics.GetOwnedAssets(owner);

            Assert.That(owned, Has.Count.EqualTo(2));
            Assert.That(owned, Contains.Item(addressKey));
            Assert.That(owned, Contains.Item(descKey));
        });

        // Instantiate のエントリは辞書キーに ":instance:" suffix が付く。
        // これも文字列からは復元できないため、元プレハブの key が返ること。
        [UnityTest]
        public IEnumerator InstantiatedAsset_GetOwnedAssets_ReturnsSourcePrefabKey() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/Instantiated.prefab");

            var instance = await _assetManagement.InstantiateAsync(key);
            var owner = AssetOwner.Bind(instance);

            var owned = _diagnostics.GetOwnedAssets(owner);

            Assert.That(owned, Has.Count.EqualTo(1));
            Assert.That(owned[0], Is.EqualTo(key));

            Object.DestroyImmediate(instance);
        });

        // 旧実装の Release(handle) は owner を問わず一律 AssetOwner.Manual を外していたため、
        // App / Scene / Bind 所有のアセットでは Owners が減らないまま RefCount だけ減っていた
        // （PR #7 High）。
        [UnityTest]
        public IEnumerator ReleaseHandle_NonManualOwner_RemovesThatOwner() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/SceneHandle.prefab");
            var owner = AssetOwner.Scene("Title");

            var handle = await _assetManagement.LoadAssetAsync<GameObject>(key, owner);
            Assert.That(_diagnostics.GetOwners(key), Has.Count.EqualTo(1));

            _assetManagement.Release(handle);

            Assert.That(_diagnostics.GetOwners(key), Is.Empty);
            Assert.That(_diagnostics.GetOwnedAssets(owner), Is.Empty);
            Assert.That(handle.IsValid, Is.False);
        });

        // 同一キーを複数 owner が持つとき、解放した handle の owner だけが外れること。
        [UnityTest]
        public IEnumerator ReleaseHandle_SharedKey_RemovesOnlyOwnHandleOwner() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/SharedOwners.prefab");
            var sceneOwner = AssetOwner.Scene("Title");

            var appHandle = await _assetManagement.LoadAssetAsync<GameObject>(key, AssetOwner.App);
            await _assetManagement.LoadAssetAsync<GameObject>(key, sceneOwner);

            Assert.That(_diagnostics.GetOwners(key), Has.Count.EqualTo(2));

            _assetManagement.Release(appHandle);

            var owners = _diagnostics.GetOwners(key);
            Assert.That(owners, Has.Count.EqualTo(1));
            Assert.That(owners[0], Is.EqualTo(sceneOwner));
            Assert.That(_diagnostics.GetOwnedAssets(AssetOwner.App), Is.Empty);
            Assert.That(_diagnostics.GetOwnedAssets(sceneOwner), Has.Count.EqualTo(1));

            // Scene 側はまだ持っているので backend Release は走っていない。
            Assert.That(_backend.ReleaseCallCount, Is.EqualTo(0));
        });

        // LoadAppAssetSync が返す handle の owner も App であること。
        [UnityTest]
        public IEnumerator ReleaseHandle_AppSyncLoad_RemovesAppOwner() => UniTask.ToCoroutine(async () =>
        {
            var key = AssetKey.FromAddress("Assets/Prefabs/AppSync.prefab");

            var handle = _assetManagement.LoadAppAssetSync<GameObject>(key);

            Assert.That(_diagnostics.GetOwners(key), Has.Count.EqualTo(1));
            Assert.That(_diagnostics.GetOwners(key)[0], Is.EqualTo(AssetOwner.App));

            _assetManagement.Release(handle);

            Assert.That(_diagnostics.GetOwners(key), Is.Empty);
            await UniTask.CompletedTask;
        });

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
