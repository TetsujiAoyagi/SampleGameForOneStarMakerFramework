#nullable enable

using System;
using OneStarMaker.Foundation.UpdateSystem;
using UnityEngine;

namespace OneStarMaker.Runtime.UpdateSystem.Hosting
{
    /// <summary>
    /// Unity PlayerLoop と UpdateCoordinator を接続する最小 driver。
    /// 実際の Element 実行方式は coordinator -> layer -> backend へ委譲し、
    /// このクラスは frame 境界を通知することだけに集中する。
    /// </summary>
    internal sealed class UpdaterDriver : MonoBehaviour
    {
        private UpdateSystemHost? _runtimeHost;

        public void Initialize(UpdateSystemHost runtimeHost)
        {
            _runtimeHost = runtimeHost ?? throw new ArgumentNullException(nameof(runtimeHost));
        }

        private void Update()
        {
            if (_runtimeHost == null)
            {
                return;
            }

            // activation は必ず Update 先頭の単一地点に寄せる。
            // ここで runtime request と scene stable 通知を合流させることで、
            // 「Start 相当 -> Update -> LateUpdate」の順序をフレーム境界で固定する。
            if (_runtimeHost.TryConsumeActivationRequest())
            {
                _runtimeHost.Coordinator.ActivatePendingRegistrations();
            }

            _runtimeHost.Coordinator.RunUpdate(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            if (_runtimeHost == null)
            {
                return;
            }

            _runtimeHost.Coordinator.RunLateUpdate(Time.deltaTime, Time.unscaledDeltaTime);
            _runtimeHost.Coordinator.ApplyMainThreadChanges();
            _runtimeHost.Coordinator.ApplyStructuralChanges();
        }
    }
}
