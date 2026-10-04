#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using OneStarMaker.Runtime.SoundSystem;

namespace OneStarMaker.SoundSystemOfflineTests
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main()
        {
            Run(nameof(Handle_DefaultIsInvalid_AndRegisteredValuesAreDense), Handle_DefaultIsInvalid_AndRegisteredValuesAreDense);
            Run(nameof(Mix_StealsOnlyTheLowerOrOlderVoice_AndFades), Mix_StealsOnlyTheLowerOrOlderVoice_AndFades);
            Run(nameof(Mix_PlayAndTick_DoNotAllocate), Mix_PlayAndTick_DoNotAllocate);
            Run(nameof(Settings_ValidateFiniteInputsAndNativeUnits), Settings_ValidateFiniteInputsAndNativeUnits);
            Run(nameof(Mix_InvalidCallsPreserveAdmissionAndFades), Mix_InvalidCallsPreserveAdmissionAndFades);
            Run(nameof(Mix_EqualPriorityEvictsOldestAndGuardsGeneration), Mix_EqualPriorityEvictsOldestAndGuardsGeneration);
            Run(nameof(Mix_InstantClampAndSteppedFades), Mix_InstantClampAndSteppedFades);
            Run(nameof(Player_ForwardsHandleAndVolume_WithoutAllocating), Player_ForwardsHandleAndVolume_WithoutAllocating);
            Run(nameof(Player_RejectsMissingBackend), Player_RejectsMissingBackend);
            Run(nameof(Sources_KeepClipTypesOffTheSharedPlaySurface), Sources_KeepClipTypesOffTheSharedPlaySurface);

            Console.WriteLine($"SoundSystem offline: {_passed} passed, {_failed} failed, {_passed + _failed} executed");
            return _failed == 0 && _passed > 0 ? 0 : 1;
        }

        private static void Run(string name, Action body)
        {
            try
            {
                body();
                _passed++;
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("FAIL " + name);
                Console.WriteLine(ex);
            }
        }

        private static void Handle_DefaultIsInvalid_AndRegisteredValuesAreDense()
        {
            True(!SoundHandle.Invalid.IsValid, "default is valid");
            int ignored;
            True(!SoundHandleIndex.TryGet(SoundHandle.Invalid, 1, out ignored), "invalid looked up");

            var first = SoundHandle.FromRegisteredCount(1);
            var second = SoundHandle.FromRegisteredCount(2);
            int firstIndex;
            int secondIndex;
            True(SoundHandleIndex.TryGet(first, 2, out firstIndex), "first missing");
            Equal(0, firstIndex, "first index");
            True(SoundHandleIndex.TryGet(second, 2, out secondIndex), "second missing");
            Equal(1, secondIndex, "second index");
            True(!SoundHandleIndex.TryGet(second, 1, out ignored), "second accepted too early");
            True(first != second, "handles collapsed");
        }

        private static void Mix_StealsOnlyTheLowerOrOlderVoice_AndFades()
        {
            var mix = new SoundMix(2);
            var master = mix.Add(new SoundVolumeSettings(1f, 0, SoundReverb.Off));
            True(!mix.TryPlay(SoundVolumeId.Invalid, 1f, 0).IsValid, "invalid region played");

            var high = mix.TryPlay(master, 1f, 5);
            var low = mix.TryPlay(master, 1f, 3);
            True(high.IsValid && low.IsValid && high != low, "two voices");
            var rejected = mix.TryPlay(master, 1f, 2);
            True(!rejected.IsValid, "lower than both was accepted");
            var mid = mix.TryPlay(master, 1f, 4);
            True(mid.IsValid, "mid was rejected");
            mix.FadeVoice(low, 0f, 0f);
            True(mix.IsActive(low.Slot), "lower slot was not the one replaced");
            mix.FadeVoice(high, 0.5f, 0f);
            Equal(0.5f, mix.VoiceGain(high.Slot), "higher voice was replaced");

            var reverb = new SoundReverb(0.4f, 1.5f, 0.8f, true);
            mix.SetReverb(master, reverb);
            SoundVolumeSettings settings;
            True(mix.TryGetSettings(master, out settings), "settings missing");
            True(settings.Reverb == reverb, "reverb was dropped");

            mix.FadeVolume(master, 0f, 1f);
            mix.Tick(0.5f);
            float audible;
            True(mix.TryGetAudibleGain(master, out audible), "audible missing");
            Equal(0.5f, audible, "volume fade");
            True(mix.IsActive(high.Slot), "volume fade freed a voice");

            mix.FadeVoice(mid, 0f, 0.5f);
            mix.Tick(0.5f);
            True(!mix.IsActive(mid.Slot), "voice fade did not release");
            mix.FadeVoice(mid, 1f, 0f);
            True(!mix.IsActive(mid.Slot), "stale voice was revived");
        }

        private static void Mix_PlayAndTick_DoNotAllocate()
        {
            var mix = new SoundMix(4);
            var master = mix.Add(new SoundVolumeSettings(1f, 1, SoundReverb.Off));
            for (var i = 0; i < 16; i++)
            {
                mix.TryPlay(master, 1f, 1);
                mix.Tick(0.016f);
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; i++)
            {
                mix.TryPlay(master, 1f, 1);
                mix.Tick(0.016f);
            }

            var after = GC.GetAllocatedBytesForCurrentThread();
            Equal(before, after, "mix allocated");
        }

        private static void Settings_ValidateFiniteInputsAndNativeUnits()
        {
            Equal(0f, new SoundVolumeSettings(-1f, 0, default).Gain, "low gain clamp");
            Equal(1f, new SoundVolumeSettings(2f, 0, default).Gain, "high gain clamp");
            foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Throws<ArgumentOutOfRangeException>(() => new SoundVolumeSettings(bad, 0, default));
                foreach (var enabled in new[] { false, true })
                {
                    Throws<ArgumentOutOfRangeException>(() => new SoundReverb(bad, 1f, 100f, enabled));
                    Throws<ArgumentOutOfRangeException>(() => new SoundReverb(0f, bad, 100f, enabled));
                    Throws<ArgumentOutOfRangeException>(() => new SoundReverb(0f, 1f, bad, enabled));
                }
            }
            var low = new SoundReverb(-20000f, -1f, -1f, true);
            Equal(-10000f, low.ReverbLevelMillibels, "level minimum");
            Equal(0.1f, low.DecaySeconds, "decay minimum");
            Equal(0f, low.DiffusionPercent, "diffusion minimum");
            var high = new SoundReverb(3000f, 30f, 200f, true);
            Equal(2000f, high.ReverbLevelMillibels, "level maximum");
            Equal(20f, high.DecaySeconds, "decay maximum");
            Equal(100f, high.DiffusionPercent, "diffusion maximum");
            True(!default(SoundReverb).Enabled && SoundReverb.Off == default, "Off/default");
        }

        private static void Mix_InvalidCallsPreserveAdmissionAndFades()
        {
            var mix = new SoundMix(2);
            var route = mix.Add(new SoundVolumeSettings(1f, 7, default));
            var first = mix.TryPlay(route, 0.8f, 2);
            mix.FadeVolume(route, 0f, 2f);
            mix.FadeVoice(first, 0.4f, 2f);
            foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                True(!mix.TryPlay(route, bad, 100).IsValid, "nonfinite admission");
                mix.FadeVolume(route, bad, 0f);
                mix.FadeVolume(route, 1f, bad);
                mix.FadeVoice(first, bad, 0f);
                mix.FadeVoice(first, 0f, bad);
                mix.Tick(bad);
            }
            mix.Tick(-1f);
            mix.Tick(0f);
            mix.FadeVolume(route, 1f, -1f);
            mix.FadeVoice(first, 0f, -1f);
            mix.FadeVolume(SoundVolumeId.Invalid, 0f, 0f);
            mix.SetReverb(SoundVolumeId.Invalid, new SoundReverb(0f, 1f, 100f, true));
            mix.TryGetAudibleGain(route, out var gain);
            Equal(1f, gain, "invalid tick/fade mutated route");
            Equal(0.8f, mix.VoiceGain(first.Slot), "invalid voice fade mutated");
            Equal(1, mix.TryPlay(route, 1f, 2).Slot, "rejection consumed empty slot");
            mix.Tick(1f);
            mix.TryGetAudibleGain(route, out gain);
            Equal(0.5f, gain, "original route fade lost");
            Equal(0.6f, mix.VoiceGain(first.Slot), "original voice fade lost");
        }

        private static void Mix_EqualPriorityEvictsOldestAndGuardsGeneration()
        {
            var mix = new SoundMix(2);
            var route = mix.Add(default);
            var first = mix.TryPlay(route, 1f, 2);
            var second = mix.TryPlay(route, 1f, 2);
            True(!mix.TryPlay(route, 1f, 1).IsValid, "lower admitted");
            var replacement = mix.TryPlay(route, 1f, 2);
            Equal(first.Slot, replacement.Slot, "oldest tie");
            True(first != replacement, "generation reused");
            mix.FadeVoice(first, 0f, 0f);
            True(mix.IsActive(replacement.Slot), "stale ID released replacement");
            mix.FadeVoice(replacement, 0f, 0f);
            True(!mix.IsActive(replacement.Slot) && mix.IsActive(second.Slot), "release isolation");
            var reused = mix.TryPlay(route, 1f, 0);
            True(reused != replacement, "released generation reused");
        }

        private static void Mix_InstantClampAndSteppedFades()
        {
            var mix = new SoundMix(1);
            var route = mix.Add(new SoundVolumeSettings(1f, 0, default));
            var voice = mix.TryPlay(route, 2f, 0);
            Equal(1f, mix.VoiceGain(voice.Slot), "voice high clamp");
            mix.FadeVolume(route, -2f, 0f);
            mix.TryGetAudibleGain(route, out var gain);
            Equal(0f, gain, "instant route low clamp");
            True(mix.IsActive(voice.Slot), "route zero releases voice");
            mix.FadeVolume(route, 2f, 1f);
            mix.FadeVoice(voice, 0f, 1f);
            mix.Tick(0.25f);
            mix.TryGetAudibleGain(route, out gain);
            Equal(0.25f, gain, "route interpolation");
            Equal(0.75f, mix.VoiceGain(voice.Slot), "voice interpolation");
            mix.Tick(5f);
            mix.TryGetAudibleGain(route, out gain);
            Equal(1f, gain, "route overshoot");
            True(!mix.IsActive(voice.Slot), "completed zero fade not released");
        }

        private static void Player_ForwardsHandleAndVolume_WithoutAllocating()
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
            Equal(before, after, "allocated bytes");
            Equal(2016, backend.Calls, "calls");
            True(backend.Last == handle, "last handle");
            Equal(0.5f, backend.Gain, "gain");

            player.Play(SoundHandle.Invalid, 0f);
            True(backend.Last == SoundHandle.Invalid, "invalid was dropped");
            Equal(0f, backend.Gain, "zero gain");

            var region = SoundVolumeId.FromRegisteredCount(2);
            var reverb = new SoundReverb(0.2f, 1f, 0.3f, true);
            var voice = SoundVoiceId.Create(0, 1);
            for (var i = 0; i < 16; i++)
            {
                player.Play(handle, region, 0.5f, 3);
                player.FadeVolume(region, 0f, 1f);
                player.FadeVoice(voice, 0f, 1f);
                player.SetReverb(region, reverb);
                player.Tick(0.016f);
            }

            before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; i++)
            {
                player.Play(handle, region, 0.5f, 3);
                player.FadeVolume(region, 0f, 1f);
                player.FadeVoice(voice, 0f, 1f);
                player.SetReverb(region, reverb);
                player.Tick(0.016f);
            }

            after = GC.GetAllocatedBytesForCurrentThread();
            Equal(before, after, "region calls allocated");
            Equal(2016, backend.RoutedCalls, "routed calls");
            True(backend.Region == region, "region");
            Equal(3, backend.Priority, "priority");
            True(backend.Reverb == reverb, "reverb");
            Equal(2016, backend.Ticks, "ticks");
        }

        private static void Player_RejectsMissingBackend()
        {
            ISoundBackend? missing = null;
            Throws<ArgumentNullException>(() => new SoundPlayer(missing!));
        }

        private static void Sources_KeepClipTypesOffTheSharedPlaySurface()
        {
            var root = FindRepoRoot();
            var directory = Path.Combine(root, "unity", "Assets", "OneStarMaker", "Scripts", "Runtime", "SoundSystem");
            var shared = File.ReadAllText(Path.Combine(directory, "ISoundBackend.cs"));
            True(shared.IndexOf("AudioClip", StringComparison.Ordinal) < 0, "shared surface names AudioClip");
            True(shared.IndexOf("string", StringComparison.Ordinal) < 0, "shared surface names string");
            True(shared.IndexOf("Play(SoundHandle handle, float gain)", StringComparison.Ordinal) >= 0, "play signature");
            True(shared.IndexOf("AudioMixer", StringComparison.Ordinal) < 0, "shared surface names a mixer type");

            foreach (var file in Directory.GetFiles(directory, "*.cs"))
            {
                var text = File.ReadAllText(file);
                var name = Path.GetFileName(file);
                True(text.IndexOf("SampleGame", StringComparison.Ordinal) < 0, name + " names SampleGame");
                True(text.IndexOf("UnityEditor", StringComparison.Ordinal) < 0, name + " names UnityEditor");
                True(text.IndexOf("record ", StringComparison.Ordinal) < 0, name + " declares a record");
                True(text.IndexOf("enum ", StringComparison.Ordinal) < 0, name + " declares an enum");
            }

            var unity = File.ReadAllText(Path.Combine(directory, "UnitySoundBackend.cs"));
            True(unity.IndexOf("AudioClip", StringComparison.Ordinal) >= 0, "unity backend lost AudioClip");
            True(unity.IndexOf("class Cri", StringComparison.Ordinal) < 0, "CRI body appeared");
            True(unity.IndexOf("Wwise", StringComparison.Ordinal) < 0, "Wwise body appeared");
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(
                    dir.FullName,
                    "unity",
                    "Assets",
                    "OneStarMaker",
                    "Scripts",
                    "Runtime",
                    "SoundSystem",
                    "SoundHandle.cs");
                if (File.Exists(candidate))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("リポジトリルートを見つけられません: " + AppContext.BaseDirectory);
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " expected " + expected + " actual " + actual);
            }
        }

        private static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void Throws<T>(Action body)
            where T : Exception
        {
            try
            {
                body();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException("expected " + typeof(T).Name);
        }

        private sealed class RecordingBackend : ISoundBackend
        {
            public int Calls;
            public int RoutedCalls;
            public int Ticks;
            public SoundHandle Last;
            public float Gain;
            public SoundVolumeId Region;
            public int Priority;
            public SoundReverb Reverb;

            public SoundVoiceId Play(SoundHandle handle, float gain)
            {
                Calls++;
                Last = handle;
                Gain = gain;
                return SoundVoiceId.Create(0, 1);
            }

            public SoundVoiceId Play(SoundHandle handle, SoundVolumeId volume, float gain, int priority)
            {
                RoutedCalls++;
                Last = handle;
                Region = volume;
                Gain = gain;
                Priority = priority;
                return SoundVoiceId.Create(0, 1);
            }

            public void FadeVolume(SoundVolumeId volume, float targetGain, float seconds)
            {
                Region = volume;
            }

            public void FadeVoice(SoundVoiceId voice, float targetGain, float seconds)
            {
            }

            public void SetReverb(SoundVolumeId volume, SoundReverb reverb)
            {
                Region = volume;
                Reverb = reverb;
            }

            public void Tick(float deltaTime)
            {
                Ticks++;
            }
        }
    }
}
