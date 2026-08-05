#nullable enable

using System;
using System.Collections.Generic;

namespace OneStarMaker.Runtime.AssetManagement.Internal
{
    /// <summary>
    /// ロード済みアセットと所有者を追跡する唯一の台帳。
    /// </summary>
    internal sealed class AssetRegistry
    {
        internal sealed class LoadedAsset
        {
            public LoadedAsset(string key, IBackendAsset backend, AssetType type, bool isInstance)
            {
                Key = key;
                Backend = backend;
                Type = type;
                IsInstance = isInstance;
                RefCount = 1;
                Owners = new List<AssetOwner>();
            }

            public string Key { get; }
            public IBackendAsset Backend { get; }
            public AssetType Type { get; }
            public bool IsInstance { get; }
            public int RefCount { get; set; }
            public List<AssetOwner> Owners { get; }
        }

        internal sealed class LoadedScene
        {
            public LoadedScene(string identity, IBackendScene backend)
            {
                Identity = identity;
                Backend = backend;
            }

            public string Identity { get; }
            public IBackendScene Backend { get; }
            public bool IsUnloaded { get; set; }
        }

        private readonly Dictionary<string, LoadedAsset> _assets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LoadedScene> _scenes = new(StringComparer.Ordinal);

        // 所有回数ぶんの Release を保証するため、重複を許容する List で保持する（_goOwned と同構造）。
        private readonly Dictionary<string, List<string>> _sceneOwned = new(StringComparer.Ordinal);
        private readonly Dictionary<ulong, List<string>> _goOwned = new();

        public int AssetCount => _assets.Count;

        public bool TryGetAsset(string key, out LoadedAsset asset) => _assets.TryGetValue(key, out asset!);

        public LoadedAsset AddAsset(string key, IBackendAsset backend, AssetType type, bool isInstance)
        {
            var loaded = new LoadedAsset(key, backend, type, isInstance);
            _assets.Add(key, loaded);
            return loaded;
        }

        public LoadedAsset Acquire(
            string key,
            IBackendAsset backend,
            AssetOwner owner,
            AssetType type,
            bool isInstance)
        {
            if (_assets.TryGetValue(key, out var loaded))
            {
                loaded.RefCount++;
            }
            else
            {
                loaded = AddAsset(key, backend, type, isInstance);
            }

            TrackOwner(owner, key);
            loaded.Owners.Add(owner);
            return loaded;
        }

        public bool Release(string key, AssetOwner owner, out LoadedAsset? loaded)
        {
            loaded = null;
            if (!_assets.TryGetValue(key, out var asset))
            {
                return false;
            }

            asset.Owners.Remove(owner);
            asset.RefCount--;
            if (asset.RefCount > 0)
            {
                return false;
            }

            _assets.Remove(key);
            loaded = asset;
            return true;
        }

        public IReadOnlyList<LoadedAsset> ReleaseSceneOwned(string sceneIdentity)
        {
            var released = new List<LoadedAsset>();
            var owner = AssetOwner.Scene(sceneIdentity);
            if (_sceneOwned.TryGetValue(sceneIdentity, out var keys))
            {
                foreach (var key in keys)
                {
                    if (Release(key, owner, out var loaded) && loaded != null)
                    {
                        released.Add(loaded);
                    }
                }
                _sceneOwned.Remove(sceneIdentity);
            }

            _scenes.Remove(sceneIdentity);
            return released;
        }

        public IReadOnlyList<LoadedAsset> ReleaseGameObjectOwned(ulong gameObjectId)
        {
            var released = new List<LoadedAsset>();
            if (!_goOwned.TryGetValue(gameObjectId, out var keys))
            {
                return released;
            }

            var owner = AssetOwner.FromGameObjectId(gameObjectId);
            foreach (var key in keys)
            {
                if (Release(key, owner, out var loaded) && loaded != null)
                {
                    released.Add(loaded);
                }
            }

            _goOwned.Remove(gameObjectId);
            return released;
        }

        public IReadOnlyList<LoadedAsset> ReleaseAllAssets()
        {
            var released = new List<LoadedAsset>(_assets.Count);
            foreach (var pair in _assets)
            {
                released.Add(pair.Value);
            }

            _assets.Clear();
            _sceneOwned.Clear();
            _goOwned.Clear();
            _scenes.Clear();
            return released;
        }

        public IReadOnlyList<LoadedScene> GetScenes()
        {
            return new List<LoadedScene>(_scenes.Values);
        }

        public void AddScene(string identity, IBackendScene backend)
        {
            _scenes[identity] = new LoadedScene(identity, backend);
        }

        public bool TryGetScene(string identity, out LoadedScene scene) => _scenes.TryGetValue(identity, out scene!);

        internal IReadOnlyList<LoadedAsset> GetAllLoadedAssets()
        {
            return new List<LoadedAsset>(_assets.Values);
        }

        public void MarkSceneUnloaded(string identity)
        {
            if (_scenes.TryGetValue(identity, out var scene))
            {
                scene.IsUnloaded = true;
            }
        }

        private void TrackOwner(AssetOwner owner, string key)
        {
            switch (owner.Kind)
            {
                case AssetOwnerKind.App:
                    // App スコープの解放は ReleaseAll（全 backend を 1 回ずつ Release）が担うため所有追跡は不要。
                    break;
                case AssetOwnerKind.Scene:
                    if (!_sceneOwned.TryGetValue(owner.Id, out var sceneKeys))
                    {
                        sceneKeys = new List<string>();
                        _sceneOwned[owner.Id] = sceneKeys;
                    }
                    sceneKeys.Add(key);
                    break;
                case AssetOwnerKind.GameObject:
                    if (!_goOwned.TryGetValue(owner.GameObjectId, out var goKeys))
                    {
                        goKeys = new List<string>();
                        _goOwned[owner.GameObjectId] = goKeys;
                    }
                    goKeys.Add(key);
                    break;
                case AssetOwnerKind.Manual:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(owner));
            }
        }
    }
}
