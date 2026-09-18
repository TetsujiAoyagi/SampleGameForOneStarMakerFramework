#nullable enable

using System;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using Unity.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.BuildContent
{
    // Unity の非同期処理は caller の取消後にも完了する。session はこの port を通じて
    // terminal を観測し、遅れて届く成功 handle を必ず解放してから unregister する。
    internal interface IContentNativeDirectory
    {
        bool Register(string path);
        BuildContentRoot[] GetRoots();
        void Unregister();
        UniTask<IBackendAsset> LoadObjectAsync<T>(BuildContentEntry entry) where T : UnityEngine.Object;
        UniTask<IBackendScene> LoadSceneAsync(BuildContentEntry entry, SceneLoadOptions options);
        UniTask<IBackendInstance> InstantiateAsync(BuildContentEntry entry, Transform? parent, bool worldSpace);
        void Release(IBackendAsset asset);
        UniTask UnloadSceneAsync(IBackendScene scene);
    }

    /// <summary>native Loadable と SceneManager の handle を token 化し、Release の順序を session へ返す。</summary>
    internal sealed class UnityContentDirectoryBackend : IContentNativeDirectory
    {
        private ContentDirectoryHandle _handle;
        public bool Register(string path) { _handle = ContentLoadManager.RegisterContentDirectory(path); return _handle.IsValid; }
        public BuildContentRoot[] GetRoots() => ContentLoadManager.GetRootAssets<BuildContentRoot>(_handle);
        public void Unregister() { if (_handle.IsValid) ContentLoadManager.UnregisterContentDirectory(_handle); _handle = default; }

        public async UniTask<IBackendAsset> LoadObjectAsync<T>(BuildContentEntry entry) where T : UnityEngine.Object
        {
            var loadable = entry.Object ?? throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Object entry has no loadable locator.");
            var loaded = await loadable.LoadAsync();
            if (loaded == null || !typeof(T).IsInstanceOfType(loaded))
            {
                loadable.Release();
                throw new ContentDirectoryException(ContentDirectoryFailureCode.TypeMismatch, "Loaded content does not match the requested type.", logicalKey: entry.LogicalKey, representation: entry.Representation);
            }
            return new DirectoryAsset(loadable, loaded);
        }

        public async UniTask<IBackendScene> LoadSceneAsync(BuildContentEntry entry, SceneLoadOptions options)
        {
            var operation = SceneManager.LoadSceneAsync(entry.SceneId, new LoadSceneParameters(options.LoadMode));
            if (operation == null) throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed, "Content scene load did not start.", logicalKey: entry.LogicalKey, representation: entry.Representation);
            operation.allowSceneActivation = options.ActivateOnLoad;
            await operation;
            var scene = SceneManager.GetSceneByLoadableSceneId(entry.SceneId);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed, "Content scene did not become loaded.", logicalKey: entry.LogicalKey, representation: entry.Representation);
            return new DirectoryScene(scene);
        }

        public async UniTask<IBackendInstance> InstantiateAsync(BuildContentEntry entry, Transform? parent, bool worldSpace)
        {
            var asset = await LoadObjectAsync<GameObject>(entry);
            var prefab = asset.Asset as GameObject;
            if (prefab == null) { Release(asset); throw new ContentDirectoryException(ContentDirectoryFailureCode.TypeMismatch, "Content entry is not a prefab.", logicalKey: entry.LogicalKey, representation: entry.Representation); }
            return new DirectoryInstance(UnityEngine.Object.Instantiate(prefab, parent, worldSpace), asset);
        }

        public void Release(IBackendAsset asset)
        {
            if (asset is DirectoryAsset directoryAsset) directoryAsset.Release();
            else if (asset is DirectoryInstance instance) instance.Release();
        }

        public async UniTask UnloadSceneAsync(IBackendScene scene)
        {
            if (scene is not DirectoryScene directoryScene || !directoryScene.Scene.IsValid() || !directoryScene.Scene.isLoaded) return;
            var operation = SceneManager.UnloadSceneAsync(directoryScene.Scene);
            if (operation != null) await operation;
            directoryScene.MarkUnloaded();
        }

        private sealed class DirectoryAsset : IBackendAsset
        {
            private Loadable<UnityEngine.Object>? _loadable;
            private UnityEngine.Object? _asset;
            internal DirectoryAsset(Loadable<UnityEngine.Object> loadable, UnityEngine.Object asset) { _loadable=loadable; _asset=asset; }
            public UnityEngine.Object? Asset => _asset;
            public bool IsValid => _loadable != null && _asset != null;
            internal void Release() { if (_loadable != null) _loadable.Release(); _loadable=null; _asset=null; }
        }
        private sealed class DirectoryInstance : IBackendInstance, IBackendAsset
        {
            private IBackendAsset? _source;
            internal DirectoryInstance(GameObject instance, IBackendAsset source) { Instance=instance; _source=source; }
            public GameObject? Instance { get; private set; }
            public UnityEngine.Object? Asset => Instance;
            public bool IsValid => _source != null && Instance != null;
            internal void Release()
            {
                // GameObject は呼出側または Scene が所有する。破棄通知の後に元 prefab の
                // loadable token を返すだけで、backend が GameObject を先回りして Destroy しない。
                Instance = null;
                if (_source is DirectoryAsset source) source.Release();
                _source = null;
            }
        }
        private sealed class DirectoryScene : IBackendScene
        {
            internal DirectoryScene(Scene scene) => Scene=scene;
            internal Scene Scene { get; }
            private bool _unloaded;
            public bool IsLoaded => !_unloaded && Scene.IsValid() && Scene.isLoaded;
            public string Name => Scene.name;
            public GameObject[] GetRootGameObjects() => IsLoaded ? Scene.GetRootGameObjects() : Array.Empty<GameObject>();
            internal void MarkUnloaded()=>_unloaded=true;
        }
    }
}
