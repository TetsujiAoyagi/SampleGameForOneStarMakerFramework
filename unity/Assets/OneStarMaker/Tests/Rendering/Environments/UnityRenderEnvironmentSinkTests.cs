#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.Rendering.Environments;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneStarMaker.Tests.Rendering.Environments
{
    /// <summary>
    /// Unity sink が Apply で書き換えた ambient 成分と sun を、capture 時点へ戻すことを見る。
    /// Editor の RenderSettings は SetUp で退避し、TearDown で戻す。
    /// </summary>
    [TestFixture]
    public sealed class UnityRenderEnvironmentSinkTests
    {
        private AmbientMode _savedMode;
        private Color _savedAmbientLight;
        private Color _savedSky;
        private Color _savedEquator;
        private Color _savedGround;
        private float _savedIntensity;
        private bool _savedFog;
        private FogMode _savedFogMode;
        private Color _savedFogColor;
        private float _savedFogDensity;
        private Light _savedSun = null!;
        private GameObject? _baselineGo;
        private GameObject? _seasonGo;
        private readonly List<Light> _disabledDirectionals = new();

        [SetUp]
        public void SetUp()
        {
            _savedMode = RenderSettings.ambientMode;
            _savedAmbientLight = RenderSettings.ambientLight;
            _savedSky = RenderSettings.ambientSkyColor;
            _savedEquator = RenderSettings.ambientEquatorColor;
            _savedGround = RenderSettings.ambientGroundColor;
            _savedIntensity = RenderSettings.ambientIntensity;
            _savedFog = RenderSettings.fog;
            _savedFogMode = RenderSettings.fogMode;
            _savedFogColor = RenderSettings.fogColor;
            _savedFogDensity = RenderSettings.fogDensity;
            _savedSun = RenderSettings.sun;
        }

        [TearDown]
        public void TearDown()
        {
            RenderSettings.ambientMode = _savedMode;
            RenderSettings.ambientLight = _savedAmbientLight;
            RenderSettings.ambientSkyColor = _savedSky;
            RenderSettings.ambientEquatorColor = _savedEquator;
            RenderSettings.ambientGroundColor = _savedGround;
            RenderSettings.ambientIntensity = _savedIntensity;
            RenderSettings.fog = _savedFog;
            RenderSettings.fogMode = _savedFogMode;
            RenderSettings.fogColor = _savedFogColor;
            RenderSettings.fogDensity = _savedFogDensity;
            RenderSettings.sun = _savedSun;

            if (_baselineGo != null)
            {
                Object.DestroyImmediate(_baselineGo);
                _baselineGo = null;
            }

            if (_seasonGo != null)
            {
                Object.DestroyImmediate(_seasonGo);
                _seasonGo = null;
            }

            foreach (var light in _disabledDirectionals)
            {
                if (light != null)
                {
                    light.enabled = true;
                }
            }

            _disabledDirectionals.Clear();
        }

        [Test]
        public void RestoreBaseline_TrilightAndSun_ReturnsCapturedComponents()
        {
            var baselineSun = CreateLight("baseline-sun", ref _baselineGo);
            var seasonSun = CreateLight("season-sun", ref _seasonGo);
            var sky = new Color(0.11f, 0.22f, 0.33f);
            var equator = new Color(0.44f, 0.55f, 0.66f);
            var ground = new Color(0.70f, 0.15f, 0.05f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.ambientIntensity = 0.35f;
            RenderSettings.fog = false;
            RenderSettings.sun = baselineSun;

            var sink = new UnityRenderEnvironmentSink();
            sink.CaptureBaseline();
            sink.Apply(FlatState(), seasonSun);

            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
            Assert.That(RenderSettings.sun, Is.EqualTo(seasonSun));

            sink.RestoreBaseline();

            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(sky));
            Assert.That(RenderSettings.ambientEquatorColor, Is.EqualTo(equator));
            Assert.That(RenderSettings.ambientGroundColor, Is.EqualTo(ground));
            Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(0.35f));
            Assert.That(RenderSettings.fog, Is.False);
            Assert.That(RenderSettings.sun, Is.EqualTo(baselineSun));
        }

        [Test]
        public void RestoreBaseline_NullSun_DoesNotLeaveDestroyedSun()
        {
            // sun の getter は未設定だと一番明るい Directional を返す。
            // 既存の Directional を止めてからでないと、capture が null を観測できない。
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional || !light.enabled)
                {
                    continue;
                }

                light.enabled = false;
                _disabledDirectionals.Add(light);
            }

            RenderSettings.sun = null;
            Assert.That(RenderSettings.sun == null, Is.True);

            var seasonSun = CreateLight("season-sun", ref _seasonGo);
            var sink = new UnityRenderEnvironmentSink();
            sink.CaptureBaseline();
            sink.Apply(FlatState(), seasonSun);
            Assert.That(RenderSettings.sun, Is.EqualTo(seasonSun));

            sink.RestoreBaseline();
            Object.DestroyImmediate(_seasonGo);
            _seasonGo = null;

            // 破棄済み Light への偽 null が残ると、== null は真でも C# 参照は残る。
            var restored = RenderSettings.sun;
            Assert.That(restored == null, Is.True);
            Assert.That((object)restored == null, Is.True);
        }

        private static Light CreateLight(string name, ref GameObject? holder)
        {
            holder = new GameObject(name);
            var light = holder.AddComponent<Light>();
            light.type = LightType.Directional;
            return light;
        }

        private static RenderEnvironmentState FlatState() => new(
            sunEulerDegrees: new Vector3(15f, -30f, 0f),
            sunColor: new Color(1f, 0.85f, 0.70f),
            sunIntensity: 0.80f,
            ambientSkyColor: new Color(0.18f, 0.22f, 0.28f),
            fogEnabled: true,
            fogColor: new Color(0.75f, 0.82f, 0.88f),
            fogDensity: 0.0015f,
            globalVolumeWeight: 1f);
    }
}
