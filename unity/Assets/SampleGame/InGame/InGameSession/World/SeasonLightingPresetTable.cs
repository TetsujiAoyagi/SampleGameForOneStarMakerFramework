#nullable enable

using OneStarMaker.Runtime.Rendering.Environments;
using UnityEngine;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// 季節 Lighting の identity → 見た目 state。
    /// 数値は HANDOFF 凍結値。Framework 側へ季節語を出さないための Game 層の表。
    /// </summary>
    internal static class SeasonLightingPresetTable
    {
        public static bool TryGet(string identity, out RenderEnvironmentState state)
        {
            switch (identity)
            {
                case "Spring_Lighting":
                    state = new RenderEnvironmentState(
                        sunEulerDegrees: new Vector3(15f, -30f, 0f),
                        sunColor: new Color(1.00f, 0.85f, 0.70f),
                        sunIntensity: 0.80f,
                        ambientSkyColor: new Color(0.18f, 0.22f, 0.28f),
                        fogEnabled: true,
                        fogColor: new Color(0.75f, 0.82f, 0.88f),
                        fogDensity: 0.0015f,
                        globalVolumeWeight: 1f);
                    return true;
                case "Summer_Lighting":
                    state = new RenderEnvironmentState(
                        sunEulerDegrees: new Vector3(50f, 40f, 0f),
                        sunColor: new Color(1.00f, 0.98f, 0.90f),
                        sunIntensity: 1.30f,
                        ambientSkyColor: new Color(0.28f, 0.30f, 0.32f),
                        fogEnabled: true,
                        fogColor: new Color(0.70f, 0.78f, 0.85f),
                        fogDensity: 0.0040f,
                        globalVolumeWeight: 1f);
                    return true;
                case "Autumn_Lighting":
                    state = new RenderEnvironmentState(
                        sunEulerDegrees: new Vector3(8f, 50f, 0f),
                        sunColor: new Color(1.00f, 0.55f, 0.25f),
                        sunIntensity: 0.70f,
                        ambientSkyColor: new Color(0.18f, 0.13f, 0.08f),
                        fogEnabled: true,
                        fogColor: new Color(0.55f, 0.38f, 0.22f),
                        fogDensity: 0.0025f,
                        globalVolumeWeight: 1f);
                    return true;
                case "Winter_Lighting":
                    state = new RenderEnvironmentState(
                        sunEulerDegrees: new Vector3(70f, 0f, 0f),
                        sunColor: new Color(0.95f, 0.97f, 1.00f),
                        sunIntensity: 0.50f,
                        ambientSkyColor: new Color(0.68f, 0.70f, 0.74f),
                        fogEnabled: true,
                        fogColor: new Color(0.90f, 0.92f, 0.95f),
                        fogDensity: 0.0020f,
                        globalVolumeWeight: 1f);
                    return true;
                default:
                    state = default;
                    return false;
            }
        }
    }
}
