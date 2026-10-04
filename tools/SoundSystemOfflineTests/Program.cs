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
            Run(nameof(Ring_ReusesTheOldestSlot), Ring_ReusesTheOldestSlot);
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

        private static void Ring_ReusesTheOldestSlot()
        {
            var cursor = 0;
            Equal(0, SoundVoiceRing.Next(ref cursor, 3), "slot 0");
            Equal(1, SoundVoiceRing.Next(ref cursor, 3), "slot 1");
            Equal(2, SoundVoiceRing.Next(ref cursor, 3), "slot 2");
            Equal(0, SoundVoiceRing.Next(ref cursor, 3), "wrap");
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
            Equal(0.5f, backend.Volume, "volume");

            player.Play(SoundHandle.Invalid, 0f);
            True(backend.Last == SoundHandle.Invalid, "invalid was dropped");
            Equal(0f, backend.Volume, "zero volume");
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
            True(shared.IndexOf("Play(SoundHandle handle, float volume)", StringComparison.Ordinal) >= 0, "play signature");

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
            public SoundHandle Last;
            public float Volume;

            public void Play(SoundHandle handle, float volume)
            {
                Calls++;
                Last = handle;
                Volume = volume;
            }
        }
    }
}
