#nullable enable

using System;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Runtime.Rendering.Environments;
using UnityEngine;

namespace OneStarMaker.Tests.Rendering.Environments
{
    /// <summary>
    /// 単一 owner・stale 権利証・Environment 回収が Fake sink 上で閉じることを見る。
    /// Unity Scene は開かない。Bind が要るケースだけ一時 Light を作る。
    /// </summary>
    [TestFixture]
    public sealed class RenderEnvironmentLeaseTests
    {
        private FakeRenderEnvironmentSink _sink = null!;
        private RenderEnvironment _environment = null!;
        private GameObject? _sunGo;

        [SetUp]
        public void SetUp()
        {
            // テストごとに新しい組。Dispose 済み Environment は Acquire できず、
            // sink の回数も前のテストから漏れない。
            _sink = new FakeRenderEnvironmentSink();
            _environment = new RenderEnvironment(_sink);
        }

        [TearDown]
        public void TearDown()
        {
            // DisposeEnvironment テストは本体を先に Dispose する。二重 Dispose は no-op である必要がある。
            _environment.Dispose();
            if (_sunGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_sunGo);
                _sunGo = null;
            }
        }

        [Test]
        public void Acquire_First_Succeeds_AndHasOwner()
        {
            var lease = _environment.Acquire("a");

            Assert.That(lease, Is.Not.Null);
            Assert.That(_environment.HasActiveOwner, Is.True);
            Assert.That(_sink.CaptureCount, Is.EqualTo(1));
            Assert.That(_sink.RestoreCount, Is.EqualTo(0));
        }

        [Test]
        public void State_ExposesValuesAsGetOnlyProperties()
        {
            var stateType = typeof(RenderEnvironmentState);
            var propertyNames = new[]
            {
                nameof(RenderEnvironmentState.SunEulerDegrees),
                nameof(RenderEnvironmentState.SunColor),
                nameof(RenderEnvironmentState.SunIntensity),
                nameof(RenderEnvironmentState.AmbientSkyColor),
                nameof(RenderEnvironmentState.FogEnabled),
                nameof(RenderEnvironmentState.FogColor),
                nameof(RenderEnvironmentState.FogDensity),
                nameof(RenderEnvironmentState.GlobalVolumeWeight)
            };

            foreach (var propertyName in propertyNames)
            {
                var property = stateType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

                Assert.That(property, Is.Not.Null, propertyName);
                Assert.That(property!.SetMethod, Is.Null, propertyName);
                Assert.That(
                    stateType.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public),
                    Is.Null,
                    propertyName);
            }
        }

        [Test]
        public void Acquire_Second_Throws_AndKeepsFirstOwner()
        {
            var first = _environment.Acquire("a");

            Assert.Throws<InvalidOperationException>(() => _environment.Acquire("b"));
            Assert.That(_environment.HasActiveOwner, Is.True);
            Assert.That(_sink.CaptureCount, Is.EqualTo(1));
            Assert.That(first.Generation, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_Matching_ClearsOwner_AndRestoresBaseline()
        {
            var lease = _environment.Acquire("a");
            lease.Dispose();

            Assert.That(_environment.HasActiveOwner, Is.False);
            Assert.That(_sink.RestoreCount, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_StaleAfterNewOwner_DoesNotClearNewOwner_AndDoesNotRestore()
        {
            var leaseA = _environment.Acquire("a");
            leaseA.Dispose();
            var leaseB = _environment.Acquire("b");
            leaseA.Dispose();

            Assert.That(_environment.HasActiveOwner, Is.True);
            Assert.That(_sink.RestoreCount, Is.EqualTo(1));
            Assert.That(leaseB.Generation, Is.EqualTo(1));
        }

        [Test]
        public void Apply_OnDisposedLease_Throws_AndDoesNotMutateSink()
        {
            var sun = CreateSun();
            var lease = _environment.Acquire("a");
            lease.BindSun(sun);
            lease.Apply(ValidState());
            lease.Dispose();

            Assert.That(_sink.Applies.Count, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => lease.Apply(ValidState()));
            Assert.That(_sink.Applies.Count, Is.EqualTo(1));
        }

        [Test]
        public void Apply_StaleLeaseAfterNewOwnerApply_Throws_AndDoesNotMutateNewOwner()
        {
            var sun = CreateSun();
            var leaseA = _environment.Acquire("a");
            leaseA.BindSun(sun);
            leaseA.Apply(ValidState());
            leaseA.Dispose();

            var leaseB = _environment.Acquire("b");
            leaseB.BindSun(sun);
            var stateB = new RenderEnvironmentState(
                new Vector3(50f, 40f, 0f),
                new Color(1f, 0.98f, 0.90f),
                1.30f,
                new Color(0.28f, 0.30f, 0.32f),
                true,
                new Color(0.70f, 0.78f, 0.85f),
                0.0040f,
                1f);
            leaseB.Apply(stateB);

            Assert.Throws<InvalidOperationException>(() => leaseA.Apply(ValidState()));
            Assert.That(_sink.Applies.Count, Is.EqualTo(2));
            Assert.That(_sink.Applies[1].SunIntensity, Is.EqualTo(1.30f));
        }

        [Test]
        public void Dispose_Twice_IsIdempotent()
        {
            var lease = _environment.Acquire("a");
            lease.Dispose();

            Assert.DoesNotThrow(() => lease.Dispose());
            Assert.That(_sink.RestoreCount, Is.EqualTo(1));
        }

        [Test]
        public void Acquire_NullOwner_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _environment.Acquire(null!));
            Assert.That(_environment.HasActiveOwner, Is.False);
            Assert.That(_sink.CaptureCount, Is.EqualTo(0));
        }

        [Test]
        public void Apply_BeforeBindSun_Throws()
        {
            var lease = _environment.Acquire("a");

            Assert.Throws<InvalidOperationException>(() => lease.Apply(ValidState()));
            Assert.That(_sink.Applies.Count, Is.EqualTo(0));
        }

        [Test]
        public void BindSun_Twice_Throws()
        {
            var sun = CreateSun();
            var lease = _environment.Acquire("a");
            lease.BindSun(sun);

            Assert.Throws<InvalidOperationException>(() => lease.BindSun(sun));
        }

        [Test]
        public void BindSun_AfterBoundLightDestroyed_Throws()
        {
            var sun = CreateSun();
            var lease = _environment.Acquire("a");
            lease.BindSun(sun);
            UnityEngine.Object.DestroyImmediate(_sunGo);
            _sunGo = null;

            var replacementGo = new GameObject("replacement-sun");
            try
            {
                var replacement = replacementGo.AddComponent<Light>();
                replacement.type = LightType.Directional;
                Assert.Throws<InvalidOperationException>(() => lease.BindSun(replacement));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(replacementGo);
            }
        }

        [Test]
        public void BindSun_NullLight_Throws()
        {
            var lease = _environment.Acquire("a");

            Assert.Throws<ArgumentNullException>(() => lease.BindSun(null!));
        }

        [Test]
        public void Validate_NegativeIntensity_Throws()
        {
            var bad = new RenderEnvironmentState(
                Vector3.zero,
                Color.white,
                -1f,
                Color.white,
                false,
                Color.white,
                0f,
                1f);

            Assert.Throws<ArgumentOutOfRangeException>(() => RenderEnvironmentValidator.Validate(bad));
        }

        [Test]
        public void DisposeEnvironment_WithActiveLease_RestoresOnce_AndLeaseDisposeIsNoOp()
        {
            var sun = CreateSun();
            var lease = _environment.Acquire("a");
            lease.BindSun(sun);
            lease.Apply(ValidState());
            _environment.Dispose();

            Assert.That(_sink.RestoreCount, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => lease.Apply(ValidState()));
            lease.Dispose();
            Assert.That(_sink.RestoreCount, Is.EqualTo(1));
        }

        private Light CreateSun()
        {
            _sunGo = new GameObject("test-sun");
            var light = _sunGo.AddComponent<Light>();
            light.type = LightType.Directional;
            return light;
        }

        private static RenderEnvironmentState ValidState() => new(
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
