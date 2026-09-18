#nullable enable

using System;
using System.Collections.Generic;
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
        private readonly SemaphoreSlim _contentAssetLoadGate = new(1, 1);
        private readonly SemaphoreSlim _contentSceneLoadGate = new(1, 1);
        private readonly Dictionary<string, string> _contentSceneRepresentations = new(StringComparer.Ordinal);

        internal void InstallContentDirectory(IContentDirectoryBackend session)
        {
            if (_contentDirectory != null) throw new InvalidOperationException("A content directory session is already installed.");
            _contentDirectory = session ?? throw new ArgumentNullException(nameof(session));
            _contentDirectory.ConfigureCacheEviction(EvictContentDirectoryCache);
        }

        internal async UniTask CloseContentDirectoryAsync()
        {
            var directory = RequireContentDirectory();
            await _contentAssetLoadGate.WaitAsync();
            await _contentSceneLoadGate.WaitAsync();
            try { await directory.StopAndDrainAsync(); }
            finally { _contentSceneLoadGate.Release(); _contentAssetLoadGate.Release(); }
            // native terminal より先に scene 台帳を列挙すると、遅れて成功した Scene が列挙から漏れる。
            // 受付停止と terminal 待ちの後、唯一の owner 台帳にある Scene を unload する。
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
            _contentSceneRepresentations.Clear();
        }

        public async UniTask<IAssetHandle<T>> LoadContentAssetAsync<T>(string logicalKey, string representation, AssetOwner owner, CancellationToken ct = default) where T : UnityEngine.Object
        {
            var directory = RequireContentDirectory();
            var key = directory.GetObjectKey(logicalKey, representation);
            // registry を確認してから native load と Acquire を終えるまで直列化する。
            // 同時 caller が別 token を発行すると、後着 token は台帳に採用されず解放不能になる。
            await _contentAssetLoadGate.WaitAsync(ct);
            try
            {
                directory.EnsureAccepting();
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
            finally { _contentAssetLoadGate.Release(); }
        }

        public async UniTask<ISceneHandle> LoadContentSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options = default, CancellationToken ct = default)
        {
            var directory = RequireContentDirectory();
            // activation を保留すると native operation が terminal に達せず、返却 handle も渡せない。
            // この公開入口では完了まで所有するため、保留要求は開始前に拒否する。
            if (!options.ActivateOnLoad)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                    "Deferred scene activation is not supported by content directory loads.", directory.BuildIdentity, directory.Target, sceneIdentity, representation);
            await _contentSceneLoadGate.WaitAsync(ct);
            try
            {
                directory.EnsureAccepting();
                if (_registry.TryGetScene(sceneIdentity, out var existing) && !existing.IsUnloaded)
                {
                    // Scene 台帳は identity 単位。異なる表現を同じ Scene と偽って返さない。
                    if (!_contentSceneRepresentations.TryGetValue(sceneIdentity, out var loadedRepresentation)
                        || !string.Equals(loadedRepresentation, representation, StringComparison.Ordinal))
                        throw new ContentDirectoryException(ContentDirectoryFailureCode.EntryAmbiguous,
                            "Scene identity is already loaded with a different representation.", directory.BuildIdentity, directory.Target, sceneIdentity, representation);
                    return new SceneHandle(sceneIdentity, existing.Backend);
                }
                var scene = await directory.LoadSceneAsync(sceneIdentity, representation, options, ct);
                _registry.AddScene(sceneIdentity, scene);
                _contentSceneRepresentations[sceneIdentity] = representation;
                return new SceneHandle(sceneIdentity, scene);
            }
            finally { _contentSceneLoadGate.Release(); }
        }

        public async UniTask<GameObject> InstantiateContentAsync(string logicalKey, string representation, Transform? parent = null, bool worldSpace = false, CancellationToken ct = default)
        {
            var directory = RequireContentDirectory();
            var backendInstance = await directory.InstantiateAsync(logicalKey, representation, parent, worldSpace, ct);
            var instance = backendInstance.Instance;
            if (instance == null)
            {
                // session は Instantiate の成功時点で live token を発行済み。GameObject が
                // 得られなくても token を返してから失敗を報告する。
                if (backendInstance is IBackendAsset failedAsset) directory.Release(failedAsset);
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed, "Content prefab instantiation returned null.", directory.BuildIdentity, directory.Target, logicalKey, representation);
            }
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
