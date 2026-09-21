#nullable enable

using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using Unity.Loading;
using UnityEngine;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>一つの登録 directory の root、native handle、発行済み token と unregister を所有する。</summary>
    internal sealed class ContentDirectorySession : IContentDirectoryBackend
    {
        private static readonly object RollbackSync = new();
        private static Action? _pendingRollback;
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
        private Exception? _closeFailure;
        private readonly List<Func<UniTask>> _failedCleanups = new();
        private Action? _evictCache;

        private ContentDirectorySession(string path, string identity, string target, IDisposable reservation,
            ContentDirectoryIndex index, IContentNativeDirectory backend)
        { _path=path; BuildIdentity=identity; Target=target; _reservation=reservation; _registered=true; _index=index; _backend=backend; }

        public string BuildIdentity { get; }
        public string Target { get; }
        public string CachePrefix => ContentDirectoryIndex.CachePrefix(_path, BuildIdentity, Target);

        public AssetKey GetObjectKey(string logicalKey, string representation)
        {
            var entry = ResolveObject(logicalKey, representation);
            return AssetKey.FromContentDirectory(ContentDirectoryIndex.CacheKey(_path, BuildIdentity, Target, entry), entry.Category);
        }

        private BuildContentEntry ResolveObject(string logicalKey, string representation)
        {
            if (_index.TryResolveObject(logicalKey, representation, out var entry, out var issue)) return entry!;
            throw ToException(issue);
        }

        private BuildContentEntry ResolveScene(string logicalKey, string representation)
        {
            if (_index.TryResolveScene(logicalKey, representation, out var entry, out var issue)) return entry!;
            throw ToException(issue);
        }

        private ContentDirectoryException ToException(ContentDirectoryIssue issue)
            => new(issue.Code, issue.Message, BuildIdentity, Target, issue.LogicalKey, issue.Representation);

        public void ConfigureCacheEviction(Action evictRevisionEntries) => _evictCache = evictRevisionEntries;

        internal static ContentDirectorySession Register(string path, string identity, string target)
            => Register(path, identity, target, new UnityContentDirectoryBackend());

        internal static ContentDirectorySession Register(string path, string identity, string target, IContentNativeDirectory backend)
        {
            if (!ContentRevisionGate.TryNormalize(identity, target, path, out var normalized))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                    "Content directory path is invalid.", identity, target);
            if (!Directory.Exists(normalized))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                    "Content directory path must exist.", identity, target);
            if (InstalledRevisionVerifier.LooksManaged(normalized))
            {
                var parent=Directory.GetParent(normalized) ?? throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,"Managed content root is invalid.",identity,target);
                var receipt=ContentDeliveryFiles.ReadJson<ContentInstallReceipt>(Path.Combine(parent.FullName,"receipt.json"));
                var verified=InstalledRevisionVerifier.Verify(parent.FullName,receipt.manifestSha256,receipt.contentSet,identity,target);
                return RegisterVerified(verified,backend);
            }
            return RegisterCore(normalized,identity,target,backend,null);
        }

        internal static ContentDirectorySession RegisterVerified(VerifiedInstalledRevision verified, IContentNativeDirectory backend)
            => RegisterCore(verified.ContentPath,verified.Identity,verified.Target,backend,verified);

        private static ContentDirectorySession RegisterCore(string normalized,string identity,string target,IContentNativeDirectory backend,VerifiedInstalledRevision? verified)
        {
            lock (RollbackSync)
            {
                // 登録失敗時の native handle は session が所有し、gate には予約だけを残す。
                // 別 path への再試行でも先の unregister が成功するまで新規登録しない。
                if (_pendingRollback != null)
                {
                    try { _pendingRollback(); _pendingRollback = null; }
                    catch (Exception ex)
                    {
                        throw new ContentDirectoryException(ContentDirectoryFailureCode.RegistrationFailed,
                            "Earlier content directory rollback is still pending.", identity, target, innerException: ex);
                    }
                }
            }
            var reservation = ContentRevisionGate.Reserve(identity, target, normalized);
            var registered = false;
            try
            {
                if(verified!=null) InstalledRevisionVerifier.VerifyInsideLease(verified.RevisionRoot,verified.ManifestSha256,verified.ContentSet,verified.Identity,verified.Target);
                registered = backend.Register(normalized);
                if (!registered) throw new ContentDirectoryException(ContentDirectoryFailureCode.RegistrationFailed, "Content directory registration returned an invalid handle.", identity, target);
                var roots = backend.GetRoots();
                if (roots.Length != 1 || roots[0] == null)
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Content directory must contain exactly one BuildContentRoot.", identity, target);
                if (!ContentDirectoryIndex.TryCreate(roots[0], identity, target, out var index, out var issue))
                    throw new ContentDirectoryException(issue.Code, issue.Message, identity, target, issue.LogicalKey, issue.Representation);
                return new ContentDirectorySession(normalized, identity, target, reservation, index!, backend);
            }
            catch (Exception ex)
            {
                try
                {
                    if (registered) backend.Unregister();
                    reservation.Dispose();
                }
                catch (Exception cleanupException)
                {
                    // native 登録が残ったまま lease を返すと、別処理が物理削除を許可される。
                    // 予約は gate に残し、次の登録時に同じ backend の unregister を再試行する。
                    lock (RollbackSync) _pendingRollback = () => { backend.Unregister(); reservation.Dispose(); };
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.RegistrationFailed,
                        "Content directory registration rollback failed.", identity, target,
                        innerException: new AggregateException(ex, cleanupException));
                }
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
                var operation = _backend.LoadObjectAsync<T>(ResolveObject(logicalKey, representation)).Preserve();
                IBackendAsset asset;
                try { asset = await operation.AttachExternalCancellation(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                { terminalCleanupOwnsCounter = true; DrainAbandonedAsset(operation).Forget(); throw; }
                Interlocked.Increment(ref _liveTokens);
                if (ct.IsCancellationRequested)
                {
                    try { _backend.Release(asset); }
                    catch (Exception) { _failedCleanups.Add(() => { _backend.Release(asset); return UniTask.CompletedTask; }); }
                    Interlocked.Decrement(ref _liveTokens);
                    ct.ThrowIfCancellationRequested();
                }
                return new SessionAsset(asset, ReleaseToken);
            }
            catch (ContentDirectoryException) { throw; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed,
                    "Content object load failed.", BuildIdentity, Target, logicalKey, representation, ex);
            }
            finally { if (!terminalCleanupOwnsCounter) CompleteOperation(); }
        }

        public async UniTask<IBackendScene> LoadSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options, CancellationToken ct)
        {
            EnsureAccepting(); ct.ThrowIfCancellationRequested();
            if (!options.ActivateOnLoad)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                    "Deferred scene activation is not supported by content directory loads.", BuildIdentity, Target, sceneIdentity, representation);
            BeginOperation();
            var terminalCleanupOwnsCounter = false;
            try
            {
                var operation = _backend.LoadSceneAsync(ResolveScene(sceneIdentity, representation), options).Preserve();
                IBackendScene scene;
                try { scene = await operation.AttachExternalCancellation(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                { terminalCleanupOwnsCounter = true; DrainAbandonedScene(operation).Forget(); throw; }
                if (ct.IsCancellationRequested)
                {
                    try { await _backend.UnloadSceneAsync(scene); }
                    catch (Exception) { _failedCleanups.Add(() => _backend.UnloadSceneAsync(scene)); }
                    ct.ThrowIfCancellationRequested();
                }
                Interlocked.Increment(ref _liveTokens); return new SessionScene(scene, _backend, ReleaseToken);
            }
            catch (ContentDirectoryException) { throw; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed,
                    "Content scene load failed.", BuildIdentity, Target, sceneIdentity, representation, ex);
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
                var operation = _backend.InstantiateAsync(ResolveObject(logicalKey, representation), parent, worldSpace).Preserve();
                IBackendInstance instance;
                try { instance = await operation.AttachExternalCancellation(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                { terminalCleanupOwnsCounter = true; DrainAbandonedInstance(operation).Forget(); throw; }
                if (ct.IsCancellationRequested)
                {
                    try { await CleanupAbandonedInstance(instance); }
                    catch (Exception) { _failedCleanups.Add(() => CleanupAbandonedInstance(instance)); }
                    ct.ThrowIfCancellationRequested();
                }
                Interlocked.Increment(ref _liveTokens); return new SessionInstance(instance, _backend, ReleaseToken);
            }
            catch (ContentDirectoryException) { throw; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed,
                    "Content prefab instantiation failed.", BuildIdentity, Target, logicalKey, representation, ex);
            }
            finally { if (!terminalCleanupOwnsCounter) CompleteOperation(); }
        }

        public async UniTask CloseAsync()
        {
            await StopAndDrainAsync();
            await RetryFailedCleanupsAsync();
            _evictCache?.Invoke();
            if (Volatile.Read(ref _liveTokens) != 0)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.ResourcesInUse, "Content directory still has live asset, scene, cache, or instance tokens.", BuildIdentity, Target);
            CloseNow();
            if (_closeFailure != null)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed,
                    "Content directory unregister failed; the registration is retained for retry.", BuildIdentity, Target,
                    innerException: _closeFailure);
        }

        public async UniTask StopAndDrainAsync()
        {
            _accepting=false;
            var drained = _operationsDrained;
            if (Volatile.Read(ref _pendingOperations) != 0 && drained != null) await drained.Task;
        }

        public void EnsureAccepting()
        {
            if (!_accepting || _closed) throw new ContentDirectoryException(ContentDirectoryFailureCode.DirectoryNotRegistered,
                "Content directory is not accepting new loads.", BuildIdentity, Target);
        }

        public void BeginSynchronousShutdown() { _accepting=false; if (Volatile.Read(ref _liveTokens)==0 && Volatile.Read(ref _pendingOperations)==0 && _failedCleanups.Count==0) CloseNow(); }

        private async UniTaskVoid DrainAbandonedAsset(UniTask<IBackendAsset> operation)
        {
            try
            {
                var asset = await operation;
                try { _backend.Release(asset); }
                catch (Exception) { _failedCleanups.Add(() => { _backend.Release(asset); return UniTask.CompletedTask; }); }
            }
            catch (Exception) { /* native load 自体が失敗した場合は解放対象が発行されていない。 */ }
            finally { CompleteOperation(); }
        }
        private async UniTaskVoid DrainAbandonedScene(UniTask<IBackendScene> operation)
        {
            try
            {
                var scene = await operation;
                try { await _backend.UnloadSceneAsync(scene); }
                catch (Exception) { _failedCleanups.Add(() => _backend.UnloadSceneAsync(scene)); }
            }
            catch (Exception) { /* native load 自体の失敗。 */ }
            finally { CompleteOperation(); }
        }
        private async UniTaskVoid DrainAbandonedInstance(UniTask<IBackendInstance> operation)
        {
            try
            {
                var instance = await operation;
                try { await CleanupAbandonedInstance(instance); }
                catch (Exception) { _failedCleanups.Add(() => CleanupAbandonedInstance(instance)); }
            }
            catch (Exception) { /* native load 自体の失敗。 */ }
            finally { CompleteOperation(); }
        }
        private async UniTask CleanupAbandonedInstance(IBackendInstance instance)
        {
            // caller に渡す前に取消された実体には GameObject owner がいない。
            // この経路だけ session が破棄し、通常の caller-owned instance は破棄しない。
            var gameObject = instance.Instance;
            if (gameObject != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(gameObject);
                else UnityEngine.Object.DestroyImmediate(gameObject);
                await UniTask.WaitUntil(() => gameObject == null);
            }
            if (instance is IBackendAsset asset) _backend.Release(asset);
        }
        private async UniTask RetryFailedCleanupsAsync()
        {
            Exception? failure = null;
            for (var i = _failedCleanups.Count - 1; i >= 0; i--)
            {
                try { await _failedCleanups[i](); _failedCleanups.RemoveAt(i); }
                catch (Exception ex) { failure ??= ex; }
            }
            if (failure != null)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed,
                    "Content directory cleanup failed; registration is retained for retry.", BuildIdentity, Target,
                    innerException: failure);
        }
        private void BeginOperation()
        {
            if (Interlocked.Increment(ref _pendingOperations) == 1) _operationsDrained = new UniTaskCompletionSource();
        }
        private void CompleteOperation()
        {
            if (Interlocked.Decrement(ref _pendingOperations) != 0) return;
            _operationsDrained?.TrySetResult();
            if (!_accepting && Volatile.Read(ref _liveTokens) == 0 && _failedCleanups.Count == 0) CloseNow();
        }
        private void ReleaseToken() { if (Interlocked.Decrement(ref _liveTokens)==0 && !_accepting && Volatile.Read(ref _pendingOperations)==0 && _failedCleanups.Count == 0) CloseNow(); }
        private void CloseNow()
        {
            if (_closed || _failedCleanups.Count != 0) return;
            try
            {
                if (_registered) _backend.Unregister();
                _closed = true;
                _closeFailure = null;
                _reservation.Dispose();
            }
            catch (Exception ex) { _closeFailure = ex; }
        }

        private sealed class SessionAsset : IBackendAsset, IContentDirectoryToken
        { private IBackendAsset? _inner; private readonly Action _release; internal SessionAsset(IBackendAsset inner, Action release){_inner=inner;_release=release;} public UnityEngine.Object? Asset=>_inner?.Asset; public bool IsValid=>_inner?.IsValid==true; internal void Release(IContentNativeDirectory backend){if(_inner!=null){backend.Release(_inner);_inner=null;_release();}} }
        private sealed class SessionInstance : IBackendInstance, IBackendAsset, IContentDirectoryToken
        { private IBackendInstance? _inner; private readonly IContentNativeDirectory _backend; private readonly Action _release; internal SessionInstance(IBackendInstance inner,IContentNativeDirectory backend,Action release){_inner=inner;_backend=backend;_release=release;} public GameObject? Instance=>_inner?.Instance; public UnityEngine.Object? Asset=>Instance; public bool IsValid=>_inner is IBackendAsset a && a.IsValid; internal void Release(){if(_inner is IBackendAsset a)_backend.Release(a);_inner=null;_release();} }
        private sealed class SessionScene : IBackendScene, IContentDirectoryToken
        {
            private IBackendScene? _inner;
            private readonly IContentNativeDirectory _backend;
            private readonly Action _release;
            private UniTaskCompletionSource? _unloadCompletion;

            internal SessionScene(IBackendScene inner, IContentNativeDirectory backend, Action release)
            { _inner = inner; _backend = backend; _release = release; }

            public bool IsLoaded => _inner?.IsLoaded == true;
            public string Name => _inner?.Name ?? string.Empty;
            public GameObject[] GetRootGameObjects() => _inner?.GetRootGameObjects() ?? Array.Empty<GameObject>();
            internal bool HasPendingUnload => _unloadCompletion != null;

            internal UniTask Unload()
            {
                if (_inner == null) return UniTask.CompletedTask;
                if (_unloadCompletion != null) return _unloadCompletion.Task;
                // 通常 unload・明示 close・同期終了が同じ native terminal を共有する。
                // token は成功した最後の一回だけ返し、失敗時は再試行できるよう保持する。
                var completion = new UniTaskCompletionSource();
                _unloadCompletion = completion;
                UnloadCore(completion).Forget();
                return completion.Task;
            }

            private async UniTaskVoid UnloadCore(UniTaskCompletionSource completion)
            {
                try
                {
                    await _backend.UnloadSceneAsync(_inner!);
                    _inner = null;
                    _release();
                    _unloadCompletion = null;
                    completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    // 完了通知で再開した caller が即座に retry できるよう、先に単一飛行状態を外す。
                    if (ReferenceEquals(_unloadCompletion, completion)) _unloadCompletion = null;
                    completion.TrySetException(ex);
                }
            }

            internal void ReleaseAfterShutdown()
            {
                if (_inner == null) return;
                _inner = null;
                _release();
            }
        }

        public void Release(IBackendAsset asset)
        {
            // owner/cache 台帳は callback 前に entry を外す。native Release が失敗した token は
            // session に移して保持し、close retry で再解放するまで gate の予約を返さない。
            if (asset is SessionAsset a)
            {
                try { a.Release(_backend); }
                catch (Exception) { _failedCleanups.Add(() => { a.Release(_backend); return UniTask.CompletedTask; }); }
            }
            else if (asset is SessionInstance i)
            {
                try { i.Release(); }
                catch (Exception) { _failedCleanups.Add(() => { i.Release(); return UniTask.CompletedTask; }); }
            }
        }
        public UniTask UnloadSceneAsync(IBackendScene scene) => scene is SessionScene s ? s.Unload() : UniTask.CompletedTask;
        public void ReleaseSceneAfterUnityShutdown(IBackendScene scene)
        {
            if (scene is not SessionScene sessionScene) return;
            // Unity が既に解体した Scene は token だけ返す。まだ loaded なら非同期 unload の
            // terminal を監視し、同期終了が先に unregister しないよう pending に数える。
            if (!sessionScene.IsLoaded && !sessionScene.HasPendingUnload)
            { sessionScene.ReleaseAfterShutdown(); return; }
            BeginOperation();
            DrainShutdownScene(sessionScene).Forget();
        }

        public void ReleaseSceneTokenAfterPlayStop(IBackendScene scene)
        {
            if (scene is not SessionScene sessionScene) return;
            // Play 停止では Unity が Play Mode ごと Scene を解体する。native UnloadSceneAsync は
            // PlayerLoop が要るのでここでは起こさず、token だけ返して lease 解放へ進む。
            sessionScene.ReleaseAfterShutdown();
        }

        public void CompletePlayStop()
        {
            _accepting = false;
            _evictCache?.Invoke();
            // pending native Unload は待たない。quit 中に PlayerLoop で進めると同期待ちは死鎖する。
            CloseNowForPlayStop();
        }

        private void CloseNowForPlayStop()
        {
            if (_closed) return;
            try
            {
                if (_registered) _backend.Unregister();
                _closed = true;
                _closeFailure = null;
                _reservation.Dispose();
            }
            catch (Exception ex)
            {
                _closeFailure = ex;
                // unregister 失敗のまま lease を返すと、native 登録が残った状態で物理削除が通る。
                // Play 停止後は AssetManagement が session を捨てるので、明示 close の再試行は残らない。
                // 予約は gate に残し、次の登録で同じ backend の unregister を再試行する。
                lock (RollbackSync)
                    _pendingRollback ??= () => { _backend.Unregister(); _reservation.Dispose(); };
            }
        }

        private async UniTaskVoid DrainShutdownScene(SessionScene scene)
        {
            try { await scene.Unload(); }
            catch (Exception) { _failedCleanups.Add(() => scene.Unload()); }
            finally { CompleteOperation(); }
        }
    }
}
