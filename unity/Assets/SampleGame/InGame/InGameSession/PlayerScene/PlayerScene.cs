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
    /// WaitUntilWorldReady のあと PlayerWorldReadySequence が Teleport → Focus 登録 → 入力 ON する。
    /// </remarks>
    public sealed class PlayerScene : SceneBase
    {
        private readonly ILogger<PlayerScene> _logger;
        private readonly ICameraSystem _cameraSystem;

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
            // Camera clear は AppInitializer / InGameScene が所有する。ここでは fog を書かない。
            if (cameraBackgroundApplier == null)
            {
                throw new System.ArgumentNullException(nameof(cameraBackgroundApplier));
            }
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
                // NecessaryAlways 子の OnStabled は親 OnLoaded（controller 生成）より先に走り得る。
                // 15 秒上限は付けない。失敗時も入力は上げない。
                await UniTask.WaitUntil(
                    () =>
                    {
                        _sessionServices = TryResolveSessionServices();
                        return _sessionServices != null;
                    },
                    cancellationToken: ct);

                if (_sessionServices == null || _flyer == null)
                {
                    return;
                }

                await _sessionServices.WaitUntilWorldReady(ct);
                PlayerWorldReadySequence.Run(
                    _sessionServices,
                    _flyer,
                    WorldCellCatalog.SpawnPosition());

                _logger.ZLogInformation($"Player ready at Cell stream spawn {WorldCellCatalog.SpawnPosition()}");
            }
            catch (OperationCanceledException)
            {
                if (_flyer != null)
                {
                    _flyer.InputEnabled = false;
                }
            }
            catch (Exception ex)
            {
                // 失敗 / キャンセルでも入力 ON には戻さない。完全フリーズ回避を入力解禁で済ませない。
                _logger.ZLogError(ex, $"Player bootstrap failed");
                if (_flyer != null)
                {
                    _flyer.InputEnabled = false;
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
    }
}
