#nullable enable

using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Environments
{
    /// <summary>
    /// グローバル見た目の純データ。Framework は季節語を持たない。
    /// FogMode / AmbientMode はここには置かず、Unity sink が四季共通値を書く。
    /// GlobalVolumeWeight は将来の共通 Volume 用。S-4c の Unity sink は読まない。
    /// </summary>
    public readonly struct RenderEnvironmentState
    {
        public readonly Vector3 SunEulerDegrees;
        public readonly Color SunColor;
        public readonly float SunIntensity;
        public readonly Color AmbientSkyColor;
        public readonly bool FogEnabled;
        public readonly Color FogColor;
        public readonly float FogDensity;
        public readonly float GlobalVolumeWeight;

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
