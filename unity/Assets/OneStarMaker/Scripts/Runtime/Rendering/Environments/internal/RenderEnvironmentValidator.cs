#nullable enable

using System;
using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Environments
{
    /// <summary>
    /// 負の intensity などを装置へ流さない fail-closed。
    /// Environment.Apply の手前だけが呼ぶ。Fake sink は検証しない（記録専用）。
    /// </summary>
    internal static class RenderEnvironmentValidator
    {
        public static void Validate(in RenderEnvironmentState state)
        {
            if (state.SunIntensity < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(state), "SunIntensity must be >= 0.");
            }

            if (state.FogDensity < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(state), "FogDensity must be >= 0.");
            }

            if (state.GlobalVolumeWeight < 0f || state.GlobalVolumeWeight > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(state), "GlobalVolumeWeight must be in 0..1.");
            }

            RejectNegativeColor(state.SunColor, nameof(state.SunColor));
            RejectNegativeColor(state.AmbientSkyColor, nameof(state.AmbientSkyColor));
            RejectNegativeColor(state.FogColor, nameof(state.FogColor));
        }

        private static void RejectNegativeColor(Color color, string name)
        {
            if (color.r < 0f || color.g < 0f || color.b < 0f || color.a < 0f)
            {
                throw new ArgumentOutOfRangeException(name, "Color channels must not be negative.");
            }
        }
    }
}
