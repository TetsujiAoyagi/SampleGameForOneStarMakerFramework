#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement.Cache;
using OneStarMaker.Runtime.AssetManagement.Components;
using OneStarMaker.Runtime.AssetManagement.Internal;
using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// アセットとシーンのロード寿命をスコープ付きで一元管理する。
    /// </summary>
    public sealed class AssetManagement : IAssetManagement, IAssetDiagnostics
    {
        private readonly IAssetBackend _backend;
        private readonly IAssetResidentCache? _cache;
        private readonly AssetRegistry _registry = new();

        /// <summary>同一 key の並行ロードを 1 本の backend ロードに集約する in-flight テーブル。</summary>
        private readonly Dictionary<string, UniTask<IBackendAsset>> _inFlight = new(StringComparer.Ordinal);

        public AssetManagement()
            : this(new AddressableBackend())
        {
        }

        public AssetManagement(MemoryBudgetConfig? budgetConfig)
        {
            var backend = new AddressableBackend();
            _backend = backend;
            _cache = budgetConfig != null
                ? new AssetResidentCache(budgetConfig, budgetConfig.HalfLifeSeconds, backend.Release)
                : null;
        }

        internal AssetManagement(IAssetBackend backend)
            : this(backend, null)
        {
        }

        internal AssetManagement(IAssetBackend backend, IAssetResidentCache? cache)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _cache = cache;
        }

        public async UniTask<IAssetHandle<T>> LoadAssetAsync<T>(
            AssetKey key,
            AssetOwner owner,
            CancellationToken ct = default) where T : UnityEngine.Object
        {
            AssetRegistry.LoadedAsset loaded;
            if (_registry.TryGetAsset(key.Canonical, out var existing))
            {
                loaded = _registry.Acquire(key.Canonical, existing.Backend, owner, key.Type, isInstance: false);
            }
            else if (_cache != null && _cache.TryTake(key.Canonical, out var cached))
            {
                loaded = _registry.Acquire(key.Canonical, cached, owner, key.Type, isInstance: false);
            }
            else
            {
                var backendAsset = await LoadBackendAssetDedup<T>(key, ct);
                loaded = _registry.Acquire(key.Canonical, backendAsset, owner, key.Type, isInstance: false);
            }

            AttachDestroyReleaseIfNeeded(owner);
            return new AssetHandle<T>(loaded);
        }

        public IAssetHandle<T> LoadAppAssetSync<T>(AssetKey key) where T : UnityEngine.Object
        {
            AssetRegistry.LoadedAsset loaded;
            if (_registry.TryGetAsset(key.Canonical, out var existing))
            {
                loaded = _registry.Acquire(key.Canonical, existing.Backend, AssetOwner.App, key.Type, isInstance: false);
            }
            else if (_cache != null && _cache.TryTake(key.Canonical, out var cached))
            {
                loaded = _registry.Acquire(key.Canonical, cached, AssetOwner.App, key.Type, isInstance: false);
            }
            else
            {
                var backendAsset = _backend.LoadAssetSync<T>(key.Address);
                loaded = _registry.Acquire(key.Canonical, backendAsset, AssetOwner.App, key.Type, isInstance: false);
            }

            return new AssetHandle<T>(loaded);
        }

        public async UniTask<ISceneHandle> LoadSceneAsync(
            string sceneIdentity,
            SceneAssetDescription desc,
            string variant = "",
            SceneLoadOptions options = default,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sceneIdentity))
            {
                throw new ArgumentException("Scene identity is required.", nameof(sceneIdentity));
            }

            if (desc == null)
            {
                throw new ArgumentNullException(nameof(desc));
            }

            if (_registry.TryGetScene(sceneIdentity, out var existing) && !existing.IsUnloaded)
            {
                return new SceneHandle(sceneIdentity, existing.Backend);
            }

            var key = AssetKey.FromDescription(desc, variant);
            var backendScene = await _backend.LoadSceneAsync(key.Address, options, ct);
            _registry.AddScene(sceneIdentity, backendScene);
            return new SceneHandle(sceneIdentity, backendScene);
        }

        public async UniTask UnloadSceneAsync(string sceneIdentity, CancellationToken ct = default)
        {
            if (!_registry.TryGetScene(sceneIdentity, out var scene) || scene.IsUnloaded)
            {
                return;
            }

            await _backend.UnloadSceneAsync(scene.Backend, ct);
            _registry.MarkSceneUnloaded(sceneIdentity);
        }

        public async UniTask<GameObject> InstantiateAsync(
            AssetKey key,
            Transform? parent = null,
            bool worldSpace = false,
            CancellationToken ct = default)
        {
            var backendInstance = await _backend.InstantiateAsync(key.Address, parent, worldSpace, ct);
            var instance = backendInstance.Instance
                ?? throw new InvalidOperationException($"Instantiate failed: {key.Canonical}");

            var owner = AssetOwner.Bind(instance);
            if (backendInstance is IBackendAsset backendAsset)
            {
                var instanceKey = $"{key.Canonical}:instance:{owner.GameObjectId}";
                _registry.Acquire(instanceKey, backendAsset, owner, AssetType.Prefab, isInstance: true);
            }

            AttachDestroyReleaseIfNeeded(owner);
            return instance;
        }

        public void Release(IAssetHandle handle)
        {
            if (handle == null)
            {
                throw new ArgumentNullException(nameof(handle));
            }

            ReleaseKey(handle.Key);
        }

        /// <inheritdoc/>
        public void ReleaseScene(string sceneIdentity)
        {
            // 契約: ReleaseScene は「所有アセット解放」だけ。Scene 本体の backend Unload はしない。
            // 通常 gameplay では Phase 2 (UnloadSceneAsync) → Phase 3 (ここ) の順が必須。
            // Play Mode 終了などで未 Unload のまま来た場合は、ここではなく ReleaseAll（Shutdown）を使う。
            if (_registry.TryGetScene(sceneIdentity, out var scene) && !scene.IsUnloaded)
            {
                throw new InvalidOperationException(
                    $"ReleaseScene('{sceneIdentity}') は未アンロードの Scene 本体に対して呼べません。" +
                    " 先に UnloadSceneAsync するか、teardown なら ReleaseAll を使ってください。");
            }

            foreach (var loaded in _registry.ReleaseSceneOwned(sceneIdentity))
            {
                ReleaseOrStore(loaded);
            }
        }

        /// <inheritdoc/>
        public void ReleaseAll()
        {
            // Shutdown 契約:
            // Application.quitting / Play Mode 終了では Unity が先に Scene を解体している。
            // その状態で Addressables.UnloadSceneAsync を呼ぶと
            // 「Cannot find handle for scene」になり得るため、backend Unload は一切行わない。
            // 台帳上の Scene を MarkUnloaded し、所有アセットと App スコープ資産を同期で一気に落とす。
            foreach (var scene in _registry.GetScenes())
            {
                if (!scene.IsUnloaded)
                {
                    _registry.MarkSceneUnloaded(scene.Identity);
                }
            }

            ReleaseAllAssetsNow();
        }

        private void ReleaseAllAssetsNow()
        {
            foreach (var loaded in _registry.ReleaseAllAssets())
            {
                _backend.Release(loaded.Backend);
            }

            _cache?.Clear();
        }

        internal void NotifyGameObjectDestroyed(ulong gameObjectInstanceId)
        {
            foreach (var loaded in _registry.ReleaseGameObjectOwned(gameObjectInstanceId))
            {
                ReleaseOrStore(loaded);
            }
        }

        internal int LoadedAssetCountForTests => _registry.AssetCount;

        public IReadOnlyList<AssetOwner> GetOwners(AssetKey key)
        {
            if (!_registry.TryGetAsset(key.Canonical, out var loaded))
            {
                return Array.Empty<AssetOwner>();
            }

            return new List<AssetOwner>(loaded.Owners);
        }

        public IReadOnlyList<AssetKey> GetOwnedAssets(AssetOwner owner)
        {
            var keys = new List<AssetKey>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var loaded in _registry.GetAllLoadedAssets())
            {
                if (!ContainsOwner(loaded, owner))
                {
                    continue;
                }

                if (!seen.Add(loaded.Key))
                {
                    continue;
                }

                keys.Add(ToAssetKey(loaded));
            }

            return keys;
        }

        private static bool ContainsOwner(AssetRegistry.LoadedAsset loaded, AssetOwner owner)
        {
            foreach (var entry in loaded.Owners)
            {
                if (entry.Equals(owner))
                {
                    return true;
                }
            }

            return false;
        }

        private static AssetKey ToAssetKey(AssetRegistry.LoadedAsset loaded)
        {
            const string addressPrefix = "address:";
            if (loaded.Key.StartsWith(addressPrefix, StringComparison.Ordinal))
            {
                return AssetKey.FromAddress(loaded.Key.Substring(addressPrefix.Length));
            }

            throw new InvalidOperationException(
                $"Cannot reconstruct AssetKey from registry entry: {loaded.Key}");
        }

        private void ReleaseKey(string key)
        {
            if (_registry.Release(key, AssetOwner.Manual, out var loaded) && loaded != null)
            {
                ReleaseOrStore(loaded);
            }
        }

        private void ReleaseOrStore(AssetRegistry.LoadedAsset loaded)
        {
            if (_cache != null && !loaded.IsInstance)
            {
                _cache.Store(loaded.Key, loaded.Type, loaded.Backend);
            }
            else
            {
                _backend.Release(loaded.Backend);
            }
        }

        /// <summary>
        /// 同一 canonical key が並行ロード中なら同じ UniTask を await 共有し、二重 backend ロードを防ぐ。
        /// 完了後に in-flight テーブルから除去し、呼び出し側が registry へ Acquire する。
        /// </summary>
        private async UniTask<IBackendAsset> LoadBackendAssetDedup<T>(AssetKey key, CancellationToken ct)
            where T : UnityEngine.Object
        {
            if (_inFlight.TryGetValue(key.Canonical, out var pending))
            {
                return await pending;
            }

            // Preserve() で複数 await を許可し、並行呼び出し間で結果を共有する。
            var task = _backend.LoadAssetAsync<T>(key.Address, ct).Preserve();
            _inFlight[key.Canonical] = task;
            try
            {
                return await task;
            }
            finally
            {
                _inFlight.Remove(key.Canonical);
            }
        }

        private void AttachDestroyReleaseIfNeeded(AssetOwner owner)
        {
            if (owner.Kind != AssetOwnerKind.GameObject || owner.BoundObject == null)
            {
                return;
            }

            if (owner.BoundObject.GetComponent<AssetReleaseOnDestroy>() == null)
            {
                var component = owner.BoundObject.AddComponent<AssetReleaseOnDestroy>();
                component.Initialize(this);
            }
        }

        private sealed class AssetHandle<T> : IAssetHandle<T> where T : UnityEngine.Object
        {
            private readonly AssetRegistry.LoadedAsset _asset;

            public AssetHandle(AssetRegistry.LoadedAsset asset)
            {
                _asset = asset;
            }

            public string Key => _asset.Key;

            public bool IsValid => _asset.RefCount > 0 && _asset.Backend.IsValid;

            public T? Value => IsValid ? _asset.Backend.Asset as T : null;
        }

        private sealed class SceneHandle : ISceneHandle
        {
            private readonly IBackendScene _scene;

            public SceneHandle(string identity, IBackendScene scene)
            {
                Identity = identity;
                _scene = scene;
            }

            public string Identity { get; }

            public bool IsLoaded => _scene.IsLoaded;

            public string Name => _scene.Name;

            public GameObject[] GetRootGameObjects() => _scene.GetRootGameObjects();
        }
    }
}
