#nullable enable

using System;
using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Hosting
{
    /// <summary>
    /// 純 C# の CameraSystem を Unity のフレームループへ橋渡しする MonoBehaviour。
    /// LateUpdate で毎フレーム Tick を呼び、カメラ確定を他の Update 後（移動確定後）に行う。
    /// </summary>
    public sealed class CameraSystemUpdateAdapter : MonoBehaviour
    {
        private OneStarMaker.Runtime.CameraSystem.Core.CameraSystem? _cameraSystem;

        public void Initialize(OneStarMaker.Runtime.CameraSystem.Core.CameraSystem cameraSystem)
        {
            _cameraSystem = cameraSystem ?? throw new ArgumentNullException(nameof(cameraSystem));
        }

        private void LateUpdate()
        {
            _cameraSystem?.Tick(Time.deltaTime);
        }
    }
}
