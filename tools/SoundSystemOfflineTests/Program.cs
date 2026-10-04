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
