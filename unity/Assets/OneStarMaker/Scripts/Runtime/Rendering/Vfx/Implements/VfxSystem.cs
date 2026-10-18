#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.SceneSystem;
using R3;
using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// VFX instance の寿命・非対応環境での停止・例外の隔離を、VFX 実装の型に依存せずに受け持つ App 常駐サービス。
    /// <para>
    /// 所有者の終端は既存の通知だけで知る。Scene 所有は <c>SceneDirector.OnSceneEvent</c> の
    /// <c>StateChanged(PreUnloading)</c> / <c>StateChanged(LoadCanceled)</c> で回収する。どちらも Unity Scene の unload と
    /// Scene 所有 asset の解放より前に出るので、backend が破棄済みの GameObject を扱う場面を減らせる。
    /// <c>Removed</c> / <c>CancelCleanedUp</c> は冪等な保険。SceneBase の unload フックは各 Scene の書き忘れが漏れになるので使わない。
    /// Bind 所有は go に付けた <see cref="VfxOwnerDestroyNotifier"/> と、<see cref="Tick"/> の偽 null 検査の二経路で回収する。
    /// </para>
    /// <para>
    /// 生成・<see cref="BindSceneLifecycle"/>・<see cref="Tick"/> の UpdateSystemRuntime 登録・<see cref="Dispose"/> は
    /// composition の責任。<see cref="Tick"/> は消え切り待ちの完了・timeout と破棄済み go の検出にだけ要り、
    /// 所有者の終端は Tick に依存しない。全メソッドは Unity の main thread からだけ呼ぶ（スレッド安全にはしない）。
    /// </para>
    /// <para>
    /// backend 由来の例外は呼び出し元へ投げない。例外を起こした instance だけを回収し、他の instance の処理は続ける。
    /// scene event の購読処理でも自分で捕捉する（R3 は購読者の例外を既定ハンドラへ回すだけで、ログに出ない）。
    /// </para>
    /// </summary>
    public sealed class VfxSystem : IVfxSystem, IDisposable
    {
        private readonly IVfxBackend _backend;
        private readonly ILogger<VfxSystem> _logger;
        private readonly float _gracefulStopTimeoutSeconds;
        private readonly bool _supported;
        private readonly VfxOwnerIndex _index = new();
        private readonly Action<VfxHandle> _onHandleReleased;
        private readonly Action<VfxHandle, Exception> _onHandleFailed;
        private readonly Action<ulong> _onOwnerDestroyed;

        private ISceneQuery? _sceneQuery;
        private IDisposable? _sceneSubscription;
        private int _bindGeneration;
        private bool _disposed;

        /// <param name="gracefulStopTimeoutSeconds">
        /// 消え切り待ちの上限。有限の正の値だけを受け付ける（待ちを必ず有限で終わらせるため）。
        /// </param>
        public VfxSystem(IVfxBackend backend, ILogger<VfxSystem> logger, float gracefulStopTimeoutSeconds = 10f)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (!(gracefulStopTimeoutSeconds > 0f) || float.IsInfinity(gracefulStopTimeoutSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(gracefulStopTimeoutSeconds),
                    gracefulStopTimeoutSeconds,
                    "Graceful stop timeout must be a finite positive number.");
            }

            _gracefulStopTimeoutSeconds = gracefulStopTimeoutSeconds;
            _onHandleReleased = _index.Remove;
            _onHandleFailed = LogHandleFailure;
            _onOwnerDestroyed = NotifyOwnerGameObjectDestroyed;
            _supported = QueryCapabilityOnce();
        }

        /// <summary>
        /// Scene lifecycle に接続する。SceneDirector は App 常駐サービスより後に生成されるので ctor と分ける。
        /// <para>
        /// SceneDirector の生成直後、最初の Scene をロードする前に呼ぶ。途中で接続すると、接続前に出た
        /// PreUnloading を知らないまま IsSceneLoaded が true の Scene を受理しうる（途中接続の状態同期は約束しない）。
        /// 二回目の呼び出しは差し替えで、前の購読を解除し、既存の Scene 所有 instance を全て回収し、終端中の記録を消す。
        /// </para>
        /// </summary>
        public void BindSceneLifecycle(Observable<SceneEvent> sceneEvents, ISceneQuery sceneQuery)
        {
            if (sceneEvents == null)
            {
                throw new ArgumentNullException(nameof(sceneEvents));
            }

            if (sceneQuery == null)
            {
                throw new ArgumentNullException(nameof(sceneQuery));
            }

            if (_disposed)
            {
                return;
            }

            // 差し替えの間は Scene 所有の Spawn を拒否する（回収中の Destroy から再入した Spawn を受理しないため）。
            _index.IsSceneLifecycleOpen = false;
            var generation = ++_bindGeneration;
            var previous = _sceneSubscription;
            _sceneSubscription = null;

            // 前の購読の解除が失敗しても、回収と新しい購読は続ける。ここで止めると既存の Scene 所有 instance が
            // 回収されず、世代が進んだ前の購読のイベントは無視されるので、その Scene の終端でも回収されなくなる。
            // 解除に失敗した前の購読がイベントを送り続けても、世代の検査で無視される。
            DisposeSceneSubscription(previous);

            // 再入点（前の購読の解除、回収中の backend Destroy、Subscribe）の後ごとに世代を確かめる。
            // 再入した BindSceneLifecycle が新しい購読まで終えていたら、それ以降の回収・終端中の記録の消去・
            // 購読は新しい購読の状態を壊す（新しい購読で受理した Scene 所有の回収、新しく受けた終端の消去）。
            if (IsBindSuperseded(generation))
            {
                return;
            }

            ReleaseAll(_index.SnapshotSceneOwned());
            if (IsBindSuperseded(generation))
            {
                return;
            }

            _index.ClearTerminal();
            _sceneQuery = sceneQuery;
            // 購読より先に開く。完了済みの Observable は Subscribe の中で OnCompleted を出し、そこで閉じる。
            _index.IsSceneLifecycleOpen = true;
            var subscription = sceneEvents.Subscribe(
                e => OnSceneEvent(generation, e),
                _ => OnSceneEventsCompleted(generation));
            if (IsBindSuperseded(generation))
            {
                // Subscribe の中の再入で Dispose された、または更に差し替えられた。この購読は誰も解除しなくなるので、ここで解除する。
                DisposeSceneSubscription(subscription);
                return;
            }

            _sceneSubscription = subscription;
        }

        public IVfxHandle Spawn(in VfxSpawnRequest request, AssetOwner owner)
        {
            if (_disposed || !_supported || !CanAccept(request, owner))
            {
                return NullVfxHandle.Instance;
            }

            VfxInstanceId id;
            try
            {
                id = _backend.Create(request);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[VfxSystem] Create failed. owner={Owner}", owner.ToString());
                return NullVfxHandle.Instance;
            }

            if (!id.IsValid)
            {
                _logger.LogError("[VfxSystem] Create returned an invalid id. owner={Owner}", owner.ToString());
                return NullVfxHandle.Instance;
            }

            if (_index.ContainsLive(id))
            {
                // backend 契約違反。生存中の別 instance を壊さないため Destroy を要求しない。
                _logger.LogError(
                    "[VfxSystem] Create returned an id that is still alive. id={Id} owner={Owner}",
                    id.Value,
                    owner.ToString());
                return NullVfxHandle.Instance;
            }

            var handle = new VfxHandle(_backend, id, owner, _onHandleReleased, _onHandleFailed);

            // Create の中の再入で所有者が終端に入った、または Dispose された場合に備えて受理判定をやり直す。
            if (_disposed || !CanAccept(request, owner))
            {
                handle.Release();
                return NullVfxHandle.Instance;
            }

            try
            {
                _index.Add(handle);
                if (owner.Kind == AssetOwnerKind.GameObject)
                {
                    EnsureDestroyNotifier(owner);
                }

                RegistrationFaultForTest?.Invoke(handle);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[VfxSystem] Registering a VFX instance failed. id={Id} owner={Owner}",
                    id.Value, owner.ToString());
                handle.Release();
                return NullVfxHandle.Instance;
            }

            return handle;
        }

        /// <summary>
        /// 消え切り待ちを進め、破棄済みの Bind 所有者の instance を回収する。delta が負・NaN・∞なら 0 として扱う。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                foreach (var handle in _index.SnapshotAll())
                {
                    if (handle.State == VfxHandle.HandleState.Stopping)
                    {
                        handle.AdvanceStopping(deltaSeconds, _gracefulStopTimeoutSeconds);
                    }
                }

                // 通知が来ない破棄（一度も active にならなかった go、EditMode）の保険。
                foreach (var owner in _index.SnapshotBindOwners())
                {
                    if (owner.BoundObject == null)
                    {
                        ReleaseAll(_index.SnapshotOwner(owner));
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[VfxSystem] Tick failed.");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // 印を先に立てる。以後の Spawn・破棄通知・Tick・scene event・BindSceneLifecycle は backend を呼ばない。
            _disposed = true;
            _index.IsSceneLifecycleOpen = false;
            var subscription = _sceneSubscription;
            _sceneSubscription = null;

            // 購読の解除と回収は別々に捕捉する。解除の失敗で全 instance の回収が飛ぶと、印が立っているので
            // 二回目の Dispose でも回収できなくなる。
            DisposeSceneSubscription(subscription);
            try
            {
                ReleaseAll(_index.SnapshotAll());
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[VfxSystem] Dispose failed.");
            }
        }

        /// <summary>
        /// Bind 所有者の go が破棄された（<see cref="VfxOwnerDestroyNotifier"/> から呼ばれる）。その go の instance を同期で回収する。
        /// </summary>
        internal void NotifyOwnerGameObjectDestroyed(ulong gameObjectId)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                ReleaseAll(_index.SnapshotOwner(AssetOwner.FromGameObjectId(gameObjectId)));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[VfxSystem] Releasing VFX of a destroyed owner failed. gameObjectId={Id}",
                    gameObjectId);
            }
        }

        /// <summary>テスト用: Spawn の登録段階（索引への登録と notifier の設置の後）で例外を起こす差し込み口。</summary>
        internal Action<VfxHandle>? RegistrationFaultForTest { get; set; }

        internal int LiveInstanceCount => _index.Count;

        private bool QueryCapabilityOnce()
        {
            try
            {
                if (_backend.QueryCapability(out var reason))
                {
                    return true;
                }

                _logger.LogWarning(
                    "[VfxSystem] VFX is not supported in this environment. All VFX requests are ignored. reason={Reason}",
                    reason ?? "(none)");
                return false;
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "[VfxSystem] VFX capability query failed. All VFX requests are ignored as unsupported.");
                return false;
            }
        }

        private bool CanAccept(in VfxSpawnRequest request, AssetOwner owner)
        {
            // 破棄されうる UnityEngine.Object は Unity の == で判定する（偽 null を null と見なすため）。
            if (request.Source == null)
            {
                return false;
            }

            if (request.HasParent && request.Parent == null)
            {
                return false;
            }

            return IsOwnerAlive(owner);
        }

        private bool IsOwnerAlive(AssetOwner owner)
        {
            // default(AssetOwner) は Kind=App だが Id が null で、索引のキーにもできない。
            if (owner.Id == null)
            {
                return false;
            }

            // AssetOwnerKind に値が増えたら受理規則も見直す。知らない種類を黙って App 扱いにしない。
            switch (owner.Kind)
            {
                case AssetOwnerKind.App:
                case AssetOwnerKind.Manual:
                    return true;
                case AssetOwnerKind.Scene:
                    return _index.IsSceneLifecycleOpen
                           && _sceneQuery != null
                           && !_index.IsTerminating(owner.Id)
                           && _sceneQuery.IsSceneLoaded(owner.Id);
                case AssetOwnerKind.GameObject:
                    // FromGameObjectId で作られた owner は BoundObject が null で、破棄済みと同じく拒否する。
                    return owner.BoundObject != null;
                default:
                    return false;
            }
        }

        private void EnsureDestroyNotifier(AssetOwner owner)
        {
            var go = owner.BoundObject;
            if (go == null)
            {
                return;
            }

            foreach (var existing in go.GetComponents<VfxOwnerDestroyNotifier>())
            {
                if (ReferenceEquals(existing.SystemKey, this))
                {
                    return;
                }
            }

            go.AddComponent<VfxOwnerDestroyNotifier>().Initialize(_onOwnerDestroyed, owner.GameObjectId, this);
        }

        private void OnSceneEvent(int generation, SceneEvent sceneEvent)
        {
            if (_disposed || generation != _bindGeneration)
            {
                return;
            }

            try
            {
                var identity = sceneEvent.SceneIdentify;
                if (string.IsNullOrWhiteSpace(identity))
                {
                    return;
                }

                switch (sceneEvent.Type)
                {
                    case SceneEventType.StateChanged:
                        if (sceneEvent.State == SceneState.PreUnloading || sceneEvent.State == SceneState.LoadCanceled)
                        {
                            TerminateScene(identity);
                        }
                        else if (sceneEvent.State == SceneState.PreLoading)
                        {
                            // 同じ identity の再ロード。SceneDirector は前の個体の Removed / CancelCleanedUp の後にしか出さない。
                            _index.ExitTerminal(identity);
                        }

                        break;
                    case SceneEventType.Removed:
                    case SceneEventType.CancelCleanedUp:
                        // 保険。通常は PreUnloading / LoadCanceled で回収済みで、ここでは何も起きない。
                        // 終端からは外さない（外すのは次の PreLoading だけ）。
                        TerminateScene(identity);
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[VfxSystem] Handling a scene event failed. event={Event}", sceneEvent.ToString());
            }
        }

        private void OnSceneEventsCompleted(int generation)
        {
            if (_disposed || generation != _bindGeneration)
            {
                return;
            }

            try
            {
                // SceneDirector の Dispose。完了の印を回収より先に立て、BindSceneLifecycle し直すまで Scene 所有を拒否する。
                _index.IsSceneLifecycleOpen = false;
                ReleaseAll(_index.SnapshotSceneOwned());
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[VfxSystem] Handling scene lifecycle completion failed.");
            }
        }

        private void TerminateScene(string sceneIdentity)
        {
            // 終端中集合への登録を回収より先に行う。回収中の Destroy から再入した Spawn をこれで拒否する。
            _index.EnterTerminal(sceneIdentity);
            ReleaseAll(_index.SnapshotOwner(AssetOwner.Scene(sceneIdentity)));
        }

        private bool IsBindSuperseded(int generation) => _disposed || generation != _bindGeneration;

        private void DisposeSceneSubscription(IDisposable? subscription)
        {
            try
            {
                subscription?.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[VfxSystem] Unsubscribing from scene events failed.");
            }
        }

        private static void ReleaseAll(List<VfxHandle> handles)
        {
            foreach (var handle in handles)
            {
                handle.Release();
            }
        }

        private void LogHandleFailure(VfxHandle handle, Exception exception)
        {
            _logger.LogError(exception,
                "[VfxSystem] VFX backend call failed. The instance was released. id={Id} owner={Owner}",
                handle.Id.Value,
                handle.Owner.ToString());
        }
    }
}
