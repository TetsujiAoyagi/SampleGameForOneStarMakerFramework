#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Runtime.SoundSystem;

namespace OneStarMaker.Tests.SoundSystem
{
    [TestFixture]
    public sealed class SoundMixTests
    {
        [Test]
        public void PriorityTieAndDenial_PreserveGenerationAndChooseOldest()
        {
            var mix = new SoundMix(2);
            var route = mix.Add(new SoundVolumeSettings(1f, 0, default));
            var first = mix.TryPlay(route, 1f, 2);
            var second = mix.TryPlay(route, 1f, 2);
            Assert.That(mix.TryPlay(route, 1f, 1).IsValid, Is.False);
            var replacement = mix.TryPlay(route, 1f, 2);
            Assert.That(replacement.Slot, Is.EqualTo(first.Slot));
            Assert.That(replacement, Is.Not.EqualTo(first));
            mix.FadeVoice(first, 0f, 0f);
            Assert.That(mix.IsActive(replacement.Slot), Is.True);
            mix.FadeVoice(second, 0f, 0f);
            Assert.That(mix.IsActive(second.Slot), Is.False);
            Assert.That(mix.TryPlay(route, 1f, 0), Is.Not.EqualTo(second));
        }

        [Test]
        public void SteppedAndInstantFades_ClampAndReleaseOnlyVoice()
        {
            var mix = new SoundMix(1);
            var route = mix.Add(new SoundVolumeSettings(1f, 0, default));
            var voice = mix.TryPlay(route, 2f, 0);
            Assert.That(mix.VoiceGain(voice.Slot), Is.EqualTo(1f));
            mix.FadeVolume(route, -1f, 0f);
            mix.TryGetAudibleGain(route, out var gain);
            Assert.That(gain, Is.Zero);
            Assert.That(mix.IsActive(voice.Slot), Is.True);
            mix.FadeVolume(route, 2f, 1f);
            mix.FadeVoice(voice, 0f, 1f);
            mix.Tick(0f);
            Assert.That(mix.VoiceGain(voice.Slot), Is.EqualTo(1f));
            mix.Tick(0.5f);
            mix.TryGetAudibleGain(route, out gain);
            Assert.That(gain, Is.EqualTo(0.5f));
            Assert.That(mix.VoiceGain(voice.Slot), Is.EqualTo(0.5f));
            mix.Tick(2f);
            Assert.That(mix.IsActive(voice.Slot), Is.False);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonfiniteInput_IsRejectedWithoutMutation(float bad)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoundVolumeSettings(bad, 0, default));
            foreach (var enabled in new[] { false, true })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new SoundReverb(bad, 1f, 100f, enabled));
                Assert.Throws<ArgumentOutOfRangeException>(() => new SoundReverb(0f, bad, 100f, enabled));
                Assert.Throws<ArgumentOutOfRangeException>(() => new SoundReverb(0f, 1f, bad, enabled));
            }
            var mix = new SoundMix(1);
            var route = mix.Add(new SoundVolumeSettings(1f, 0, default));
            var voice = mix.TryPlay(route, 0.8f, 0);
            mix.FadeVolume(route, 0f, 1f);
            mix.FadeVoice(voice, 0f, 1f);
            mix.FadeVolume(route, bad, 0f);
            mix.FadeVolume(route, 1f, bad);
            mix.FadeVoice(voice, bad, 0f);
            mix.FadeVoice(voice, 1f, bad);
            mix.Tick(bad);
            mix.Tick(-1f);
            mix.FadeVoice(voice, 0f, -1f);
            Assert.That(mix.TryPlay(route, bad, 5).IsValid, Is.False);
            Assert.That(mix.VoiceGain(voice.Slot), Is.EqualTo(0.8f));
            mix.Tick(0.5f);
            mix.TryGetAudibleGain(route, out var gain);
            Assert.That(gain, Is.EqualTo(0.5f));
            Assert.That(mix.VoiceGain(voice.Slot), Is.EqualTo(0.4f));
        }

        [Test]
        public void Reverb_UsesClampedNativeUnitsAndValidOffDefault()
        {
            var low = new SoundReverb(-20000f, -2f, -1f, true);
            Assert.That(low.ReverbLevelMillibels, Is.EqualTo(-10000f));
            Assert.That(low.DecaySeconds, Is.EqualTo(0.1f));
            Assert.That(low.DiffusionPercent, Is.Zero);
            var high = new SoundReverb(3000f, 30f, 200f, true);
            Assert.That(high.ReverbLevelMillibels, Is.EqualTo(2000f));
            Assert.That(high.DecaySeconds, Is.EqualTo(20f));
            Assert.That(high.DiffusionPercent, Is.EqualTo(100f));
            Assert.That(SoundReverb.Off, Is.EqualTo(default(SoundReverb)));
            Assert.That(default(SoundReverb).Enabled, Is.False);
        }
    }
}
