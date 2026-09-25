#nullable enable

using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Environments
{
    /// <summary>
    /// グローバル見た目の純データ
    /// FogMode / AmbientMode はここには置かず、Unity sink が四季共通値を書く。
    /// GlobalVolumeWeight は将来の共通 Volume 用。S-4c の Unity sink は読まない。
    /// </summary>
    public readonly struct RenderEnvironmentState
    {
        public Vector3 SunEulerDegrees { get; }
        public Color SunColor { get; }
        public float SunIntensity { get; }
        public Color AmbientSkyColor { get; }
        public bool FogEnabled { get; }
        public Color FogColor { get; }
        public float FogDensity { get; }
        public float GlobalVolumeWeight { get; }

        public RenderEnvironmentState(
            Vector3 sunEulerDegrees,
            Color sunColor,
            float sunIntensity,
            Color ambientSkyColor,
            bool fogEnabled,
            Color fogColor,
            float fogDensity,
            float globalVolumeWeight)
        {
            SunEulerDegrees = sunEulerDegrees;
            SunColor = sunColor;
            SunIntensity = sunIntensity;
            AmbientSkyColor = ambientSkyColor;
            FogEnabled = fogEnabled;
            FogColor = fogColor;
            FogDensity = fogDensity;
            GlobalVolumeWeight = globalVolumeWeight;
        }
    }
}
