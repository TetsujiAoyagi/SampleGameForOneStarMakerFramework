#nullable enable

using System;
using Microsoft.Extensions.Logging;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Foundation.UpdateSystem.World;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UpdateSystem.Api;
using R3;
using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// App 常駐の <see cref="VfxSystem"/> 一式を組み立て、Tick の登録、Scene lifecycle への接続、解放の順序を受け持つ。
    /// <para>
    /// <c>AbstractApplicationInitializer</c> が BeforeSceneLoad で <see cref="Create"/> し、SceneDirector の生成直後に
    /// <see cref="BindSceneLifecycle"/>、<c>ReleaseAll</c> の最初（logger・UpdateSystemHost・SceneDirector・
    /// AssetManagement より前）に <see cref="Dispose"/> する。順序が initializer の private な処理の間にあり、
    /// Game の hook からは割り込めないので Framework に置く。判定ロジックは持たず、orchestration だけを行う。
    /// </para>
    /// <para>
    /// Tick 要素は Scene 安定待ちの gate を経由せず即時 active にする（gate が開かない起動経路でも反映が止まらない）。
    /// 時間の反映点は <c>Vfx</c> Layer の LateUpdate で、全 Layer の Update の後・VFX の simulation の前にある。
    /// どの Layer の Update で pause・倍率を変えても同じフレームに反映される。<c>Vfx</c> Layer 自身は時間の領域に
    /// しない（pause されると全 VFX への反映が止まる）。
    /// </para>
    /// <para>
    /// 時間の値の出所（<see cref="IVfxTimeSource"/>）もここが実装する。<c>UnityEngine.Time</c> の delta と、
    /// UpdateCoordinator の Layer の状態を、Layer を作らずに読む。既定の時間の領域である Gameplay Layer は、
    /// 生成時に既定の順序で作って存在を保証する（Game の要素より先に別の順序で作られることを防ぐ）。
    /// </para>
    /// </summary>
    internal sealed class VfxRuntimeHost : IUpdateElement, IVfxTimeSource, IDisposable
    {
        private readonly IVfxBackend _backend;
        private ILogger<VfxRuntimeHost>? _logger;
        private VfxSystem? _system;
        private UpdateCoordinator? _coordinator;

        // Dispose で先に false にする。登録解除は構造変更フェーズまで遅れるので、その間に Tick を転送しない。
        private bool _forwardTicks;
        private bool _disposed;

        private VfxRuntimeHost(IVfxBackend backend)
        {
            _backend = backend;
        }

        /// <summary>Game が使う VFX の入口。未構成・非対応でも非 null で、その場合は何もしない handle を返す。</summary>
        internal IVfxSystem System => _system ?? throw new ObjectDisposedException(nameof(VfxRuntimeHost));

        /// <summary>テスト用: Tick 要素が coordinator で active になり、まだ Dispose されていないか。</summary>
        internal bool IsTickElementActive { get; private set; }

        /// <summary>テスト用: Scene lifecycle への接続が成功したか（失敗、または Dispose 後は false）。</summary>
        internal bool IsSceneLifecycleBound { get; private set; }

        /// <summary>
        /// テスト用: 非 null なら <c>UnityEngine.Time</c> の代わりにこの delta を時間の値として使う
        /// （EditMode では <c>Time.deltaTime</c> を制御できないため）。
        /// </summary>
        internal (float DeltaTime, float UnscaledDeltaTime)? FrameTimeForTest { get; set; }

        float IVfxTimeSource.DeltaTime => FrameTimeForTest?.DeltaTime ?? Time.deltaTime;

        float IVfxTimeSource.UnscaledDeltaTime => FrameTimeForTest?.UnscaledDeltaTime ?? Time.unscaledDeltaTime;

        float IVfxTimeSource.MaximumDeltaTime => Time.maximumDeltaTime;

        /// <summary>
        /// 一式を作る。<paramref name="backend"/> の所有権はこのメソッドに入った時点で host に移る。
        /// 途中で失敗したら、作りかけの VfxSystem と backend を Dispose してから例外を外へ出す
        /// （失敗した backend が能力判定の結果や DontDestroyOnLoad の root を残さないため）。
        /// </summary>
        /// <param name="backend">null なら未構成として <see cref="UnconfiguredVfxBackend"/> を使う。</param>
        internal static VfxRuntimeHost Create(
            IVfxBackend? backend,
            ILoggerFactory loggerFactory,
            UpdateCoordinator coordinator,
            float gracefulStopTimeoutSeconds = 10f)
        {
            var host = new VfxRuntimeHost(backend ?? new UnconfiguredVfxBackend());
            try
            {
                if (loggerFactory == null)
                {
                    throw new ArgumentNullException(nameof(loggerFactory));
                }

                if (coordinator == null)
                {
                    throw new ArgumentNullException(nameof(coordinator));
                }

                host._logger = loggerFactory.CreateLogger<VfxRuntimeHost>();

                // 既定の時間の領域。UpdateBehaviourAdapter の既定と同じ id・順序なので、既存の Layer と同一になる。
                coordinator.GetOrCreateLayer(UpdateLayerIds.Gameplay, UpdateLayerIds.GameplayLayerOrder);

                // 時間源は coordinator を読むので、VfxSystem より先に記録する。
                host._coordinator = coordinator;

                // VfxSystem の ctor が能力判定を 1 回だけ行う。
                host._system = new VfxSystem(
                    host._backend,
                    loggerFactory.CreateLogger<VfxSystem>(),
                    gracefulStopTimeoutSeconds,
                    host);

                if (!coordinator.RegisterElement(UpdateLayerIds.Vfx, host, UpdateLayerIds.VfxLayerOrder))
                {
                    throw new InvalidOperationException(
                        "The VFX update element could not be registered. Check the UpdateSystem initialization order.");
                }

                // 登録できた時点から転送する。以降で失敗しても Dispose が登録を解除できる（coordinator は記録済み）。
                host._forwardTicks = true;
                coordinator.ActivatePendingRegistrations();
            }
            catch
            {
                host.Dispose();
                throw;
            }

            return host;
        }

        /// <summary>
        /// Scene lifecycle に接続する。SceneDirector の生成直後、最初の Scene をロードする前に呼ぶ。
        /// 接続に失敗しても起動は止めない。VfxSystem は失敗時に Scene 所有の Spawn を拒否する状態に戻っているので、
        /// Error を 1 回出すだけにする（App / Manual / Bind 所有の VFX は使い続けられる）。
        /// </summary>
        internal void BindSceneLifecycle(Observable<SceneEvent> sceneEvents, ISceneQuery sceneQuery)
        {
            if (_disposed || _system == null)
            {
                return;
            }

            IsSceneLifecycleBound = false;
            try
            {
                _system.BindSceneLifecycle(sceneEvents, sceneQuery);
                IsSceneLifecycleBound = true;
            }
            catch (Exception e)
            {
                LogError(e, "[VfxRuntimeHost] Binding the VFX system to the scene lifecycle failed. Scene-owned VFX requests are rejected.");
            }
        }

        /// <summary>
        /// 要素を no-op にする → 生きている coordinator から登録解除する → VfxSystem を Dispose する → backend を Dispose する。
        /// backend の Dispose を最後に置くのは、VfxSystem の回収が backend の Destroy を呼ぶため。2 回目以降は何もしない。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _forwardTicks = false;
            IsTickElementActive = false;
            IsSceneLifecycleBound = false;

            var coordinator = _coordinator;
            _coordinator = null;
            if (coordinator != null)
            {
                try
                {
                    // 登録の前に失敗した場合も呼ばれる。未登録なら false が返るだけ。
                    coordinator.UnregisterElement(this);
                }
                catch (Exception e)
                {
                    LogError(e, "[VfxRuntimeHost] Unregistering the VFX update element failed.");
                }
            }

            var system = _system;
            _system = null;
            if (system != null)
            {
                try
                {
                    system.Dispose();
                }
                catch (Exception e)
                {
                    LogError(e, "[VfxRuntimeHost] Disposing the VFX system failed.");
                }
            }

            if (_backend is IDisposable disposableBackend)
            {
                try
                {
                    disposableBackend.Dispose();
                }
                catch (Exception e)
                {
                    LogError(e, "[VfxRuntimeHost] Disposing the VFX backend failed.");
                }
            }
        }

        void IUpdateElement.OnElementStart()
        {
            if (_forwardTicks)
            {
                IsTickElementActive = true;
            }
        }

        void IUpdateElement.OnElementUpdate(in UpdateFrameContext context)
        {
        }

        void IUpdateElement.OnElementLateUpdate(in UpdateFrameContext context)
        {
            if (!_forwardTicks)
            {
                return;
            }

            // 反映点。時間の値は IVfxTimeSource（この host）から読むので、delta 引数は使われない。
            // VfxSystem.Tick は自分で例外を捕捉する（他の UpdateSystem 要素の Tick を止めない）。
            _system?.Tick(context.UnscaledDeltaTime);
        }

        bool IVfxTimeSource.TryGetLayerState(string layerId, out bool isPaused, out float timeScale)
        {
            var coordinator = _coordinator;
            if (coordinator != null && coordinator.TryGetLayer(layerId, out var layer))
            {
                isPaused = layer.IsPaused;
                timeScale = layer.TimeScale;
                return true;
            }

            isPaused = false;
            timeScale = 1f;
            return false;
        }

        private void LogError(Exception exception, string message)
        {
            // logger の生成前に失敗した場合（logger factory の失敗）でも、解放の失敗を黙って捨てない。
            try
            {
                if (_logger != null)
                {
                    _logger.LogError(exception, message);
                }
                else
                {
                    Debug.LogException(exception);
                }
            }
            catch
            {
            }
        }
    }
}
