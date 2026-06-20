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
        private bool _disposed;

        public UpdateSystemHost()
        {
            Coordinator = new UpdateCoordinator();

            var host = new GameObject("[UpdaterHost]");
            UnityEngine.Object.DontDestroyOnLoad(host);
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
            if (!_activationRequested || _unstableSceneIds.Count > 0)
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
