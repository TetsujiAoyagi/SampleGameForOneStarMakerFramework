#nullable enable

using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using Unity.Loading;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>一つの登録 directory の root、native handle、発行済み token と unregister を所有する。</summary>
    internal sealed class ContentDirectorySession : IContentDirectoryBackend
    {
        private readonly string _path;
        private readonly IDisposable _reservation;
        private readonly bool _registered;
        private readonly ContentDirectoryIndex _index;
        private readonly IContentNativeDirectory _backend;
        private int _liveTokens;
        private int _pendingOperations;
        private UniTaskCompletionSource? _operationsDrained;
        private bool _accepting = true;
        private bool _closed;
        private Action? _evictCache;

        private ContentDirectorySession(string path, string identity, string target, IDisposable reservation,
            ContentDirectoryIndex index, IContentNativeDirectory backend)
        { _path=path; BuildIdentity=identity; Target=target; _reservation=reservation; _registered=true; _index=index; _backend=backend; }

        public string BuildIdentity { get; }
        public string Target { get; }
        public string CachePrefix => ContentDirectoryIndex.CachePrefix(_path, BuildIdentity, Target);

        public AssetKey GetObjectKey(string logicalKey, string representation)
        {
            var entry = _index.ResolveObject(logicalKey, representation);
            return AssetKey.FromContentDirectory(ContentDirectoryIndex.CacheKey(_path, BuildIdentity, Target, entry), entry.Category);
        }

        public void ConfigureCacheEviction(Action evictRevisionEntries) => _evictCache = evictRevisionEntries;

        internal static ContentDirectorySession Register(string path, string identity, string target)
            => Register(path, identity, target, new UnityContentDirectoryBackend());

        internal static ContentDirectorySession Register(string path, string identity, string target, IContentNativeDirectory backend)
        {
            if (!Path.IsPathRooted(path) || !Directory.Exists(path))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration, "Content directory path must be an existing absolute directory.", identity, target);
            var normalized = Path.GetFullPath(path);
            var reservation = ContentRevisionGate.Reserve(identity, target, normalized);
            var registered = false;
            try
            {
                registered = backend.Register(normalized);
                if (!registered) throw new ContentDirectoryException(ContentDirectoryFailureCode.RegistrationFailed, "Content directory registration returned an invalid handle.", identity, target);
                var roots = backend.GetRoots();
                if (roots.Length != 1 || roots[0] == null)
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Content directory must contain exactly one BuildContentRoot.", identity, target);
                return new ContentDirectorySession(normalized, identity, target, reservation, ContentDirectoryIndex.Create(roots[0], identity, target), backend);
            }
            catch (Exception ex)
            {
                // unregister 自体が失敗しても process 内の予約を閉じ込めず、同じ revision の再試行を許す。
                try { if (registered) backend.Unregister(); }
                finally { reservation.Dispose(); }
                if (ex is ContentDirectoryException) throw;
                throw new ContentDirectoryException(ContentDirectoryFailureCode.RegistrationFailed, "Content directory registration failed.", identity, target, innerException: ex);
            }
        }

        public async UniTask<IBackendAsset> LoadAssetAsync<T>(string logicalKey, string representation, CancellationToken ct) where T : UnityEngine.Object
        {
            EnsureAccepting(); ct.ThrowIfCancellationRequested();
            BeginOperation();
            var terminalCleanupOwnsCounter = false;
            try
            {
                // 呼出側の取消は Unity のロード自体を止めない。遅れて成功した資源を回収するまで
                // directory を unregister すると、native handle の寿命が逆転する。
                var operation = _backend.LoadObjectAsync<T>(_index.ResolveObject(logicalKey, representation)).Preserve();
                IBackendAsset asset;
                try { asset = await operation.AttachExternalCancellation(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                { terminalCleanupOwnsCounter = true; DrainAbandonedAsset(operation).Forget(); throw; }
                Interlocked.Increment(ref _liveTokens);
                if (ct.IsCancellationRequested) { _backend.Release(asset); Interlocked.Decrement(ref _liveTokens); ct.ThrowIfCancellationRequested(); }
                return new SessionAsset(asset, ReleaseToken);
            }
            finally { if (!terminalCleanupOwnsCounter) CompleteOperation(); }
        }

        public async UniTask<IBackendScene> LoadSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options, CancellationToken ct)
        {
            EnsureAccepting(); ct.ThrowIfCancellationRequested();
            BeginOperation();
            var terminalCleanupOwnsCounter = false;
            try
            {
                var operation = _backend.LoadSceneAsync(_index.ResolveScene(sceneIdentity, representation), options).Preserve();
                IBackendScene scene;
                try { scene = await operation.AttachExternalCancellation(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                { terminalCleanupOwnsCounter = true; DrainAbandonedScene(operation).Forget(); throw; }
                if (ct.IsCancellationRequested)
                {
                    await _backend.UnloadSceneAsync(scene);
                    ct.ThrowIfCancellationRequested();
                }
                Interlocked.Increment(ref _liveTokens); return new SessionScene(scene, _backend, ReleaseToken);
            }
            finally { if (!terminalCleanupOwnsCounter) CompleteOperation(); }
        }

        public async UniTask<IBackendInstance> InstantiateAsync(string logicalKey, string representation, Transform? parent, bool worldSpace, CancellationToken ct)
        {
            EnsureAccepting(); ct.ThrowIfCancellationRequested();
            BeginOperation();
            var terminalCleanupOwnsCounter = false;
            try
            {
                var operation = _backend.InstantiateAsync(_index.ResolveObject(logicalKey, representation), parent, worldSpace).Preserve();
                IBackendInstance instance;
                try { instance = await operation.AttachExternalCancellation(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                { terminalCleanupOwnsCounter = true; DrainAbandonedInstance(operation).Forget(); throw; }
                if (ct.IsCancellationRequested)
                {
                    if (instance is IBackendAsset abandoned) _backend.Release(abandoned);
                    ct.ThrowIfCancellationRequested();
                }
                Interlocked.Increment(ref _liveTokens); return new SessionInstance(instance, _backend, ReleaseToken);
            }
            finally { if (!terminalCleanupOwnsCounter) CompleteOperation(); }
        }

        public async UniTask CloseAsync()
        {
            _accepting=false;
            var drained = _operationsDrained;
            if (Volatile.Read(ref _pendingOperations) != 0 && drained != null) await drained.Task;
            _evictCache?.Invoke();
            if (Volatile.Read(ref _liveTokens) != 0)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.ResourcesInUse, "Content directory still has live asset, scene, cache, or instance tokens.", BuildIdentity, Target);
            CloseNow();
        }

        public void BeginSynchronousShutdown() { _accepting=false; if (Volatile.Read(ref _liveTokens)==0 && Volatile.Read(ref _pendingOperations)==0) CloseNow(); }

        private void EnsureAccepting()
        {
            if (!_accepting || _closed) throw new ContentDirectoryException(ContentDirectoryFailureCode.DirectoryNotRegistered, "Content directory is not accepting new loads.", BuildIdentity, Target);
        }
        private async UniTaskVoid DrainAbandonedAsset(UniTask<IBackendAsset> operation)
        { try { _backend.Release(await operation); } catch (Exception) { } finally { CompleteOperation(); } }
        private async UniTaskVoid DrainAbandonedScene(UniTask<IBackendScene> operation)
        { try { await _backend.UnloadSceneAsync(await operation); } catch (Exception) { } finally { CompleteOperation(); } }
        private async UniTaskVoid DrainAbandonedInstance(UniTask<IBackendInstance> operation)
        { try { if (await operation is IBackendAsset asset) _backend.Release(asset); } catch (Exception) { } finally { CompleteOperation(); } }
        private void BeginOperation()
        {
            if (Interlocked.Increment(ref _pendingOperations) == 1) _operationsDrained = new UniTaskCompletionSource();
        }
        private void CompleteOperation()
        {
            if (Interlocked.Decrement(ref _pendingOperations) != 0) return;
            _operationsDrained?.TrySetResult();
            if (!_accepting && Volatile.Read(ref _liveTokens) == 0) CloseNow();
        }
        private void ReleaseToken() { if (Interlocked.Decrement(ref _liveTokens)==0 && !_accepting && Volatile.Read(ref _pendingOperations)==0) CloseNow(); }
        private void CloseNow()
        {
            if (_closed) return;
            _closed=true;
            try { if (_registered) _backend.Unregister(); }
            finally { _reservation.Dispose(); }
        }

        private sealed class SessionAsset : IBackendAsset, IContentDirectoryToken
        { private IBackendAsset? _inner; private readonly Action _release; internal SessionAsset(IBackendAsset inner, Action release){_inner=inner;_release=release;} public UnityEngine.Object? Asset=>_inner?.Asset; public bool IsValid=>_inner?.IsValid==true; internal void Release(IContentNativeDirectory backend){if(_inner!=null){backend.Release(_inner);_inner=null;_release();}} }
        private sealed class SessionInstance : IBackendInstance, IBackendAsset, IContentDirectoryToken
        { private IBackendInstance? _inner; private readonly IContentNativeDirectory _backend; private readonly Action _release; internal SessionInstance(IBackendInstance inner,IContentNativeDirectory backend,Action release){_inner=inner;_backend=backend;_release=release;} public GameObject? Instance=>_inner?.Instance; public UnityEngine.Object? Asset=>Instance; public bool IsValid=>_inner is IBackendAsset a && a.IsValid; internal void Release(){if(_inner is IBackendAsset a)_backend.Release(a);_inner=null;_release();} }
        private sealed class SessionScene : IBackendScene, IContentDirectoryToken
        { private IBackendScene? _inner; private readonly IContentNativeDirectory _backend; private readonly Action _release; internal SessionScene(IBackendScene inner,IContentNativeDirectory backend,Action release){_inner=inner;_backend=backend;_release=release;} public bool IsLoaded=>_inner?.IsLoaded==true; public string Name=>_inner?.Name ?? string.Empty; public GameObject[] GetRootGameObjects()=>_inner?.GetRootGameObjects() ?? Array.Empty<GameObject>(); internal async UniTask Unload(){if(_inner!=null){await _backend.UnloadSceneAsync(_inner);_inner=null;_release();}} internal void ReleaseAfterShutdown(){if(_inner==null)return;_inner=null;_release();} }

        public void Release(IBackendAsset asset) { if(asset is SessionAsset a)a.Release(_backend); else if(asset is SessionInstance i)i.Release(); }
        public UniTask UnloadSceneAsync(IBackendScene scene) => scene is SessionScene s ? s.Unload() : UniTask.CompletedTask;
        public void ReleaseSceneAfterUnityShutdown(IBackendScene scene)
        {
            // Application.quitting/Play Mode 終了では Unity が Scene を解体済みのことがある。
            // 再度 UnloadSceneAsync せず台帳 token だけ返し、残る native operation の terminal は別に待つ。
            if (scene is SessionScene sessionScene) sessionScene.ReleaseAfterShutdown();
        }
    }
}
