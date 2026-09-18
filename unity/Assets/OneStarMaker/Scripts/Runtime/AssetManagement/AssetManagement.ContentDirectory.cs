#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement
{
    public sealed partial class AssetManagement
    {
        private IContentDirectoryBackend? _contentDirectory;

        internal void InstallContentDirectory(IContentDirectoryBackend session)
        {
            if (_contentDirectory != null) throw new InvalidOperationException("A content directory session is already installed.");
            _contentDirectory = session ?? throw new ArgumentNullException(nameof(session));
            _contentDirectory.ConfigureCacheEviction(EvictContentDirectoryCache);
        }

        internal async UniTask CloseContentDirectoryAsync()
        {
            var directory = RequireContentDirectory();
            // Scene registry remains AssetManagement's only ownership ledger. Close asks the session to
            // unload registered directory scenes first, then lets the session reject still-live owners.
            foreach (var scene in _registry.GetScenes())
            {
                if (!scene.IsUnloaded && scene.Backend is IContentDirectoryToken)
                {
                    await directory.UnloadSceneAsync(scene.Backend);
                    _registry.MarkSceneUnloaded(scene.Identity);
                    ReleaseScene(scene.Identity);
                }
            }
            await directory.CloseAsync();
            _contentDirectory = null;
        }

        public async UniTask<IAssetHandle<T>> LoadContentAssetAsync<T>(string logicalKey, string representation, AssetOwner owner, CancellationToken ct = default) where T : UnityEngine.Object
        {
            var directory = RequireContentDirectory();
            var key = directory.GetObjectKey(logicalKey, representation);
            AssetRegistry.LoadedAsset loaded;
            if (_registry.TryGetAsset(key.Canonical, out var existing))
            {
                // 共有 token は最初の caller が指定した型を記憶しない。二人目も実 Object で検査する。
                var actual = existing.Backend.Asset;
                if (actual == null || !typeof(T).IsInstanceOfType(actual))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.TypeMismatch, "Loaded content does not match the requested type.", directory.BuildIdentity, directory.Target, logicalKey, representation);
                loaded = _registry.Acquire(key.Canonical, key, existing.Backend, owner, key.Type, false);
            }
            else if (_cache != null && _cache.TryTake(key.Canonical, out var cached))
            {
                var actual = cached.Asset;
                if (actual == null || !typeof(T).IsInstanceOfType(actual))
                {
                    directory.Release(cached);
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.TypeMismatch, "Cached content does not match the requested type.", directory.BuildIdentity, directory.Target, logicalKey, representation);
                }
                loaded = _registry.Acquire(key.Canonical, key, cached, owner, key.Type, false);
            }
            else
                loaded = _registry.Acquire(key.Canonical, key, await directory.LoadAssetAsync<T>(logicalKey, representation, ct), owner, key.Type, false);
            AttachDestroyReleaseIfNeeded(owner);
            return new AssetHandle<T>(loaded, owner);
        }

        public async UniTask<ISceneHandle> LoadContentSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options = default, CancellationToken ct = default)
        {
            var directory = RequireContentDirectory();
            if (_registry.TryGetScene(sceneIdentity, out var existing) && !existing.IsUnloaded) return new SceneHandle(sceneIdentity, existing.Backend);
            var scene = await directory.LoadSceneAsync(sceneIdentity, representation, options, ct);
            _registry.AddScene(sceneIdentity, scene);
            return new SceneHandle(sceneIdentity, scene);
        }

        public async UniTask<GameObject> InstantiateContentAsync(string logicalKey, string representation, Transform? parent = null, bool worldSpace = false, CancellationToken ct = default)
        {
            var directory = RequireContentDirectory();
            var backendInstance = await directory.InstantiateAsync(logicalKey, representation, parent, worldSpace, ct);
            var instance = backendInstance.Instance;
            if (instance == null)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed, "Content prefab instantiation returned null.", directory.BuildIdentity, directory.Target, logicalKey, representation);
            var owner = AssetOwner.Bind(instance);
            if (backendInstance is IBackendAsset backendAsset)
            {
                var source = directory.GetObjectKey(logicalKey, representation);
                _registry.Acquire(source.Canonical + ":instance:" + owner.GameObjectId, source, backendAsset, owner, AssetType.Prefab, true);
            }
            AttachDestroyReleaseIfNeeded(owner);
            return instance;
        }

        private IContentDirectoryBackend RequireContentDirectory()
            => _contentDirectory ?? throw new ContentDirectoryException(ContentDirectoryFailureCode.DirectoryNotRegistered, "No Content Directory session is installed.");

        private void EvictContentDirectoryCache()
        {
            if (_contentDirectory == null) return;
            _cache?.EvictByPrefix(_contentDirectory.CachePrefix);
        }
    }
}
