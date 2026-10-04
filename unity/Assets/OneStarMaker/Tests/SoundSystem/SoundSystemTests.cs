#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Runtime.SoundSystem;
using UnityEngine;

namespace OneStarMaker.Tests.SoundSystem
{
    /// <summary>
    /// 再生の転送が割り当てないことと、Unity ネイティブの登録と無効ハンドルを検証する。
    /// 鳴っているかの聴取はここではしない。
    /// </summary>
    [TestFixture]
    public sealed class SoundSystemTests
    {
        [Test]
        public void Handle_DefaultIsInvalid_AndRegisteredValuesAreDense()
        {
            Assert.That(SoundHandle.Invalid.IsValid, Is.False);
            Assert.That(SoundHandleIndex.TryGet(SoundHandle.Invalid, 1, out _), Is.False);

            var first = SoundHandle.FromRegisteredCount(1);
            var second = SoundHandle.FromRegisteredCount(2);
            Assert.That(SoundHandleIndex.TryGet(first, 2, out var firstIndex), Is.True);
            Assert.That(firstIndex, Is.EqualTo(0));
            Assert.That(SoundHandleIndex.TryGet(second, 2, out var secondIndex), Is.True);
            Assert.That(secondIndex, Is.EqualTo(1));
            Assert.That(SoundHandleIndex.TryGet(second, 1, out _), Is.False);
        }

        [Test]
        public void Mix_StealsTheLowerVoice_AndFadesTheRegionWithoutFreeingIt()
        {
            var mix = new SoundMix(2);
            var master = mix.Add(new SoundVolumeSettings(1f, 0, SoundReverb.Off));
            var high = mix.TryPlay(master, 1f, 5);
            var low = mix.TryPlay(master, 1f, 3);
            Assert.That(mix.TryPlay(master, 1f, 2).IsValid, Is.False);

            var mid = mix.TryPlay(master, 1f, 4);
            Assert.That(mid.IsValid, Is.True);
            mix.FadeVoice(low, 0f, 0f);
            Assert.That(mix.IsActive(low.Slot), Is.True);
            mix.FadeVoice(high, 0.5f, 0f);
            Assert.That(mix.VoiceGain(high.Slot), Is.EqualTo(0.5f));

            var reverb = new SoundReverb(0.4f, 1.5f, 0.8f, true);
            mix.SetReverb(master, reverb);
            Assert.That(mix.TryGetSettings(master, out var settings), Is.True);
            Assert.That(settings.Reverb, Is.EqualTo(reverb));

            mix.FadeVolume(master, 0f, 1f);
            mix.Tick(0.5f);
            Assert.That(mix.TryGetAudibleGain(master, out var audible), Is.True);
            Assert.That(audible, Is.EqualTo(0.5f));
            Assert.That(mix.IsActive(high.Slot), Is.True);
            mix.FadeVoice(mid, 0f, 0.5f);
            mix.Tick(0.5f);
            Assert.That(mix.IsActive(mid.Slot), Is.False);
        }

        [Test]
        public void Player_ForwardsHandleAndVolume_WithoutAllocating()
        {
            var backend = new RecordingBackend();
            var player = new SoundPlayer(backend);
            var handle = SoundHandle.FromRegisteredCount(1);

            for (var i = 0; i < 16; i++)
            {
                player.Play(handle, 0.25f);
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; i++)
            {
                player.Play(handle, 0.5f);
            }

            var after = GC.GetAllocatedBytesForCurrentThread();
            Assert.That(after, Is.EqualTo(before));
            Assert.That(backend.Calls, Is.EqualTo(2016));
            Assert.That(backend.Last, Is.EqualTo(handle));
            Assert.That(backend.Gain, Is.EqualTo(0.5f));

            player.Play(SoundHandle.Invalid, 0f);
            Assert.That(backend.Last, Is.EqualTo(SoundHandle.Invalid));
            Assert.That(backend.Gain, Is.EqualTo(0f));
        }

        [Test]
        public void UnityBackend_FadesARegion_AndKeepsReverbOnIt()
        {
            using var backend = new UnitySoundBackend(1);
            var region = backend.RegisterVolume(new SoundVolumeSettings(1f, 4, SoundReverb.Off));
            Assert.That(backend.DefaultVolume.IsValid, Is.True);
            Assert.That(region, Is.Not.EqualTo(backend.DefaultVolume));

            backend.FadeVolume(region, 0f, 1f);
            backend.Tick(0.5f);
            Assert.That(backend.TryGetAudibleGain(region, out var audible), Is.True);
            Assert.That(audible, Is.EqualTo(0.5f));

            var reverb = new SoundReverb(0.25f, 2f, 1f, true);
            backend.SetReverb(region, reverb);
            Assert.That(backend.TryGetSettings(region, out var settings), Is.True);
            Assert.That(settings.Priority, Is.EqualTo(4));
            Assert.That(settings.Reverb, Is.EqualTo(reverb));
            Assert.That(settings.Gain, Is.EqualTo(0f));
        }

        [Test]
        public void UnityBackend_RegistersInOrder_AndIgnoresInvalidOrDisposedPlay()
        {
            var clip = AudioClip.Create("sound-system-test", 32, 1, 8000, false);
            try
            {
                using var backend = new UnitySoundBackend(2);
                var first = backend.Register(clip);
                var second = backend.Register(clip);
                Assert.That(first.IsValid, Is.True);
                Assert.That(second.IsValid, Is.True);
                Assert.That(first, Is.Not.EqualTo(second));
                Assert.That(backend.RegisteredCount, Is.EqualTo(2));

                Assert.DoesNotThrow(() => backend.Play(SoundHandle.Invalid, 1f));
                Assert.DoesNotThrow(() => backend.Play(first, 0.5f));
                Assert.DoesNotThrow(() => backend.Play(second, 0.25f));
                backend.Dispose();
                Assert.DoesNotThrow(() => backend.Play(first, 1f));
            }
            finally
            {
                if (clip != null)
                {
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            }
        }

        [Test]
        public void UnityBackend_RejectsEmptyPool_NullClip_AndRegisterAfterDispose()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UnitySoundBackend(0));

            var backend = new UnitySoundBackend(1);
            AudioClip missing = null!;
            Assert.Throws<ArgumentNullException>(() => backend.Register(missing));
            backend.Dispose();
            var clip = AudioClip.Create("sound-system-disposed", 32, 1, 8000, false);
            try
            {
                Assert.Throws<ObjectDisposedException>(() => backend.Register(clip));
            }
            finally
            {
                if (clip != null)
                {
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            }
        }

        private sealed class RecordingBackend : ISoundBackend
        {
            public int Calls;
            public SoundHandle Last;
            public float Gain;

            public SoundVoiceId Play(SoundHandle handle, float gain)
            {
                Calls++;
                Last = handle;
                Gain = gain;
                return SoundVoiceId.Create(0, 1);
            }

            public SoundVoiceId Play(SoundHandle handle, SoundVolumeId volume, float gain, int priority)
            {
                return SoundVoiceId.Invalid;
            }

            public void FadeVolume(SoundVolumeId volume, float targetGain, float seconds)
            {
            }

            public void FadeVoice(SoundVoiceId voice, float targetGain, float seconds)
            {
            }

            public void SetReverb(SoundVolumeId volume, SoundReverb reverb)
            {
            }

            public void Tick(float deltaTime)
            {
            }
        }
    }
}
