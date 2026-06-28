#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement.Components;
using OneStarMaker.Runtime.AssetManagement.Internal;
using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// アセットとシーンのロード寿命をスコープ付きで一元管理する。
    /// </summary>
    public sealed class AssetManagement : IAssetManagement
    {
        private readonly IAssetBackend _backend;
        private readonly AssetRegistry _registry = new();

        /// <summary>同一 key の並行ロードを 1 本の backend ロードに集約する in-flight テーブル。</summary>
        private readonly Dictionary<string, UniTask<IBackendAsset>> _inFlight = new(StringComparer.Ordinal);

        public AssetManagement()
            : this(new AddressableBackend())
        {
        }

        internal AssetManagement(IAssetBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public async UniTask<IAssetHandle<T>> LoadAssetAsync<T>(
            AssetKey key,
            AssetOwner owner,
            CancellationToken ct = default) where T : UnityEngine.Object
        {
            AssetRegistry.LoadedAsset loaded;
            if (_registry.TryGetAsset(key.Canonical, out var existing))
            {
                loaded = _registry.Acquire(key.Canonical, existing.Backend, owner);
            }
            else
            {
                var backendAsset = await LoadBackendAssetDedup<T>(key, ct);
                loaded = _registry.Acquire(key.Canonical, backendAsset, owner);
            }

            AttachDestroyReleaseIfNeeded(owner);
            return new AssetHandle<T>(loaded);
        }

        public IAssetHandle<T> LoadAppAssetSync<T>(AssetKey key) where T : UnityEngine.Object
        {
            AssetRegistry.LoadedAsset loaded;
            if (_registry.TryGetAsset(key.Canonical, out var existing))
            {
                loaded = _registry.Acquire(key.Canonical, existing.Backend, AssetOwner.App);
            }
            else
            {
                var backendAsset = _backend.LoadAssetSync<T>(key.Address);
                loaded = _registry.Acquire(key.Canonical, backendAsset, AssetOwner.App);
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
                _registry.Acquire(instanceKey, backendAsset, owner);
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

        public void ReleaseScene(string sceneIdentity)
        {
            if (_registry.TryGetScene(sceneIdentity, out var scene) && !scene.IsUnloaded)
            {
                // 未アンロード（Dispose 経路）なら、アンロード完了後に所有アセットを解放して順序を保証する。
                // シーン内 GO が生きたまま Addressables 参照を切ると MissingReference を招くため。
                UnloadThenReleaseSceneAsync(sceneIdentity, scene).Forget();
                return;
            }

            // 既にアンロード済み（通常の 3-Phase Phase3）なら同期で所有アセットのみ解放する。
            foreach (var backendAsset in _registry.ReleaseSceneOwned(sceneIdentity))
            {
                _backend.Release(backendAsset);
            }
        }

        private async UniTaskVoid UnloadThenReleaseSceneAsync(string sceneIdentity, AssetRegistry.LoadedScene scene)
        {
            await _backend.UnloadSceneAsync(scene.Backend, CancellationToken.None);
            _registry.MarkSceneUnloaded(sceneIdentity);
            foreach (var backendAsset in _registry.ReleaseSceneOwned(sceneIdentity))
            {
                _backend.Release(backendAsset);
            }
        }

        public void ReleaseAll()
        {
            var loadedScenes = new List<AssetRegistry.LoadedScene>();
            foreach (var scene in _registry.GetScenes())
            {
                if (!scene.IsUnloaded)
                {
                    loadedScenes.Add(scene);
                }
            }

            if (loadedScenes.Count > 0)
            {
                // ロード中シーンを全てアンロード完了させてから全アセットを解放する（順序保証）。
                // シーン内 GO が生きたまま Addressables 参照を切ると MissingReference を招くため。
                UnloadScenesThenReleaseAllAsync(loadedScenes).Forget();
                return;
            }

            // 既にシーンが全てアンロード済み、またはシーンが無い場合は同期で解放（従来挙動と等価）。
            ReleaseAllAssetsNow();
        }

        private async UniTaskVoid UnloadScenesThenReleaseAllAsync(List<AssetRegistry.LoadedScene> scenes)
        {
            foreach (var scene in scenes)
            {
                await _backend.UnloadSceneAsync(scene.Backend, CancellationToken.None);
                _registry.MarkSceneUnloaded(scene.Identity);
            }

            ReleaseAllAssetsNow();
        }

        private void ReleaseAllAssetsNow()
        {
            foreach (var backendAsset in _registry.ReleaseAllAssets())
            {
                _backend.Release(backendAsset);
            }
        }

        internal void NotifyGameObjectDestroyed(ulong gameObjectInstanceId)
        {
            foreach (var backendAsset in _registry.ReleaseGameObjectOwned(gameObjectInstanceId))
            {
                _backend.Release(backendAsset);
            }
        }

        internal int LoadedAssetCountForTests => _registry.AssetCount;

        private void ReleaseKey(string key)
        {
            if (_registry.Release(key, out var backendAsset) && backendAsset != null)
            {
                _backend.Release(backendAsset);
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
