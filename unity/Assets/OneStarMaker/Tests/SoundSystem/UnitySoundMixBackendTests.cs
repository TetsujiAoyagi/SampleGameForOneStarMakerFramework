#nullable enable

using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Runtime.SoundSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace OneStarMaker.Tests.SoundSystem
{
    /// <summary>Native component configuration and cleanup proof; no listening or elapsed natural-end claim.</summary>
    [TestFixture]
    public sealed class UnitySoundMixBackendTests
    {
        [Test]
        public void RealGroup_UsesSourceProductGain_AndInstantFacadeFades()
        {
            using var fixture = new MixerFixture();
            using var backend = new UnitySoundBackend(1);
            var clip = Clip();
            try
            {
                var route = backend.RegisterVolume(fixture.Group, new SoundVolumeSettings(0.4f, 3, default));
                var voice = backend.Play(backend.Register(clip), route, 0.5f, 3);
                var source = Sources(backend)[voice.Slot];
                Assert.That(source.outputAudioMixerGroup, Is.SameAs(fixture.Group));
                Assert.That(source.volume, Is.EqualTo(0.2f).Within(0.00001f));
                backend.FadeVolume(route, 0.8f, 0f);
                Assert.That(source.volume, Is.EqualTo(0.4f).Within(0.00001f));
                backend.FadeVoice(voice, 0.25f, 0f);
                Assert.That(source.volume, Is.EqualTo(0.2f).Within(0.00001f));
                backend.FadeVoice(voice, 0f, 0f);
                AssertCleared(source);
                AssertFilter(source.GetComponent<AudioReverbFilter>(), SoundReverb.Off);
                Assert.That(Mix(backend).IsActive(voice.Slot), Is.False);
            }
            finally { backend.Dispose(); Destroy(clip); }
        }

        [Test]
        public void Voices_HaveSeparateChildrenAndFilters_AndEffectsAreIndependent()
        {
            using var backend = new UnitySoundBackend(2);
            var clip = Clip();
            try
            {
                var enabled = new SoundReverb(-700f, 2.5f, 65f, true);
                var route = backend.RegisterVolume(new SoundVolumeSettings(0.6f, 2, enabled));
                var handle = backend.Register(clip);
                var wetVoice = backend.Play(handle, route, 0.5f, 2);
                var dryVoice = backend.Play(handle, 0.8f);
                var sources = Sources(backend);
                var host = Field<GameObject>(backend, "_host");
                Assert.That(host.GetComponents<AudioSource>(), Is.Empty);
                Assert.That(host.transform.childCount, Is.EqualTo(2));
                Assert.That(sources[0].gameObject, Is.Not.SameAs(sources[1].gameObject));
                foreach (var source in sources)
                {
                    Assert.That(source.transform.parent, Is.SameAs(host.transform));
                    Assert.That(source.GetComponents<AudioSource>().Length, Is.EqualTo(1));
                    Assert.That(source.GetComponents<AudioReverbFilter>().Length, Is.EqualTo(1));
                    Assert.That(source.loop, Is.False);
                    Assert.That(source.spatialBlend, Is.Zero);
                }
                Assert.That(sources[wetVoice.Slot].volume, Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(sources[dryVoice.Slot].volume, Is.EqualTo(0.8f));
                Assert.That(sources[dryVoice.Slot].outputAudioMixerGroup == null, Is.True);
                AssertFilter(sources[wetVoice.Slot].GetComponent<AudioReverbFilter>(), enabled);
                AssertFilter(sources[dryVoice.Slot].GetComponent<AudioReverbFilter>(), default);
                backend.SetReverb(route, SoundReverb.Off);
                AssertFilter(sources[wetVoice.Slot].GetComponent<AudioReverbFilter>(), default);
                backend.SetReverb(route, enabled);
                AssertFilter(sources[wetVoice.Slot].GetComponent<AudioReverbFilter>(), enabled);
                AssertFilter(sources[dryVoice.Slot].GetComponent<AudioReverbFilter>(), default);
            }
            finally { backend.Dispose(); Destroy(clip); }
        }

        [Test]
        public void Eviction_RebindsClipAndGroup_AndResetsReusedFilter()
        {
            using var fixture = new MixerFixture();
            using var backend = new UnitySoundBackend(1);
            var first = Clip();
            var second = Clip();
            try
            {
                var route = backend.RegisterVolume(fixture.Group,
                    new SoundVolumeSettings(1f, 0, new SoundReverb(200f, 3f, 40f, true)));
                var old = backend.Play(backend.Register(first), route, 1f, 0);
                var replacement = backend.Play(backend.Register(second), 0.7f);
                Assert.That(replacement.IsValid && replacement != old, Is.True);
                var source = Sources(backend)[replacement.Slot];
                Assert.That(source.clip, Is.SameAs(second));
                Assert.That(source.outputAudioMixerGroup == null, Is.True);
                AssertFilter(source.GetComponent<AudioReverbFilter>(), default);
                backend.FadeVoice(old, 0f, 0f);
                Assert.That(source.clip, Is.SameAs(second));
                backend.FadeVoice(replacement, 0f, 1f);
                backend.Tick(1f);
                AssertCleared(source);
                Assert.That(Mix(backend).IsActive(replacement.Slot), Is.False);
            }
            finally { backend.Dispose(); Destroy(first); Destroy(second); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DestroyedGroup_FailsClosed_OnNextDirectedPlayOrValidTick(bool useTick)
        {
            using var fixture = new MixerFixture();
            using var backend = new UnitySoundBackend(2);
            var clip = Clip();
            try
            {
                var group = fixture.Group;
                var route = backend.RegisterVolume(group, new SoundVolumeSettings(1f, 5, default));
                var handle = backend.Register(clip);
                var voice = backend.Play(handle, route, 1f, 5);
                var source = Sources(backend)[voice.Slot];
                fixture.Dispose(); // Deletes only the asset created by this fixture, destroying its group.
                Assert.That(group == null, Is.True);
                Assert.Throws<ArgumentNullException>(() => backend.RegisterVolume(group, default));
                if (useTick)
                    backend.Tick(0f);
                else
                    Assert.That(backend.Play(handle, route, 1f, 10).IsValid, Is.False);
                AssertCleared(source);
                Assert.That(Mix(backend).IsActive(voice.Slot), Is.False);
                Assert.That(backend.Play(handle, route, 1f, 10).IsValid, Is.False);
                Assert.That(backend.Play(handle, 1f).IsValid, Is.True);
            }
            finally { backend.Dispose(); Destroy(clip); }
        }

        [Test]
        public void StoppedFixture_IsNotCollectedByPlay_ButIsClearedAtNextValidTick()
        {
            using var backend = new UnitySoundBackend(1);
            var clip = Clip();
            try
            {
                var route = backend.RegisterVolume(new SoundVolumeSettings(1f, 5,
                    new SoundReverb(-100f, 2f, 75f, true)));
                var handle = backend.Register(clip);
                var voice = backend.Play(handle, route, 1f, 5);
                var source = Sources(backend)[voice.Slot];
                // Controlled fixture only: external Stop/Pause is not a supported production API.
                source.Stop();
                Assert.That(source.isPlaying, Is.False);
                Assert.That(backend.Play(handle, route, 1f, 4).IsValid, Is.False);
                Assert.That(source.clip, Is.SameAs(clip));
                foreach (var delta in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    backend.Tick(delta);
                    Assert.That(source.clip, Is.SameAs(clip));
                    Assert.That(Mix(backend).IsActive(voice.Slot), Is.True);
                }
                backend.Tick(0f);
                Assert.That(Mix(backend).IsActive(voice.Slot), Is.False);
                AssertCleared(source);
                AssertFilter(source.GetComponent<AudioReverbFilter>(), default);
                Assert.That(backend.Play(handle, route, 1f, 4).IsValid, Is.True);
            }
            finally { backend.Dispose(); Destroy(clip); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingNativeComponent_IsGuardedAndClearedOnTick(bool destroyFilter)
        {
            using var backend = new UnitySoundBackend(1);
            var clip = Clip();
            try
            {
                var handle = backend.Register(clip);
                var voice = backend.Play(handle, 0.5f);
                var source = Sources(backend)[voice.Slot];
                var filter = source.GetComponent<AudioReverbFilter>();
                var host = Field<GameObject>(backend, "_host");
                if (destroyFilter)
                {
                    UnityEngine.Object.DestroyImmediate(filter);
                    Assert.That(filter == null, Is.True);
                    Assert.That(source != null, Is.True);
                }
                else
                {
                    // AudioReverbFilter requires AudioSource; destroy the owned voice child to remove both.
                    UnityEngine.Object.DestroyImmediate(source.gameObject);
                    Assert.That(source == null, Is.True);
                    Assert.That(filter == null, Is.True);
                }
                Assert.That(host != null, Is.True);
                Assert.That(backend.Play(handle, 1f).IsValid, Is.False);
                Assert.That(Mix(backend).TrySlot(voice, out _), Is.True);
                Assert.DoesNotThrow(() => backend.Tick(0f));
                Assert.That(Mix(backend).IsActive(voice.Slot), Is.False);
                if (source != null) AssertCleared(source);
                if (filter != null) AssertFilter(filter, default);
            }
            finally { backend.Dispose(); Destroy(clip); }
        }

        [Test]
        public void DefaultAndUngroupedRoutes_AreDistinct_AndNullGroupCannotRegister()
        {
            using var backend = new UnitySoundBackend(1);
            Assert.That(backend.TryGetSettings(backend.DefaultVolume, out var settings), Is.True);
            Assert.That(settings, Is.EqualTo(new SoundVolumeSettings(1f, 0, default)));
            Assert.That(backend.RegisterVolume(default), Is.Not.EqualTo(backend.DefaultVolume));
            Assert.Throws<ArgumentNullException>(() => backend.RegisterVolume(null!, default));
        }

        private static void AssertFilter(AudioReverbFilter filter, SoundReverb value)
        {
            Assert.That(filter.enabled, Is.EqualTo(value.Enabled));
            Assert.That(filter.reverbPreset, Is.EqualTo(AudioReverbPreset.User));
            Assert.That(filter.reverbLevel, Is.EqualTo(value.Enabled ? value.ReverbLevelMillibels : -10000f).Within(0.001f));
            Assert.That(filter.decayTime, Is.EqualTo(value.Enabled ? value.DecaySeconds : 1f).Within(0.001f));
            Assert.That(filter.diffusion, Is.EqualTo(value.Enabled ? value.DiffusionPercent : 100f).Within(0.001f));
            Assert.That(filter.dryLevel, Is.Zero);
            Assert.That(filter.room, Is.Zero);
            Assert.That(filter.roomHF, Is.Zero);
            Assert.That(filter.roomLF, Is.Zero);
            Assert.That(filter.decayHFRatio, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(filter.reflectionsLevel, Is.EqualTo(-10000f));
            Assert.That(filter.reflectionsDelay, Is.Zero);
            Assert.That(filter.reverbDelay, Is.EqualTo(0.04f).Within(0.001f));
            Assert.That(filter.hfReference, Is.EqualTo(5000f));
            Assert.That(filter.lfReference, Is.EqualTo(250f));
            Assert.That(filter.density, Is.EqualTo(100f));
        }

        private static void AssertCleared(AudioSource source)
        {
            Assert.That(source.isPlaying, Is.False);
            Assert.That(source.clip == null, Is.True);
            Assert.That(source.outputAudioMixerGroup == null, Is.True);
        }
        private static AudioClip Clip() => AudioClip.Create("sound-mix-native", 32, 1, 8000, false);
        private static void Destroy(AudioClip clip)
        {
            if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
        }
        private static AudioSource[] Sources(UnitySoundBackend backend) => Field<AudioSource[]>(backend, "_voices");
        private static SoundMix Mix(UnitySoundBackend backend) => Field<SoundMix>(backend, "_mix");
        private static T Field<T>(UnitySoundBackend backend, string name) => (T)typeof(UnitySoundBackend)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(backend)!;

        // Editor-only bounded asset creation; the first invocation/feasibility evidence belongs to Phase C.
        private sealed class MixerFixture : IDisposable
        {
            private readonly string _path = "Assets/OneStarMaker/Tests/SoundSystem/GeneratedMixer-" + Guid.NewGuid().ToString("N") + ".mixer";
            private bool _disposed;
            public AudioMixerGroup Group { get; }

            public MixerFixture()
            {
                if (File.Exists(_path) || AssetDatabase.LoadMainAssetAtPath(_path) != null)
                    throw new InvalidOperationException("Refusing to overwrite fixture asset: " + _path);
                try
                {
                    Type? controller = null;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        controller = assembly.GetType("UnityEditor.Audio.AudioMixerController");
                        if (controller != null) break;
                    }
                    var create = controller?.GetMethod("CreateMixerControllerAtPath", BindingFlags.Static |
                        BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
                    if (create == null) throw new InvalidOperationException("Unity mixer fixture API unavailable.");
                    create.Invoke(null, new object[] { _path });
                    AssetDatabase.ImportAsset(_path);
                    var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(_path);
                    if (mixer == null) throw new InvalidOperationException("Native mixer fixture was not created.");
                    var groups = mixer.FindMatchingGroups("");
                    if (groups.Length == 0 || groups[0] == null)
                        throw new InvalidOperationException("Native mixer fixture has no group.");
                    Group = groups[0];
                }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                // DeleteAsset deletes this unique asset and meta; never delete a preexisting asset or folder.
                if (!AssetDatabase.DeleteAsset(_path) && (File.Exists(_path) || File.Exists(_path + ".meta")))
                    throw new InvalidOperationException("Native mixer fixture cleanup failed: " + _path);
            }
        }
    }
}
