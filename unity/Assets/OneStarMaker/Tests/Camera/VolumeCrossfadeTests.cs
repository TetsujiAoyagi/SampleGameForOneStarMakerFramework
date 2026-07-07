#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime.CameraSystem;
using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Tests.CameraSystem
{
    /// <summary>
    /// CAM-07: Volume weight クロスフェード policy の純 C# テスト。
    /// </summary>
    [TestFixture]
    public class VolumeCrossfadeTests
    {
        private VolumeCrossfade _crossfade = null!;
        private LogicalCamera _cameraA = null!;
        private LogicalCamera _cameraB = null!;
        private LogicalCamera _cameraC = null!;
        private UnityEngine.Object _profileA = null!;
        private UnityEngine.Object _profileB = null!;
        private UnityEngine.Object _profileC = null!;

        [SetUp]
        public void SetUp()
        {
            _crossfade = new VolumeCrossfade();
            _profileA = CreateStubProfile("profile-a");
            _profileB = CreateStubProfile("profile-b");
            _profileC = CreateStubProfile("profile-c");

            _cameraA = new LogicalCamera("camera-a") { VolumeProfile = _profileA };
            _cameraB = new LogicalCamera("camera-b") { VolumeProfile = _profileB };
            _cameraC = new LogicalCamera("camera-c") { VolumeProfile = _profileC };
        }

        [TearDown]
        public void TearDown()
        {
            DestroyProfile(_profileA);
            DestroyProfile(_profileB);
            DestroyProfile(_profileC);
        }

        [Test]
        public void Crossfade_MidBlend_WeightsAreComplementary()
        {
            var blend = new CameraBlendSpec
            {
                DurationSec = 2f,
                Easing = CameraBlendEasing.Linear,
            };

            _crossfade.BeginCrossfade(_cameraB, _cameraA, blend);
            _crossfade.Tick(1f);

            var incoming = _crossfade.TryGetWeight(_profileB);
            var departing = _crossfade.TryGetWeight(_profileA);

            Assert.That(incoming, Is.Not.Null);
            Assert.That(departing, Is.Not.Null);
            Assert.That(incoming!.Value, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(departing!.Value, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(incoming.Value + departing.Value, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Crossfade_Cut_ImmediatelyFullWeight()
        {
            _crossfade.BeginCrossfade(_cameraB, _cameraA, CameraBlendSpec.Cut);

            Assert.That(_crossfade.IsBlending, Is.False);
            Assert.That(_crossfade.TryGetWeight(_profileB), Is.EqualTo(1f).Within(1e-5f));
            // カットでも退場プロファイルは weight 0 の解放待ちとして観測可能にする。
            Assert.That(_crossfade.TryGetWeight(_profileA), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(_crossfade.IsPendingRelease(_profileA), Is.True);
        }

        [Test]
        public void Crossfade_Complete_DepartingProfileReleased()
        {
            var blend = new CameraBlendSpec
            {
                DurationSec = 1f,
                Easing = CameraBlendEasing.Linear,
            };

            _crossfade.BeginCrossfade(_cameraB, _cameraA, blend);
            _crossfade.Tick(1f);

            Assert.That(_crossfade.IsBlending, Is.False);
            Assert.That(_crossfade.TryGetWeight(_profileB), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(_crossfade.TryGetWeight(_profileA), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(_crossfade.IsPendingRelease(_profileA), Is.True);
        }

        [Test]
        public void Crossfade_InterruptedByNewBlend_StartsFromCurrentWeight()
        {
            var firstBlend = new CameraBlendSpec
            {
                DurationSec = 2f,
                Easing = CameraBlendEasing.Linear,
            };

            _crossfade.BeginCrossfade(_cameraB, _cameraA, firstBlend);
            _crossfade.Tick(0.5f);

            Assert.That(_crossfade.TryGetWeight(_profileB), Is.EqualTo(0.25f).Within(1e-5f));

            var secondBlend = new CameraBlendSpec
            {
                DurationSec = 1f,
                Easing = CameraBlendEasing.Linear,
            };

            // ブレンド中の再切替: 入場 weight 0.25 を起点に C へ遷移する
            _crossfade.BeginCrossfade(_cameraC, _cameraB, secondBlend);

            Assert.That(_crossfade.TryGetWeight(_profileC), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(_crossfade.TryGetWeight(_profileB), Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(_crossfade.IsBlending, Is.True);

            _crossfade.Tick(0.5f);

            // 0.25→1 を 0.5 秒で進めた時点の入場 weight
            Assert.That(_crossfade.TryGetWeight(_profileC), Is.EqualTo(0.625f).Within(1e-5f));
            Assert.That(_crossfade.TryGetWeight(_profileB), Is.EqualTo(0.375f).Within(1e-5f));
        }

        [Test]
        public void Crossfade_InterruptedBlend_ReleasesSupersededDepartingProfile()
        {
            var firstBlend = new CameraBlendSpec
            {
                DurationSec = 2f,
                Easing = CameraBlendEasing.Linear,
            };

            _crossfade.BeginCrossfade(_cameraB, _cameraA, firstBlend);
            _crossfade.Tick(0.5f);

            var secondBlend = new CameraBlendSpec
            {
                DurationSec = 1f,
                Easing = CameraBlendEasing.Linear,
            };

            // A は新しい入退場（C/B）に含まれないため、Host 側の残留を避ける解放待ちに移る。
            _crossfade.BeginCrossfade(_cameraC, _cameraB, secondBlend);

            Assert.That(_crossfade.IsPendingRelease(_profileA), Is.True);
            Assert.That(_crossfade.TryGetWeight(_profileA), Is.EqualTo(0f).Within(1e-5f));

            var entries = _crossfade.GetEntries();
            var hasReleasedA = false;
            foreach (var entry in entries)
            {
                if (ReferenceEquals(entry.Profile, _profileA)
                    && entry.IsPendingRelease
                    && Mathf.Approximately(entry.Weight, 0f))
                {
                    hasReleasedA = true;
                    break;
                }
            }

            Assert.That(hasReleasedA, Is.True);
        }

        [Test]
        public void Camera_WithoutProfile_NoCrossfadeEntry()
        {
            var cameraWithoutProfile = new LogicalCamera("no-profile");
            var blend = new CameraBlendSpec
            {
                DurationSec = 1f,
                Easing = CameraBlendEasing.Linear,
            };

            _crossfade.BeginCrossfade(cameraWithoutProfile, _cameraA, blend);
            _crossfade.Tick(0.5f);

            Assert.That(_crossfade.TryGetWeight(_profileA), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(_crossfade.GetEntries().Length, Is.EqualTo(1));

            _crossfade.BeginCrossfade(_cameraB, cameraWithoutProfile, blend);

            // プロファイルなし退場カメラは weight 対象外。入場 B は 0 から開始する。
            Assert.That(_crossfade.TryGetWeight(_profileB), Is.EqualTo(0f).Within(1e-5f));
            // 直前フェードで使っていた A は新しい入退場に含まれないため、解放待ちとして残す。
            Assert.That(_crossfade.TryGetWeight(_profileA), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(_crossfade.IsPendingRelease(_profileA), Is.True);

            var entries = _crossfade.GetEntries();
            Assert.That(entries.Length, Is.EqualTo(2));
            Assert.That(entries[0].Profile, Is.SameAs(_profileB));
            Assert.That(entries[1].Profile, Is.SameAs(_profileA));
            Assert.That(entries[1].IsPendingRelease, Is.True);
        }

        [Test]
        public void Crossfade_EaseInOut_MidpointIsHalf()
        {
            var blend = new CameraBlendSpec
            {
                DurationSec = 2f,
                Easing = CameraBlendEasing.EaseInOut,
            };

            _crossfade.BeginCrossfade(_cameraB, _cameraA, blend);
            _crossfade.Tick(1f);

            Assert.That(_crossfade.TryGetWeight(_profileB), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(_crossfade.TryGetWeight(_profileA), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void GetEntries_ReturnsStableSnapshot()
        {
            var blend = new CameraBlendSpec
            {
                DurationSec = 2f,
                Easing = CameraBlendEasing.Linear,
            };

            _crossfade.BeginCrossfade(_cameraB, _cameraA, blend);
            _crossfade.Tick(1f);

            var firstEntries = _crossfade.GetEntries();
            Assert.That(firstEntries.Length, Is.EqualTo(2));
            Assert.That(firstEntries[0].Weight, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(firstEntries[1].Weight, Is.EqualTo(0.5f).Within(1e-5f));

            _crossfade.Tick(1f);
            _ = _crossfade.GetEntries();

            // 1 回目の結果は配列コピーなので、後続 GetEntries で内容が変わらない。
            Assert.That(firstEntries.Length, Is.EqualTo(2));
            Assert.That(firstEntries[0].Weight, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(firstEntries[1].Weight, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(firstEntries[0].IsPendingRelease, Is.False);
            Assert.That(firstEntries[1].IsPendingRelease, Is.False);
        }

        private static UnityEngine.Object CreateStubProfile(string name)
        {
            var stub = ScriptableObject.CreateInstance<ScriptableObject>();
            stub.name = name;
            return stub;
        }

        private static void DestroyProfile(UnityEngine.Object profile)
        {
            if (profile != null)
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
