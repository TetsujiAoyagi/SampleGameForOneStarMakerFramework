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
            // 通常 gameplay Unload の防衛:
            // Scene.IsValid だけ見ると、Play Mode 終了直後などで
            // Unity Scene はまだ Valid に見えても Addressables の handle マップが消えていることがある。
            // その状態で UnloadSceneAsync(SceneInstance) すると「Cannot find handle for scene」になる。
            // ロード時に保持した AsyncOperationHandle の IsValid と isLoaded を見てから Unload する。
            if (scene is not AddressableScene addressableScene || !addressableScene.CanUnloadViaAddressables)
            {
                return;
            }

            // SceneInstance 経由より handle 経由の方が Addressables 内部対応と一致しやすい。
            var unloadHandle = Addressables.UnloadSceneAsync(
                addressableScene.Handle,
                UnloadSceneOptions.None);
            await AwaitHandle(unloadHandle, ct);
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

            /// <summary>ロード時の Addressables ハンドル。Unload はこれを正とする。</summary>
            public AsyncOperationHandle<SceneInstance> Handle => _handle;

            public SceneInstance SceneInstance => _handle.Result;

            public bool IsLoaded => _handle.IsValid() && SceneInstance.Scene.isLoaded;

            /// <summary>
            /// Addressables.UnloadSceneAsync を安全に呼べるか。
            /// handle が無効、または Unity Scene が既に unloaded なら false。
            /// </summary>
            public bool CanUnloadViaAddressables =>
                _handle.IsValid()
                && SceneInstance.Scene.IsValid()
                && SceneInstance.Scene.isLoaded;

            public string Name =>
                _handle.IsValid() && SceneInstance.Scene.IsValid()
                    ? SceneInstance.Scene.name
                    : string.Empty;

            public GameObject[] GetRootGameObjects()
            {
                return _handle.IsValid() && SceneInstance.Scene.IsValid()
                    ? SceneInstance.Scene.GetRootGameObjects()
                    : Array.Empty<GameObject>();
            }
        }
    }
}
