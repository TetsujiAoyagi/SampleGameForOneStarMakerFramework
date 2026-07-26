#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.InGame.LevelStreaming;
using SampleGame.InGame.Player;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame
{
    /// <summary>
    /// 飛行プレイヤーを持つ論理シーン。
    /// Payload の PlayerScene.unity にリグを置き、ここでは参照解決と CameraSystem 配線のみを行う。
    /// 自前 Camera / AudioListener / DontDestroyOnLoad / HUD は持たない。
    /// </summary>
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
            // 親 Session の OnLoaded 完了や初期 Level Add を待つ必要があるため、
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
                if (_sessionServices.Coordinator != null)
                {
                    _sessionServices.Coordinator.CurrentLevelChanged -= OnCurrentLevelChanged;
                }
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
                // 親 Session サービス面を解決（兄弟 UI とのやり取りは必ずここ経由）。
                // NecessaryAlways 子の OnStabled は親 OnLoaded（Coordinator 生成）より先に走り得るため、
                // ここで親サービスと Coordinator の出現を待ってから初期 Level を決める。
                // SpringLevel 固定フォールバックは夏指定時に永久待ちになるので使わない。
                // 親が初期 Level を解決できないと Coordinator が作られない。
                // 無期限待ちにしないようタイムアウトし、失敗時は入力を上げずログして抜ける。
                using var hubTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                hubTimeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
                try
                {
                    await UniTask.WaitUntil(
                        () =>
                        {
                            _sessionServices = TryResolveSessionServices();
                            return _sessionServices?.Coordinator != null;
                        },
                        cancellationToken: hubTimeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.ZLogError(
                        $"Player bootstrap aborted: InGameSession Coordinator が 15 秒以内に現れませんでした。InGameArgs.TransitionLevel または季節 Level からの Play を確認してください。");
                    return;
                }

                if (_sessionServices?.Coordinator == null)
                {
                    return;
                }

                var coordinator = _sessionServices.Coordinator;
                var initialLevel = coordinator.CurrentLevelIdentity;

                // EnsureLevelLoadedAsync は Stable（地形生成完了）まで待つ。
                await coordinator.EnsureLevelLoadedAsync(initialLevel, ct);

                if (_flyer == null)
                {
                    return;
                }

                var spawn = SeasonWorldCatalog.SpawnPosition(initialLevel);
                _flyer.Teleport(spawn, Vector3.forward);
                _flyer.InputEnabled = true;

                _sessionServices.RegisterFlight(_flyer);
                ApplySeasonLook(initialLevel);

                if (_sessionServices.Coordinator != null)
                {
                    _sessionServices.Coordinator.CurrentLevelChanged -= OnCurrentLevelChanged;
                    _sessionServices.Coordinator.CurrentLevelChanged += OnCurrentLevelChanged;
                }

                _logger.ZLogInformation($"Player ready at {initialLevel} {spawn}");
            }
            catch (OperationCanceledException)
            {
                // teardown
            }
            catch (Exception ex)
            {
                // 初期 Level 失敗時も入力を上げてカーソルを戻し、完全フリーズを避ける。
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

        private void OnCurrentLevelChanged(string identity)
        {
            ApplySeasonLook(identity);
        }

        /// <summary>
        /// 季節スカイは CameraSystem の MainView 背景へ書く（自前 Camera 禁止）。
        /// Fog / Ambient は暫定で RenderSettings 直書き（環境オーナーシップは Level 書き直し時に移す）。
        /// </summary>
        private void ApplySeasonLook(string identity)
        {
            try
            {
                var def = SeasonWorldCatalog.Get(identity);
                _cameraBackgroundApplier.SetClearFlag(
                    _cameraSystem.MainView,
                    ClearFlag.Color,
                    def.Sky);

                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = def.Fog;
                RenderSettings.fogDensity = 0.0045f;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.Lerp(def.Sky, Color.white, 0.35f);
            }
            catch (System.Exception ex)
            {
                _logger.ZLogWarning($"ApplySeasonLook failed: {ex.Message}");
            }
        }
    }
}
