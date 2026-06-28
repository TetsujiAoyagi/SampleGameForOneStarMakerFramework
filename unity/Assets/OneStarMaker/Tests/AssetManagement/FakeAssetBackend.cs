#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using UnityEngine;

namespace OneStarMaker.Tests.AssetManagement
{
    /// <summary>
    /// Addressables に依存しない AssetManagement テスト用 backend。
    /// </summary>
    internal sealed class FakeAssetBackend : IAssetBackend
    {
        private readonly Dictionary<string, GameObject[]> _sceneRoots = new(StringComparer.Ordinal);

        public int ReleaseCallCount { get; private set; }

        public int LoadAssetCallCount { get; private set; }

        public int LoadSceneCallCount { get; private set; }

        public int UnloadSceneCallCount { get; private set; }

        public IReadOnlyList<IBackendAsset> ReleasedAssets => _releasedAssets;

        private readonly List<IBackendAsset> _releasedAssets = new();

        public void SetSceneRoots(string address, params GameObject[] roots)
        {
            _sceneRoots[address] = roots;
        }

        public UniTask<IBackendAsset> LoadAssetAsync<T>(string address, CancellationToken ct) where T : UnityEngine.Object
        {
            LoadAssetCallCount++;
            return UniTask.FromResult<IBackendAsset>(new FakeAsset(CreateObject(address, typeof(T))));
        }

        public IBackendAsset LoadAssetSync<T>(string address) where T : UnityEngine.Object
        {
            LoadAssetCallCount++;
            return new FakeAsset(CreateObject(address, typeof(T)));
        }

        public UniTask<IBackendScene> LoadSceneAsync(string address, SceneLoadOptions options, CancellationToken ct)
        {
            LoadSceneCallCount++;
            var roots = _sceneRoots.TryGetValue(address, out var value) ? value : Array.Empty<GameObject>();
            return UniTask.FromResult<IBackendScene>(new FakeScene(address, roots));
        }

        public UniTask UnloadSceneAsync(IBackendScene scene, CancellationToken ct)
        {
            UnloadSceneCallCount++;
            if (scene is FakeScene fakeScene)
            {
                fakeScene.Unload();
            }

            ReleaseCallCount++;
            return UniTask.CompletedTask;
        }

        public UniTask<IBackendInstance> InstantiateAsync(
            string address,
            Transform? parent,
            bool worldSpace,
            CancellationToken ct)
        {
            var go = new GameObject($"FakeInstance_{address}");
            if (parent != null)
            {
                go.transform.SetParent(parent, worldSpace);
            }

            return UniTask.FromResult<IBackendInstance>(new FakeInstance(go));
        }

        public void Release(IBackendAsset asset)
        {
            ReleaseCallCount++;
            _releasedAssets.Add(asset);
            if (asset is IReleasableFake releasable)
            {
                releasable.Release();
            }
        }

        private static UnityEngine.Object? CreateObject(string address, Type assetType)
        {
            if (assetType == typeof(GameObject) || assetType.IsSubclassOf(typeof(GameObject)))
            {
                return new GameObject($"FakeAsset_{address}");
            }

            if (assetType == typeof(TextAsset))
            {
                return new TextAsset("{}");
            }

            if (assetType == typeof(Texture2D))
            {
                return new Texture2D(1, 1);
            }

            if (typeof(ScriptableObject).IsAssignableFrom(assetType))
            {
                return ScriptableObject.CreateInstance(assetType);
            }

            return null;
        }

        private interface IReleasableFake
        {
            void Release();
        }

        private sealed class FakeAsset : IBackendAsset, IReleasableFake
        {
            private bool _released;

            public FakeAsset(UnityEngine.Object? asset)
            {
                Asset = asset;
            }

            public UnityEngine.Object? Asset { get; }

            public bool IsValid => !_released;

            public void Release() => _released = true;
        }

        private sealed class FakeInstance : IBackendInstance, IBackendAsset, IReleasableFake
        {
            private bool _released;

            public FakeInstance(GameObject instance)
            {
                Instance = instance;
            }

            public GameObject? Instance { get; private set; }

            public UnityEngine.Object? Asset => Instance;

            public bool IsValid => !_released && Instance != null;

            public void Release()
            {
                _released = true;
                Instance = null;
            }
        }

        private sealed class FakeScene : IBackendScene
        {
            private bool _loaded = true;
            private readonly GameObject[] _roots;

            public FakeScene(string name, GameObject[] roots)
            {
                Name = name;
                _roots = roots;
            }

            public bool IsLoaded => _loaded;

            public string Name { get; }

            public GameObject[] GetRootGameObjects() => _loaded ? _roots : Array.Empty<GameObject>();

            public void Unload() => _loaded = false;
        }
    }
}
