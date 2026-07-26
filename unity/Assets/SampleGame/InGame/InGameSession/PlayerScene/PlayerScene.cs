#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.InGame.Player;
using SampleGame.InGame.Streaming;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame
{
    /// <summary>
    /// 飛行プレイヤーを持つ論理シーン。
    /// Payload の PlayerScene.unity にリグを置き、ここでは参照解決と CameraSystem 配線のみを行う。
    /// 自前 Camera / AudioListener / DontDestroyOnLoad / HUD は持たない。
    /// </summary>
    /// <remarks>
    /// Cell Streaming では Level Ensure を待たない。
    /// Focus（Flight）を Session に登録すれば、WorldStreamingController がセルを載せる。
    /// </remarks>
    public sealed class PlayerScene : SceneBase
    {
        private readonly ILogger<PlayerScene> _logger;
        private readonly ICameraSystem _cameraSystem;
        private readonly ICameraBackgroundApplier _cameraBackgroundApplier;

        private PlayerRigBindings? _rig;
        private FlyController? _flyer;
        private LogicalCamera? _followCamera;
        private CameraStackHandle? _stackHandle;
        private CancellationTokenSource? _bootstrapCts;
        private IInGameSessionServices? _sessionServices;

        public FlyController? Flyer => _flyer;

        public PlayerScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory,
            ICameraSystem cameraSystem,
            ICameraBackgroundApplier cameraBackgroundApplier)
            : base(sceneResource, sceneQuery, sceneController)
        {
            _logger = loggerFactory.CreateLogger<PlayerScene>();
            // CameraSystem は任意依存ではない。Composition Root で失敗を確定させ、破棄済み Host への遅延事故を防ぐ。
            _cameraSystem = cameraSystem ?? throw new System.ArgumentNullException(nameof(cameraSystem));
            _cameraBackgroundApplier = cameraBackgroundApplier
                ?? throw new System.ArgumentNullException(nameof(cameraBackgroundApplier));
            _logger.ZLogInformation($"Create PlayerScene");
        }

        protected override UniTask OnLoadedImpl(CancellationToken ct)
        {
            // Unity シーン payload から配線コンポーネントを解決する（ランタイム組み立て禁止）。
            _rig = FindRootComponent<PlayerRigBindings>()
                ?? throw new System.InvalidOperationException(
                    "PlayerScene.unity に PlayerRigBindings がありません。Editor メニューでシーンを再生成してください。");

            _flyer = _rig.Flyer;
            if (_flyer == null)
            {
                throw new System.InvalidOperationException("PlayerRigBindings.Flyer が未設定です。");
            }

            _flyer.Configure(_rig.LookAtTarget);
            _flyer.InputEnabled = false;

            BindGameplayCamera(_rig);
            return UniTask.CompletedTask;
        }

        protected override UniTask OnStabledImpl()
        {
            // 親 Session の OnLoaded（Driver 生成）より先に走り得るため、
            // ここではブロックせずバックグラウンド起動する（デッドロック防止）。
            _bootstrapCts = new CancellationTokenSource();
            BootstrapAsync(_bootstrapCts.Token).Forget();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnPreUnLoadedImpl()
        {
            _bootstrapCts?.Cancel();
            return UniTask.CompletedTask;
        }

        protected override UniTask OnAfterUnLoadedImpl()
        {
            _bootstrapCts?.Dispose();
            _bootstrapCts = null;

            if (_sessionServices != null && _flyer != null)
            {
                _sessionServices.UnregisterFlight(_flyer);
            }

            // Push ハンドルの Dispose = Pop。続けて managed 実体も破棄（Pop だけでは CM GO が残る）。
            _stackHandle?.Dispose();
            _stackHandle = null;
            if (_followCamera != null)
            {
                _cameraSystem.ReleaseManagedCamera(_followCamera);
                _followCamera = null;
            }

            _sessionServices = null;
            _flyer = null;
            _rig = null;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// CameraSystem に Gameplay 論理カメラを積み、Follow/LookAt をシーン内 Transform へ結ぶ。
        /// View_Main 以外の Unity Camera は生成しない。
        /// </summary>
        private void BindGameplayCamera(PlayerRigBindings rig)
        {
            _followCamera = _cameraSystem.CreateManagedCamera(_cameraSystem.MainView, "player-follow");
            // 生成時 Configure はデフォルト値。書き換え後は ApplyLens で CM 実体へ再反映する。
            _followCamera.FieldOfViewDegrees = 70f;
            _followCamera.NearClip = 0.2f;
            _followCamera.FarClip = 2000f;
            _cameraSystem.ApplyLens(_followCamera);

            _cameraSystem.SetFollow(_followCamera, rig.FollowTarget);
            _cameraSystem.SetLookAt(_followCamera, rig.LookAtTarget);

            _stackHandle = _cameraSystem.MainView.Push(
                _followCamera,
                CameraLayer.Gameplay,
                CameraBlendSpec.Cut);

            _logger.ZLogInformation($"Gameplay camera pushed (Follow/LookAt bound)");
        }

        private async UniTaskVoid BootstrapAsync(CancellationToken ct)
        {
            try
            {
                // NecessaryAlways 子の OnStabled は親 OnLoaded（Driver 生成）より先に走り得る。
                // Streaming が使える状態（IsStreamingActive）になるまで待ち、Focus を登録する。
                using var hubTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                hubTimeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
                try
                {
                    await UniTask.WaitUntil(
                        () =>
                        {
                            _sessionServices = TryResolveSessionServices();
                            return _sessionServices is { IsStreamingActive: true };
                        },
                        cancellationToken: hubTimeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.ZLogError(
                        $"Player bootstrap aborted: InGameSession Streaming が 15 秒以内に現れませんでした。");
                    return;
                }

                if (_sessionServices == null || _flyer == null)
                {
                    return;
                }

                var spawn = WorldCellCatalog.SpawnPosition();
                _flyer.Teleport(spawn, Vector3.forward);
                _flyer.InputEnabled = true;

                // Focus 供給を開始 → Driver の WaitUntil が解除され、desired セルが載り始める。
                _sessionServices.RegisterFlight(_flyer);
                ApplyDemoLook();

                _logger.ZLogInformation($"Player ready at Cell stream spawn {spawn}");
            }
            catch (OperationCanceledException)
            {
                // teardown
            }
            catch (Exception ex)
            {
                // 失敗時も入力を上げてカーソルを戻し、完全フリーズを避ける。
                _logger.ZLogError(ex, $"Player bootstrap failed");
                if (_flyer != null)
                {
                    _flyer.InputEnabled = true;
                }
            }
        }

        private IInGameSessionServices? TryResolveSessionServices()
        {
            var parent = SceneResource.Parent;
            if (parent == null)
            {
                return null;
            }

            return SceneQuery.GetLoadedScene(parent.Identity) as IInGameSessionServices;
        }

        /// <summary>
        /// 実証用の単純な空模様。季節テーマは捨てたので固定トーンにする。
        /// Fog / Ambient は暫定で RenderSettings 直書き（環境オーナーは将来 Environment 子シーン側へ）。
        /// </summary>
        private void ApplyDemoLook()
        {
            try
            {
                var sky = new Color(0.45f, 0.7f, 0.95f);
                _cameraBackgroundApplier.SetClearFlag(
                    _cameraSystem.MainView,
                    ClearFlag.Color,
                    sky);

                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.75f, 0.85f, 0.95f);
                RenderSettings.fogDensity = 0.0035f;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.Lerp(sky, Color.white, 0.4f);
            }
            catch (Exception ex)
            {
                _logger.ZLogWarning($"ApplyDemoLook failed: {ex.Message}");
            }
        }
    }
}
