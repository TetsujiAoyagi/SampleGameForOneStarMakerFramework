#nullable enable

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneStarMaker.Runtime.Rendering.Environments
{
    /// <summary>
    /// RenderSettings と bind された太陽 Light への唯一の Unity I/O。
    /// 世代番号や owner は見ない。呼ばれたら装置へ書く／退避した装置へ戻すだけ。
    /// S-4c は URP 参照を足さない。GlobalVolumeWeight は preset と Fake の観測用で、ここでは適用しない。
    /// </summary>
    public sealed class UnityRenderEnvironmentSink : IRenderEnvironmentSink
    {
        private bool _captured;
        private bool _fog;
        private FogMode _fogMode;
        private Color _fogColor;
        private float _fogDensity;
        private AmbientMode _ambientMode;
        private Color _ambientLight;
        private Color _ambientSkyColor;
        private Color _ambientEquatorColor;
        private Color _ambientGroundColor;
        private float _ambientIntensity;
        private Light _sun = null!;

        public void CaptureBaseline()
        {
            _fog = RenderSettings.fog;
            _fogMode = RenderSettings.fogMode;
            _fogColor = RenderSettings.fogColor;
            _fogDensity = RenderSettings.fogDensity;
            _ambientMode = RenderSettings.ambientMode;
            _ambientLight = RenderSettings.ambientLight;
            // Apply は Flat と ambientLight だけを書く。Skybox / Trilight の評価は
            // sky / equator / ground / intensity 側にあるので、ambientLight だけ戻しても baseline にならない。
            _ambientSkyColor = RenderSettings.ambientSkyColor;
            _ambientEquatorColor = RenderSettings.ambientEquatorColor;
            _ambientGroundColor = RenderSettings.ambientGroundColor;
            _ambientIntensity = RenderSettings.ambientIntensity;
            // Apply が RenderSettings.sun を差し替える。解放後に Season Scene が消えると偽 null が残る。
            _sun = RenderSettings.sun;
            _captured = true;
        }

        public void Apply(in RenderEnvironmentState state, Light sun)
        {
            if (sun == null)
            {
                throw new ArgumentNullException(nameof(sun));
            }

            if (sun.type != LightType.Directional)
            {
                throw new InvalidOperationException("Bound sun must be LightType.Directional.");
            }

            sun.transform.rotation = Quaternion.Euler(state.SunEulerDegrees);
            sun.color = state.SunColor;
            sun.intensity = state.SunIntensity;

            RenderSettings.sun = sun;
            RenderSettings.fog = state.FogEnabled;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = state.FogColor;
            RenderSettings.fogDensity = state.FogDensity;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = state.AmbientSkyColor;
        }

        public void RestoreBaseline()
        {
            if (!_captured)
            {
                return;
            }

            RenderSettings.fog = _fog;
            RenderSettings.fogMode = _fogMode;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientLight = _ambientLight;
            RenderSettings.ambientSkyColor = _ambientSkyColor;
            RenderSettings.ambientEquatorColor = _ambientEquatorColor;
            RenderSettings.ambientGroundColor = _ambientGroundColor;
            RenderSettings.ambientIntensity = _ambientIntensity;
            // Flat のとき ambientLight が評価色。先に書いた sky を Flat 用の色で上書きしないよう、最後に戻す。
            if (_ambientMode == AmbientMode.Flat)
            {
                RenderSettings.ambientLight = _ambientLight;
            }

            RenderSettings.sun = _sun;
        }
    }
}
