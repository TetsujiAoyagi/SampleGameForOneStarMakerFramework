#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.AssetManagement.Internal
{
    /// <summary>
    /// <see cref="IAssetBackend"/> の Unity Addressables 本番実装。
    /// Runtime アセンブリ内で <c>Addressables.</c> を呼ぶ唯一のクラス。
    /// </summary>
    internal sealed class AddressableBackend : IAssetBackend
    {
        public async UniTask<IBackendAsset> LoadAssetAsync<T>(string address, CancellationToken ct)
            where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetAsync<T>(address);
            await AwaitHandle(handle, ct);
            return new AddressableAsset(handle);
        }

        public IBackendAsset LoadAssetSync<T>(string address) where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetAsync<T>(address);
            handle.WaitForCompletion();
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                throw handle.OperationException
                    ?? new InvalidOperationException($"Addressables load failed: {address}");
            }

            return new AddressableAsset(handle);
        }

        public async UniTask<IBackendScene> LoadSceneAsync(
            string address,
            SceneLoadOptions options,
            CancellationToken ct)
        {
            var handle = Addressables.LoadSceneAsync(
                address,
                options.LoadMode,
                options.ActivateOnLoad,
                options.Priority);
            await AwaitHandle(handle, ct);
            return new AddressableScene(handle);
        }

        public async UniTask UnloadSceneAsync(IBackendScene scene, CancellationToken ct)
        {
            if (scene is not AddressableScene addressableScene || !addressableScene.SceneInstance.Scene.IsValid())
            {
                return;
            }

            var handle = Addressables.UnloadSceneAsync(
                addressableScene.SceneInstance,
                UnloadSceneOptions.None);
            await AwaitHandle(handle, ct);
        }

        public async UniTask<IBackendInstance> InstantiateAsync(
            string address,
            Transform? parent,
            bool worldSpace,
            CancellationToken ct)
        {
            var handle = Addressables.InstantiateAsync(address, parent, worldSpace);
            await AwaitHandle(handle, ct);
            return new AddressableInstance(handle);
        }

        public void Release(IBackendAsset asset)
        {
            if (asset is AddressableAsset addressableAsset && addressableAsset.Handle.IsValid())
            {
                Addressables.Release(addressableAsset.Handle);
            }
            else if (asset is AddressableInstance addressableInstance && addressableInstance.Instance != null)
            {
                Addressables.ReleaseInstance(addressableInstance.Instance);
            }
        }

        private static async UniTask AwaitHandle<T>(AsyncOperationHandle<T> handle, CancellationToken ct)
        {
            await handle.ToUniTask(cancellationToken: ct);
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                throw handle.OperationException
                    ?? new InvalidOperationException("Addressables operation failed.");
            }
        }

        private sealed class AddressableAsset : IBackendAsset
        {
            public AddressableAsset(AsyncOperationHandle handle)
            {
                Handle = handle;
            }

            /// <summary>ジェネリックハンドルから暗黙変換した非ジェネリックハンドル。</summary>
            public AsyncOperationHandle Handle { get; }

            public UnityEngine.Object? Asset => Handle.IsValid() ? Handle.Result as UnityEngine.Object : null;

            public bool IsValid => Handle.IsValid() && Asset != null;
        }

        private sealed class AddressableInstance : IBackendInstance, IBackendAsset
        {
            private readonly AsyncOperationHandle<GameObject> _handle;

            public AddressableInstance(AsyncOperationHandle<GameObject> handle)
            {
                _handle = handle;
            }

            public GameObject? Instance => _handle.IsValid() ? _handle.Result : null;

            public UnityEngine.Object? Asset => Instance;

            public bool IsValid => _handle.IsValid() && Instance != null;
        }

        private sealed class AddressableScene : IBackendScene
        {
            private readonly AsyncOperationHandle<SceneInstance> _handle;

            public AddressableScene(AsyncOperationHandle<SceneInstance> handle)
            {
                _handle = handle;
            }

            public SceneInstance SceneInstance => _handle.Result;

            public bool IsLoaded => _handle.IsValid() && SceneInstance.Scene.isLoaded;

            public string Name => SceneInstance.Scene.name;

            public GameObject[] GetRootGameObjects()
            {
                return SceneInstance.Scene.IsValid()
                    ? SceneInstance.Scene.GetRootGameObjects()
                    : Array.Empty<GameObject>();
            }
        }
    }
}
