#nullable enable

using OneStarMaker.Runtime;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.BackgroundApplier;
using OneStarMaker.Runtime.CameraSystem.Cinemachine;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UpdateSystem.Api;
using System;
using UnityEngine;
using RuntimeCameraSystem = OneStarMaker.Runtime.CameraSystem.Core.CameraSystem;

namespace SampleGame.DependOnAll
{
    /// <summary>
    /// アプリケーション起動エントリーポイント。
    /// AbstractApplicationInitializer の abstract メソッドを実装する。
    /// </summary>
    public sealed class AppInitializer : AbstractApplicationInitializer
    {
        private static readonly AppInitializer s_instance = new();

        private CameraSystemHost? _cameraSystemHost;
        private CinemachineCameraBackend? _cameraBackend;
        private RuntimeCameraSystem? _cameraSystem;
        private CameraSystemUpdateElement? _cameraUpdateElement;
        private CameraBackgroundApplier? _cameraBackgroundApplier;
        private bool _cameraQuittingHandlerRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Sub()
        {
            // Domain Reload 無効時も前セッションの常駐 Host を先に片付けてから Framework を初期化する。
            s_instance.ReleaseCameraSystem();
            BootstrapSubsystemRegistration(s_instance);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Before()
        {
            BootstrapBeforeSceneLoad(s_instance);

            // CameraSystem は Addressables・SceneDirector・UICommon を必要としない。
            // UpdateSystemHost の生成直後に View_Main を確保すれば、AfterSceneLoad の非同期 bootstrap 中も
            // 有効な Camera / AudioListener が存在し、旧シーンカメラを無効化した構成でも描画と音声が途切れない。
            if (s_instance.UpdateCoordinator != null)
            {
                s_instance.InitializeCameraSystem();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void After() => BootstrapAfterSceneLoad(s_instance);

        protected override ISceneFactory CreateSceneFactory()
        {
            // Bootstrap が構成した唯一の ILoggerFactory を Game 層へ渡す。
            // Game 層で AppLoggerFactory を再生成すると、rolling file と DebugSocket への出力経路が分断される。
            var loggerFactory = LoggerFactory
                ?? throw new InvalidOperationException(
                    "ILoggerFactory is not initialized. Ensure BeforeSceneLoad completed successfully.");
            return new GameSceneFactory(loggerFactory, _cameraSystem, _cameraBackgroundApplier);
        }

        protected override string GetUICommonPrefabAddress()
            => "Assets/OneStarMaker/Scenes/UIScene.unity";

        protected override string GetSceneResourceMapAddress()
            => "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";

        protected override ILoadingDisplay CreateLoadingDisplay()
            => new NullLoadingDisplay();

        protected override string GetConfigFilePath()
            => "Assets/SampleGame/Config/app-config.json";

        protected override string GetEnvironmentVariablePrefix()
            => "SAMPLEGAME_";

        /// <summary>
        /// Framework の AfterSceneLoad 処理は、サービス初期化後にも SceneDirector 構築や初回シーン追加を行う。
        /// その後段で失敗した場合も CameraSystemHost は DontDestroyOnLoad に残るため、
        /// AppInitializer が所有する CameraSystem 一式をここで回収して次回初期化へ持ち越さない。
        /// </summary>
        protected override void OnAfterSceneLoadInitializationFailed(string stage, Exception exception)
        {
            ReleaseCameraSystem();
        }

        private void InitializeCameraSystem()
        {
            if (_cameraSystemHost != null)
            {
                return;
            }

            try
            {
                // UpdateSystemHost 構築後の BeforeSceneLoad で一度だけ構築する。Host は DontDestroyOnLoad で常駐し、
                // シーン遷移後も AppInitializer が所有し続ける（シーン側へ複製配置しない）。
                var host = CameraSystemHost.Initialize();
                // Host の生成が成功した時点で先に所有権を記録する。
                // 後続の Backend / Element 登録で例外になっても catch の ReleaseCameraSystem が
                // 静的 Host を確実に Dispose でき、Domain Reload 無効時に残存しない。
                _cameraSystemHost = host;

                var backend = new CinemachineCameraBackend(host);
                _cameraBackend = backend;

                var system = new RuntimeCameraSystem(backend);
                _cameraSystem = system;

                var backgroundApplier = new CameraBackgroundApplier(host);
                _cameraBackgroundApplier = backgroundApplier;
                // 旧シーン Main Camera を使わないため、AfterSceneLoad の非同期処理中も View_Main 自身が
                // 意図した背景を描画する必要がある。OutGameScene 側でも同じ設定を行うが、ここは起動空白を
                // 作らないための application-scope 既定値として先に確定する。
                backgroundApplier.SetClearFlag(system.MainView, ClearFlag.Color, Color.black);

                // CameraSystem は独自 MonoBehaviour を持たず、UpdateSystem の Camera Layer が唯一の更新入口になる。
                // Element 内で Brain の ManualUpdate → Modifier / Snapshot Tick の順を固定するため、
                // Unity の LateUpdate 実行順や別経路の Tick に依存せず I-1 の観測整合性を守れる。
                var updateElement = CameraSystemUpdateElement.Create(backend, system);
                var coordinator = UpdateCoordinator
                                  ?? throw new InvalidOperationException(
                                      "CameraSystem の初期化前に UpdateSystem が利用可能である必要があります。");
                if (!coordinator.RegisterElement(
                        UpdateLayerIds.Camera,
                        updateElement,
                        layerOrder: UpdateLayerIds.CameraLayerOrder))
                {
                    throw new InvalidOperationException(
                        "CameraSystem の UpdateElement を登録できませんでした。UpdateSystem の初期化順を確認してください。");
                }

                // CameraSystem はシーン所属ではなくアプリ常駐であり、初回 SceneDirector の ViewIn 中にも
                // View_Main を更新し続けなければならない。Scene 安定待ちの Runtime facade は使わず、
                // Bootstrap の main thread でこの Element を即時 active 化する。
                // この時点では AppInitializer が登録した Element だけを対象にし、以後の scene object 登録は
                // 従来どおり UpdateSystemHost の scene stability gate を経由する。
                coordinator.ActivatePendingRegistrations();

                _cameraUpdateElement = updateElement;

                RegisterCameraSystemQuittingHandler();
            }
            catch (Exception ex)
            {
                ReleaseCameraSystem();
                Debug.LogException(ex);
                throw;
            }
        }

        private void RegisterCameraSystemQuittingHandler()
        {
            if (_cameraQuittingHandlerRegistered)
            {
                return;
            }

            Application.quitting += ReleaseCameraSystem;
            _cameraQuittingHandlerRegistered = true;
        }

        /// <summary>
        /// 常駐 CameraSystemHost の解放。AppInitializer が生成から破棄までを担う。
        /// Dispose は冪等のため、SubsystemRegistration と Application.quitting の双方から安全に呼べる。
        /// </summary>
        private void ReleaseCameraSystem()
        {
            Application.quitting -= ReleaseCameraSystem;
            _cameraQuittingHandlerRegistered = false;

            // Unregister は構造変更フェーズまで遅延する。Host を破棄した同フレームに Element が
            // 再実行される事故を防ぐため、まず no-op 化してから登録解除を要求する。
            _cameraUpdateElement?.Deactivate();
            if (_cameraUpdateElement != null)
            {
                UpdateCoordinator?.UnregisterElement(_cameraUpdateElement);
                _cameraUpdateElement = null;
            }

            // GameSceneFactory が生成済みの場合でも、破棄済み Host を保持する applier を後段へ渡さない。
            // CameraSystem と BackgroundApplier は同じ Host の寿命に従うため、必ず同じ解放境界で null に戻す。
            _cameraBackgroundApplier = null;
            _cameraSystem = null;
            _cameraBackend = null;

            _cameraSystemHost?.Dispose();
            _cameraSystemHost = null;
        }
    }
}
