#nullable enable

using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Modifiers
{
    /// <summary>
    /// 減衰しながら位置を揺らすシェイク Modifier。残り時間に比例して振幅を減衰させ、
    /// 3 軸に異なる周波数を掛けて機械的な往復に見えないようにする。時間切れで自己除去する（Apply が false を返す）。
    /// </summary>
    public sealed class ShakeModifier : ICameraPoseModifier
    {
        private readonly Vector3 _amplitude;
        private readonly float _initialDuration;
        private float _remainingDuration;
        private float _phase;

        public ShakeModifier(Vector3 amplitude, float duration)
        {
            _amplitude = amplitude;
            _initialDuration = duration;
            _remainingDuration = duration;
        }

        /// <inheritdoc />
        public bool Apply(ref CameraPose pose, float deltaTime)
        {
            if (_remainingDuration <= 0f)
            {
                return false;
            }

            _remainingDuration -= deltaTime;
            var decay = _initialDuration <= 0f
                ? 0f
                : Mathf.Clamp01(_remainingDuration / _initialDuration);

            _phase += deltaTime * 20f;
            var offset = new Vector3(
                Mathf.Sin(_phase) * _amplitude.x,
                Mathf.Sin(_phase * 1.3f) * _amplitude.y,
                Mathf.Sin(_phase * 0.7f) * _amplitude.z) * decay;

            pose = pose.WithPosition(pose.Position + offset);
            return _remainingDuration > 0f;
        }
    }
}
