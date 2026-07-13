#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Foundation.UpdateSystem.World;
using OneStarMaker.Runtime.SceneSystem;
using R3;
using UnityEngine;

namespace OneStarMaker.Runtime.UpdateSystem.Hosting
{
    /// <summary>
    /// Unity PlayerLoop と UpdateCoordinator の橋渡しを行う host。
    /// runtime 側の install、scene 安定待ち、driver 管理を集約する。
    /// </summary>
    public class UpdateSystemHost : IDisposable
    {
        private readonly UpdaterDriver _driver;
        private readonly HashSet<string> _unstableSceneIds = new(StringComparer.Ordinal);
        private IDisposable? _sceneEventSubscription;
        private bool _activationRequested = true;
        private bool _sceneDirectorBound;
        private bool _disposed;

        public UpdateSystemHost()
        {
            Coordinator = new UpdateCoordinator();

            var host = new GameObject("[UpdaterHost]");
            // EditMode テストでは DontDestroyOnLoad を呼べない。再生時だけ常駐化し、
            // テスト側は Dispose によって生成した Host を明示的に破棄する。
            if (Application.isPlaying)
            {
                UnityEngine.Object.DontDestroyOnLoad(host);
            }

            _driver = host.AddComponent<UpdaterDriver>();
            _driver.Initialize(this);

            Api.UpdateSystemRuntime.Install(this);
        }

        public UpdateCoordinator Coordinator { get; }

        internal void RequestActivation()
        {
            _activationRequested = true;
        }

        internal bool TryConsumeActivationRequest()
        {
            // SceneDirector が未接続の間は、ロード済みシーン由来 Element の安定性を判定できない。
            // Application 常駐サービス（CameraSystem など）は Bootstrap が Coordinator を直接操作して
            // 明示的に active 化するが、通常の RegisterElement はここで止めて遷移途中の実行を防ぐ。
            if (!_sceneDirectorBound || !_activationRequested || _unstableSceneIds.Count > 0)
            {
                return false;
            }

            _activationRequested = false;
            return true;
        }

        public void BindSceneDirector(SceneDirector sceneDirector)
        {
            if (sceneDirector == null)
            {
                throw new ArgumentNullException(nameof(sceneDirector));
            }

            _sceneEventSubscription?.Dispose();
            _sceneEventSubscription = sceneDirector.OnSceneEvent.Subscribe(OnSceneEvent);
            // 既に pending の Element があるため、SceneDirector 接続後の最初の安定フレームで
            // activation を再評価する。scene event が直ちに届く場合は _unstableSceneIds が優先して停止する。
            _sceneDirectorBound = true;
            _activationRequested = true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _sceneEventSubscription?.Dispose();
            _sceneEventSubscription = null;
            Api.UpdateSystemRuntime.Uninstall(this);

            UnityEngine.Object.Destroy(_driver.gameObject);
        }

        private void OnSceneEvent(SceneEvent sceneEvent)
        {
            switch (sceneEvent.Type)
            {
                case SceneEventType.StateChanged:
                    TrackSceneStability(sceneEvent);
                    break;

                case SceneEventType.Added:
                    _unstableSceneIds.Remove(sceneEvent.SceneIdentify);
                    _activationRequested = true;
                    break;

                case SceneEventType.Removed:
                case SceneEventType.CancelCleanedUp:
                    _unstableSceneIds.Remove(sceneEvent.SceneIdentify);
                    break;
            }
        }

        private void TrackSceneStability(SceneEvent sceneEvent)
        {
            switch (sceneEvent.State)
            {
                case SceneState.Stable:
                case SceneState.AfterUnloading:
                    _unstableSceneIds.Remove(sceneEvent.SceneIdentify);
                    break;

                default:
                    _unstableSceneIds.Add(sceneEvent.SceneIdentify);
                    break;
            }
        }
    }
}
