#nullable enable

using System;
using Unity.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CD0Spike
{
    internal sealed class Cd0ContentSession
    {
        private ContentDirectoryHandle _directory;
        private Loadable<Cd0ProbeAsset>? _asset;
        private LoadableSceneId _sceneId;
        private Scene _scene;
        private bool _registered;
        private bool _assetIssued;
        private bool _sceneIssued;
        private bool _sceneLoaded;

        internal Cd0Root RegisterAndGetRoot(string directoryPath)
        {
            if (_registered) throw new InvalidOperationException("A content directory is already registered.");
            _directory = ContentLoadManager.RegisterContentDirectory(directoryPath);
            if (!_directory.IsValid) throw new InvalidOperationException("Content directory registration returned an invalid handle.");
            _registered = true;

            var roots = ContentLoadManager.GetRootAssets<Cd0Root>(_directory);
            if (roots.Length != 1 || roots[0] == null)
            {
                throw new InvalidOperationException($"Expected exactly one {nameof(Cd0Root)}; found {roots.Length}.");
            }
            return roots[0];
        }

        internal async Awaitable<Cd0ProbeAsset> LoadAssetAsync(Cd0Root root)
        {
            EnsureRegistered();
            if (_assetIssued) throw new InvalidOperationException("Probe asset load was already issued.");
            _asset = root.ProbeAsset;
            _assetIssued = true;
            var asset = await _asset.LoadAsync();
            if (asset == null) throw new InvalidOperationException("Probe asset load returned null.");
            return asset;
        }

        internal async Awaitable<Scene> LoadSceneAsync(Cd0Root root)
        {
            EnsureRegistered();
            if (_sceneIssued) throw new InvalidOperationException("Probe scene load was already issued.");
            _sceneId = root.PayloadScene;
            _sceneIssued = true;
            var operation = SceneManager.LoadSceneAsync(_sceneId, new LoadSceneParameters(LoadSceneMode.Additive));
            if (operation == null) throw new InvalidOperationException("Scene load did not return an operation.");
            await operation;
            _scene = SceneManager.GetSceneByLoadableSceneId(_sceneId);
            if (!_scene.IsValid() || !_scene.isLoaded) throw new InvalidOperationException("Loaded content scene could not be resolved.");
            _sceneLoaded = true;
            return _scene;
        }

        internal async Awaitable CleanupAsync()
        {
            if (_sceneIssued && !_sceneLoaded)
            {
                _scene = SceneManager.GetSceneByLoadableSceneId(_sceneId);
                _sceneLoaded = _scene.IsValid() && _scene.isLoaded;
            }
            if (_sceneLoaded)
            {
                var operation = SceneManager.UnloadSceneAsync(_scene);
                if (operation == null) throw new InvalidOperationException("Scene unload did not return an operation.");
                await operation;
                _sceneLoaded = false;
                _sceneIssued = false;
                _sceneId = default;
                _scene = default;
            }

            if (_assetIssued)
            {
                _asset!.Release();
                _assetIssued = false;
                _asset = null;
            }

            if (_registered)
            {
                ContentLoadManager.UnregisterContentDirectory(_directory);
                _registered = false;
                _directory = default;
            }
        }

        private void EnsureRegistered()
        {
            if (!_registered) throw new InvalidOperationException("No content directory is registered by this session.");
        }
    }
}
