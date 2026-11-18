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
    /// <summary>
    /// 一つの登録（base と任意の上書き 1 つの層の列）の発行済み token・処理・後始末と unregister の時点を所有する。
    /// 層の native の登録・解決・逆順の解除は <see cref="ContentDirectoryLayers"/> にあり、ここは数え方と load の入口に留める。
    /// token・処理・後始末は層をまたいで 1 組だけ数える。層ごとに数えると、上書きの物が 0 になった時点で上書きだけを外し、
    /// base から載せた物が残る間に上書きの解除が base の物を壊す順を作ってしまう。
    /// </summary>
    internal sealed class ContentDirectorySession : IContentDirectoryBackend
    {
        private readonly ContentDirectoryLayers _layers;
        private int _liveTokens;
        private int _pendingOperations;
        private UniTaskCompletionSource? _operationsDrained;
        private bool _accepting = true;
        private bool _closed;
        private Exception? _closeFailure;
        private readonly List<Func<UniTask>> _failedCleanups = new();
        private Action? _evictCache;

        private ContentDirectorySession(ContentDirectoryLayers layers, string target, VerifiedInstalledRevision? verifiedRevision)
        { _layers=layers; BuildIdentity=layers.Base.Identity; Target=target; Declaration=layers.Base.Root!.Declaration; VerifiedRevision=verifiedRevision; }

        public string BuildIdentity { get; }
        public string Target { get; }

        /// <summary>登録した root が持つ宣言。宣言の無い root（旧 build、合成 fixture）では null。起動が設定との不一致を見るために読む。</summary>
        internal BuildContentDeclaration? Declaration { get; }

        /// <summary>
        /// 検証済みの installed revision から登録した時の検証の値（4 欄の identity と manifest の互換の事実。登録の予約の中で検証し直した値）。
        /// 検証済みでない登録（Editor の path 指定、Player に同梱した directory）では null。要求時解決の供給元が使う。
        /// </summary>
        internal VerifiedInstalledRevision? VerifiedRevision { get; }

        /// <summary>新しい load を受け付けているか。close・Play 停止・同期終了の後は false で、戻らない。</summary>
        internal bool IsAccepting => _accepting && !_closed;

        /// <summary>全ての層の解除と予約の返却が済んだか（解除の失敗の後は false。close か、次の登録の前の再試行が済めば true）。</summary>
        internal bool IsClosed => _closed || _layers.IsReleased;

        /// <summary>上書きの層。上書きを重ねない登録では null。要求時解決の上書きの供給元が使う。</summary>
        internal ContentDirectoryLayers.Layer? OverlayLayer => _layers.Overlay;

        /// <summary>
        /// 層の解決（native の load はしない）。load の入口と供給元が同じ規則を使う。上書きを重ねない登録の規則は 1 directory のまま
        /// （要求の表現が無ければ Full、空の表現は Full）。
        /// </summary>
        internal LayerResolution ResolveEntry(string logicalKey, BuildContentKind kind, string representation)
            => _layers.Resolve(logicalKey, kind, representation);

        /// <summary>索引で entry を引く（native の load はしない）。層の解決が選んだ entry を返す。</summary>
        internal bool TryFindEntry(string logicalKey, BuildContentKind kind, string representation, out BuildContentEntry? entry)
        {
            entry = ResolveEntry(logicalKey, kind, representation).Entry;
            return entry != null;
        }

        public string CachePrefix => _layers.CachePrefix(Target);

        public AssetKey GetObjectKey(string logicalKey, string representation)
        {
            var (layer, entry) = ResolveForLoad(logicalKey, BuildContentKind.Object, representation);
            return AssetKey.FromContentDirectory(_layers.CacheKey(Target, layer, entry), entry.Category);
        }

        // content 入口の解決。要求の表現の base の entry が上書きの id による影で除かれた時は、別の表現（上書きの Full）へ
        // 読み替えずに拒む。呼び手は base が持つ (key, 表現) を指したが、native の id は上書きの版を読むので、どちらを渡しても要求と違う物になる。
        // 表現を読み替えたい呼び手は Full を明示して呼ぶ（要求時解決の loader は採った候補の表現で呼ぶ）。
        private (ContentDirectoryLayers.Layer Layer, BuildContentEntry Entry) ResolveForLoad(string logicalKey, BuildContentKind kind, string representation)
        {
            var resolution = _layers.Resolve(logicalKey, kind, representation);
            if (resolution.Entry != null
                && (!resolution.BaseShadowedAtRequested
                    || string.Equals(ContentEntryMatch.EffectiveRepresentation(resolution.Entry.Representation),
                        ContentEntryMatch.EffectiveRepresentation(representation), StringComparison.Ordinal)))
                return (resolution.Layer!, resolution.Entry);
            throw new ContentDirectoryException(ContentDirectoryFailureCode.EntryMissing,
                resolution.BaseShadowedAtRequested
                    ? "The base entry for the requested representation is shadowed by the overlay layer."
                    : "No matching content entry is registered.",
                BuildIdentity, Target, logicalKey, representation);
        }

        private static string LayerSuffix(ContentDirectoryLayers.Layer layer) => layer.IsOverlay ? " (overlay layer)" : string.Empty;

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
            return RegisterCore(normalized,identity,target,backend,null,null);
        }

        internal static ContentDirectorySession RegisterVerified(VerifiedInstalledRevision verified, IContentNativeDirectory backend)
            => RegisterCore(verified.ContentPath,verified.Identity,verified.Target,backend,verified,null);

        /// <summary>
        /// 検証済みの base に、上書きの directory を 1 つ重ねて登録する。base → 上書きの順に native へ登録し、上書きの root の base の記録を
        /// base の検証済みの値（予約の中で検証し直した値）と照合し、一致した時だけ層 2 つの session を返す。上書きの target は base の検証済みの target。
        /// 失敗は例外で返し、base だけの session を返さない（上書きを重ねた登録の失敗の後に base だけで続ける道を作らない）。
        /// 層は作った後に足せず、外せない。
        /// </summary>
        internal static ContentDirectorySession RegisterVerifiedWithOverlay(VerifiedInstalledRevision baseRevision, string overlayPath, string overlayIdentity)
            => RegisterVerifiedWithOverlay(baseRevision, new UnityContentDirectoryBackend(), overlayPath, overlayIdentity, new UnityContentDirectoryBackend());

        internal static ContentDirectorySession RegisterVerifiedWithOverlay(VerifiedInstalledRevision baseRevision, IContentNativeDirectory baseBackend,
            string overlayPath, string overlayIdentity, IContentNativeDirectory overlayBackend)
        {
            if (baseRevision == null) throw new ArgumentNullException(nameof(baseRevision));
            if (baseBackend == null) throw new ArgumentNullException(nameof(baseBackend));
            if (overlayBackend == null) throw new ArgumentNullException(nameof(overlayBackend));
            if (!ContentRevisionGate.TryNormalize(overlayIdentity, baseRevision.Target, overlayPath, out var normalized))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                    "Overlay content directory path or identity is invalid.", baseRevision.Identity, baseRevision.Target);
            if (!Directory.Exists(normalized))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                    "Overlay content directory path must exist.", baseRevision.Identity, baseRevision.Target);
            return RegisterCore(baseRevision.ContentPath, baseRevision.Identity, baseRevision.Target, baseBackend, baseRevision,
                new ContentDirectoryLayers.Layer(true, normalized, overlayIdentity, overlayBackend));
        }

        private static ContentDirectorySession RegisterCore(string normalized,string identity,string target,IContentNativeDirectory backend,
            VerifiedInstalledRevision? verified, ContentDirectoryLayers.Layer? overlay)
        {
            // 登録失敗時の native handle は層の列が所有し、gate には予約だけを残す。
            // 別 path への再試行でも先の unregister が成功するまで新規登録しない（層の列の再試行が先に走る）。
            var layers = ContentDirectoryLayers.Register(target, new ContentDirectoryLayers.Layer(false, normalized, identity, backend), overlay,
                () => verified == null ? null
                    : InstalledRevisionVerifier.VerifyInsideLease(verified.RevisionRoot,verified.ManifestSha256,verified.ContentSet,verified.Identity,verified.Target),
                out var reverified);
            return new ContentDirectorySession(layers, target, reverified);
        }

        public async UniTask<IBackendAsset> LoadAssetAsync<T>(string logicalKey, string representation, CancellationToken ct) where T : UnityEngine.Object
        {
            EnsureAccepting(); ct.ThrowIfCancellationRequested();
            BeginOperation();
            var terminalCleanupOwnsCounter = false;
            ContentDirectoryLayers.Layer? selected = null;
            try
            {
                // 呼出側の取消は Unity のロード自体を止めない。遅れて成功した資源を回収するまで
                // directory を unregister すると、native handle の寿命が逆転する。
                var (layer, entry) = ResolveForLoad(logicalKey, BuildContentKind.Object, representation);
                selected = layer;
                var native = layer.Native;
                var operation = native.LoadObjectAsync<T>(entry).Preserve();
                IBackendAsset asset;
                try { asset = await operation.AttachExternalCancellation(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                { terminalCleanupOwnsCounter = true; DrainAbandonedAsset(native, operation).Forget(); throw; }
                Interlocked.Increment(ref _liveTokens);
                if (ct.IsCancellationRequested)
                {
                    try { native.Release(asset); }
                    catch (Exception) { _failedCleanups.Add(() => { native.Release(asset); return UniTask.CompletedTask; }); }
                    Interlocked.Decrement(ref _liveTokens);
                    ct.ThrowIfCancellationRequested();
                }
                return new SessionAsset(asset, native, ReleaseToken);
            }
            catch (ContentDirectoryException) { throw; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed,
                    "Content object load failed" + (selected == null ? "" : LayerSuffix(selected)) + ".", BuildIdentity, Target, logicalKey, representation, ex);
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
            ContentDirectoryLayers.Layer? selected = null;
            try
            {
                var (layer, entry) = ResolveForLoad(sceneIdentity, BuildContentKind.Scene, representation);
                selected = layer;
                var native = layer.Native;
                var operation = native.LoadSceneAsync(entry, options).Preserve();
                IBackendScene scene;
                try { scene = await operation.AttachExternalCancellation(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                { terminalCleanupOwnsCounter = true; DrainAbandonedScene(native, operation).Forget(); throw; }
                if (ct.IsCancellationRequested)
                {
                    try { await native.UnloadSceneAsync(scene); }
                    catch (Exception) { _failedCleanups.Add(() => native.UnloadSceneAsync(scene)); }
                    ct.ThrowIfCancellationRequested();
                }
                Interlocked.Increment(ref _liveTokens); return new SessionScene(scene, native, ReleaseToken);
            }
            catch (ContentDirectoryException) { throw; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed,
                    "Content scene load failed" + (selected == null ? "" : LayerSuffix(selected)) + ".", BuildIdentity, Target, sceneIdentity, representation, ex);
            }
            finally { if (!terminalCleanupOwnsCounter) CompleteOperation(); }
        }

        public async UniTask<IBackendInstance> InstantiateAsync(string logicalKey, string representation, Transform? parent, bool worldSpace, CancellationToken ct)
        {
            EnsureAccepting(); ct.ThrowIfCancellationRequested();
            BeginOperation();
            var terminalCleanupOwnsCounter = false;
            ContentDirectoryLayers.Layer? selected = null;
            try
            {
                var (layer, entry) = ResolveForLoad(logicalKey, BuildContentKind.Object, representation);
                selected = layer;
                var native = layer.Native;
                var operation = native.InstantiateAsync(entry, parent, worldSpace).Preserve();
                IBackendInstance instance;
                try { instance = await operation.AttachExternalCancellation(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                { terminalCleanupOwnsCounter = true; DrainAbandonedInstance(native, operation).Forget(); throw; }
                if (ct.IsCancellationRequested)
                {
                    try { await CleanupAbandonedInstance(native, instance); }
                    catch (Exception) { _failedCleanups.Add(() => CleanupAbandonedInstance(native, instance)); }
                    ct.ThrowIfCancellationRequested();
                }
                Interlocked.Increment(ref _liveTokens); return new SessionInstance(instance, native, ReleaseToken);
            }
            catch (ContentDirectoryException) { throw; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed,
                    "Content prefab instantiation failed" + (selected == null ? "" : LayerSuffix(selected)) + ".", BuildIdentity, Target, logicalKey, representation, ex);
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

        private async UniTaskVoid DrainAbandonedAsset(IContentNativeDirectory native, UniTask<IBackendAsset> operation)
        {
            try
            {
                var asset = await operation;
                try { native.Release(asset); }
                catch (Exception) { _failedCleanups.Add(() => { native.Release(asset); return UniTask.CompletedTask; }); }
            }
            catch (Exception) { /* native load 自体が失敗した場合は解放対象が発行されていない。 */ }
            finally { CompleteOperation(); }
        }
        private async UniTaskVoid DrainAbandonedScene(IContentNativeDirectory native, UniTask<IBackendScene> operation)
        {
            try
            {
                var scene = await operation;
                try { await native.UnloadSceneAsync(scene); }
                catch (Exception) { _failedCleanups.Add(() => native.UnloadSceneAsync(scene)); }
            }
            catch (Exception) { /* native load 自体の失敗。 */ }
            finally { CompleteOperation(); }
        }
        private async UniTaskVoid DrainAbandonedInstance(IContentNativeDirectory native, UniTask<IBackendInstance> operation)
        {
            try
            {
                var instance = await operation;
                try { await CleanupAbandonedInstance(native, instance); }
                catch (Exception) { _failedCleanups.Add(() => CleanupAbandonedInstance(native, instance)); }
            }
            catch (Exception) { /* native load 自体の失敗。 */ }
            finally { CompleteOperation(); }
        }
        private static async UniTask CleanupAbandonedInstance(IContentNativeDirectory native, IBackendInstance instance)
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
            if (instance is IBackendAsset asset) native.Release(asset);
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
            // 層の列の逆順に解除し、全ての層と予約が済んだ時だけ closed にする。途中で失敗した層の列は、close の再試行で
            // 未解除の層から再開する（解除済みの層は再び呼ばない）。
            if (_layers.TryUnregisterAll(out var failure)) { _closed = true; _closeFailure = null; }
            else _closeFailure = failure;
        }

        // token は載せた層の native を持ち、解放はその層へ返す。
        private sealed class SessionAsset : IBackendAsset, IContentDirectoryToken
        { private IBackendAsset? _inner; private readonly IContentNativeDirectory _backend; private readonly Action _release; internal SessionAsset(IBackendAsset inner, IContentNativeDirectory backend, Action release){_inner=inner;_backend=backend;_release=release;} public UnityEngine.Object? Asset=>_inner?.Asset; public bool IsValid=>_inner?.IsValid==true; internal void Release(){if(_inner!=null){_backend.Release(_inner);_inner=null;_release();}} }
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
                try { a.Release(); }
                catch (Exception) { _failedCleanups.Add(() => { a.Release(); return UniTask.CompletedTask; }); }
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
            // token も失敗した後始末も待たない（Unity が Play mode ごと解体する）。順（上書き → base）と
            // 「上書きの解除が失敗したら base を解除しない」は層の列が守る。
            if (_layers.TryUnregisterAll(out var failure)) { _closed = true; _closeFailure = null; return; }
            _closeFailure = failure;
            // unregister 失敗のまま lease を返すと、native 登録が残った状態で物理削除が通る。
            // Play 停止後は AssetManagement が session を捨てるので、明示 close の再試行は残らない。
            // 予約は gate に残し、次の登録の前に同じ層の列の未解除の層から再試行する。
            _layers.DeferRollback();
        }

        private async UniTaskVoid DrainShutdownScene(SessionScene scene)
        {
            try { await scene.Unload(); }
            catch (Exception) { _failedCleanups.Add(() => scene.Unload()); }
            finally { CompleteOperation(); }
        }
    }
}
