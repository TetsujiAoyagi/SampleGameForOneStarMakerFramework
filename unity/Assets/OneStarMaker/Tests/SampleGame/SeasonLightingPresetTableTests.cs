#nullable enable

using NUnit.Framework;
using SampleGame.InGame.World;
using UnityEngine;

namespace OneStarMaker.Tests.SampleGame
{
    [TestFixture]
    public sealed class SeasonLightingPresetTableTests
    {
        [Test]
        public void TryGet_Spring_MatchesFrozenValues()
        {
            Assert.That(SeasonLightingPresetTable.TryGet("Spring_Lighting", out var state), Is.True);
            AssertState(
                state,
                new Vector3(15f, -30f, 0f),
                new Color(1.00f, 0.85f, 0.70f),
                0.80f,
                new Color(0.18f, 0.22f, 0.28f),
                true,
                new Color(0.75f, 0.82f, 0.88f),
                0.0015f,
                1f);
        }

        [Test]
        public void TryGet_Summer_MatchesFrozenValues()
        {
            Assert.That(SeasonLightingPresetTable.TryGet("Summer_Lighting", out var state), Is.True);
            AssertState(
                state,
                new Vector3(50f, 40f, 0f),
                new Color(1.00f, 0.98f, 0.90f),
                1.30f,
                new Color(0.28f, 0.30f, 0.32f),
                true,
                new Color(0.70f, 0.78f, 0.85f),
                0.0040f,
                1f);
        }

        [Test]
        public void TryGet_Autumn_MatchesFrozenValues()
        {
            Assert.That(SeasonLightingPresetTable.TryGet("Autumn_Lighting", out var state), Is.True);
            AssertState(
                state,
                new Vector3(8f, 50f, 0f),
                new Color(1.00f, 0.55f, 0.25f),
                0.70f,
                new Color(0.18f, 0.13f, 0.08f),
                true,
                new Color(0.55f, 0.38f, 0.22f),
                0.0025f,
                1f);
        }

        [Test]
        public void TryGet_Winter_MatchesFrozenValues()
        {
            Assert.That(SeasonLightingPresetTable.TryGet("Winter_Lighting", out var state), Is.True);
            AssertState(
                state,
                new Vector3(70f, 0f, 0f),
                new Color(0.95f, 0.97f, 1.00f),
                0.50f,
                new Color(0.68f, 0.70f, 0.74f),
                true,
                new Color(0.90f, 0.92f, 0.95f),
                0.0020f,
                1f);
        }

        [Test]
        public void TryGet_Unknown_ReturnsFalse()
        {
            Assert.That(SeasonLightingPresetTable.TryGet("Spring_Lighting_4_2", out var state), Is.False);
            Assert.That(state, Is.EqualTo(default(OneStarMaker.Runtime.Rendering.Environments.RenderEnvironmentState)));
        }

        private static void AssertState(
            OneStarMaker.Runtime.Rendering.Environments.RenderEnvironmentState state,
            Vector3 sunEuler,
            Color sunColor,
            float intensity,
            Color ambient,
            bool fogEnabled,
            Color fogColor,
            float fogDensity,
            float volumeWeight)
        {
            Assert.That(state.SunEulerDegrees, Is.EqualTo(sunEuler));
            Assert.That(state.SunColor, Is.EqualTo(sunColor));
            Assert.That(state.SunIntensity, Is.EqualTo(intensity).Within(1e-5f));
            Assert.That(state.AmbientSkyColor, Is.EqualTo(ambient));
            Assert.That(state.FogEnabled, Is.EqualTo(fogEnabled));
            Assert.That(state.FogColor, Is.EqualTo(fogColor));
            Assert.That(state.FogDensity, Is.EqualTo(fogDensity).Within(1e-6f));
            Assert.That(state.GlobalVolumeWeight, Is.EqualTo(volumeWeight).Within(1e-5f));
        }
    }
}
