#nullable enable

using System;
using NUnit.Framework;
using Unity.Profiling;
using OneStarMaker.Runtime.SoundSystem;
using UnityEngine;

namespace OneStarMaker.Tests.SoundSystem
{
    /// <summary>
    /// 純粋な転送と、実際の AudioClip / AudioSource の結合・置換・解放を検証する。
    /// EditMode の component 状態を観測し、isPlaying や可聴出力を成立条件にしない。
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
        public void Ring_ReusesTheOldestSlot()
        {
            var cursor = 0;
            Assert.That(SoundVoiceRing.Next(ref cursor, 3), Is.EqualTo(0));
            Assert.That(SoundVoiceRing.Next(ref cursor, 3), Is.EqualTo(1));
            Assert.That(SoundVoiceRing.Next(ref cursor, 3), Is.EqualTo(2));
            Assert.That(SoundVoiceRing.Next(ref cursor, 3), Is.EqualTo(0));
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

            const ProfilerRecorderOptions options =
                ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            using var recorder = new ProfilerRecorder(ProfilerCategory.Internal, "GC.Alloc", 1, options);
            Assert.That(recorder.Valid, Is.True);
            recorder.Start();
            try
            {
                var positiveControl = new byte[4096];
                GC.KeepAlive(positiveControl);
            }
            finally
            {
                recorder.Stop();
            }
            var positiveEvents = recorder.Count == 0 ? 0L : recorder.GetSample(0).Count;
            TestContext.WriteLine($"GC.Alloc unit={recorder.UnitType} positiveEvents={positiveEvents}");
            Assert.That(positiveEvents, Is.GreaterThan(0L));
            recorder.Reset();
            Assert.That(recorder.Count, Is.Zero);

            recorder.Start();
            try
            {
                for (var i = 0; i < 2000; i++)
                {
                    player.Play(handle, 0.5f);
                }
            }
            finally
            {
                recorder.Stop();
            }
            var targetEvents = recorder.Count == 0 ? 0L : recorder.GetSample(0).Count;
            TestContext.WriteLine($"forwarding: unit={recorder.UnitType}, positiveEvents={positiveEvents}, targetEvents={targetEvents}, warmup=16, iterations=2000, calls={backend.Calls}, volume={backend.Volume}");
            Assert.That(targetEvents, Is.Zero);
            Assert.That(backend.Calls, Is.EqualTo(2016));
            Assert.That(backend.Last, Is.EqualTo(handle));
            Assert.That(backend.Volume, Is.EqualTo(0.5f));

            player.Play(SoundHandle.Invalid, 0f);
            Assert.That(backend.Last, Is.EqualTo(SoundHandle.Invalid));
            Assert.That(backend.Volume, Is.EqualTo(0f));
        }

        [Test]
        public void UnityBackend_OneVoice_ReplacesBinding_AndClampsFiniteVolume()
        {
            var firstClip = CreateClip("first");
            var secondClip = CreateClip("second");
            using var backend = new UnitySoundBackend(1);
            try
            {
                var source = Field<AudioSource[]>(backend, "_voices")[0];
                var first = backend.Register(firstClip);
                var second = backend.Register(secondClip);
                Assert.That(first.IsValid && second.IsValid, Is.True);
                Assert.That(first, Is.Not.EqualTo(second));
                Assert.That(backend.RegisteredCount, Is.EqualTo(2));
                backend.Play(first, -1f);
                Assert.That(source.clip, Is.SameAs(firstClip));
                Assert.That(source.volume, Is.EqualTo(0f));
                backend.Play(second, 2f);
                Assert.That(source.clip, Is.SameAs(secondClip));
                Assert.That(source.volume, Is.EqualTo(1f));
                backend.Play(first, 0.25f);
                Assert.That(source.clip, Is.SameAs(firstClip));
                Assert.That(source.volume, Is.EqualTo(0.25f));
            }
            finally
            {
                backend.Dispose();
                DestroyClip(firstClip);
                DestroyClip(secondClip);
            }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void UnityBackend_NonfiniteGain_PreservesBindingVolumeAndRing(float gain)
        {
            var clip = CreateClip("nonfinite");
            using var backend = new UnitySoundBackend(2);
            try
            {
                var handle = backend.Register(clip);
                var voices = Field<AudioSource[]>(backend, "_voices");
                backend.Play(handle, 0.4f);
                var cursor = Field<int>(backend, "_cursor");
                Assert.DoesNotThrow(() => backend.Play(handle, gain));
                Assert.That(voices[0].clip, Is.SameAs(clip));
                Assert.That(voices[0].volume, Is.EqualTo(0.4f));
                Assert.That(voices[1].clip == null, Is.True);
                Assert.That(Field<int>(backend, "_cursor"), Is.EqualTo(cursor));
            }
            finally
            {
                backend.Dispose();
                DestroyClip(clip);
            }
        }

        [Test]
        public void UnityBackend_InvalidUnregisteredAndDestroyedClip_DoNotEvictVoice()
        {
            var liveClip = CreateClip("live");
            var deadClip = CreateClip("dead");
            using var backend = new UnitySoundBackend(2);
            try
            {
                var live = backend.Register(liveClip);
                var dead = backend.Register(deadClip);
                var source = Field<AudioSource[]>(backend, "_voices")[0];
                backend.Play(live, 0.4f);
                DestroyClip(deadClip);
                var cursor = Field<int>(backend, "_cursor");
                Assert.DoesNotThrow(() => backend.Play(SoundHandle.Invalid, 1f));
                Assert.DoesNotThrow(() => backend.Play(SoundHandle.FromRegisteredCount(3), 1f));
                Assert.DoesNotThrow(() => backend.Play(dead, 1f));
                Assert.That(source.clip, Is.SameAs(liveClip));
                Assert.That(source.volume, Is.EqualTo(0.4f));
                Assert.That(Field<int>(backend, "_cursor"), Is.EqualTo(cursor));
                Assert.That(Field<AudioSource[]>(backend, "_voices")[1].clip == null, Is.True);
                Assert.Throws<ArgumentNullException>(() => backend.Register(deadClip));
            }
            finally
            {
                backend.Dispose();
                DestroyClip(liveClip);
                DestroyClip(deadClip);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnityBackend_ExternallyDestroyedSourceOrHost_IsInert(bool destroyHost)
        {
            var clip = CreateClip("external-destroy");
            using var backend = new UnitySoundBackend(1);
            try
            {
                var handle = backend.Register(clip);
                var source = Field<AudioSource[]>(backend, "_voices")[0];
                backend.Play(handle, 0.4f);
                if (destroyHost)
                {
                    UnityEngine.Object.DestroyImmediate(Field<GameObject>(backend, "_host"));
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(source);
                }
                Assert.DoesNotThrow(() => backend.Play(handle, 1f));
                Assert.DoesNotThrow(() => backend.Dispose());
                Assert.That(backend.RegisteredCount, Is.Zero);
            }
            finally
            {
                backend.Dispose();
                DestroyClip(clip);
            }
        }

        [Test]
        public void UnityBackend_Dispose_ClearsBorrowedTableAndHost_AndIsIdempotent()
        {
            var firstClip = CreateClip("dispose-first");
            var secondClip = CreateClip("dispose-second");
            using var backend = new UnitySoundBackend(1);
            try
            {
                var handle = backend.Register(firstClip);
                backend.Register(secondClip);
                var retainedTable = Field<AudioClip[]>(backend, "_clips");
                var host = Field<GameObject>(backend, "_host");
                var source = Field<AudioSource[]>(backend, "_voices")[0];
                backend.Play(handle, 0.4f);
                backend.Dispose();
                Assert.That(backend.RegisteredCount, Is.Zero);
                Assert.That(Field<AudioClip[]>(backend, "_clips"), Is.Empty);
                foreach (var entry in retainedTable)
                {
                    Assert.That(entry == null, Is.True);
                }
                // EditMode DestroyImmediate removes native components; clips remain caller-owned.
                Assert.That(host == null, Is.True);
                Assert.That(source == null, Is.True);
                Assert.That(firstClip != null && secondClip != null, Is.True);
                Assert.DoesNotThrow(() => backend.Dispose());
                Assert.DoesNotThrow(() => backend.Play(handle, 1f));
                Assert.Throws<ObjectDisposedException>(() => backend.Register(firstClip));
            }
            finally
            {
                backend.Dispose();
                DestroyClip(firstClip);
                DestroyClip(secondClip);
            }
        }

        [Test]
        public void CallerProtocolFixture_DisposesBackendBeforeManualClipRelease()
        {
            // A caller-protocol spy, not an IAssetManagement integration proof.
            var clip = CreateClip("manual-protocol");
            using var backend = new UnitySoundBackend(1);
            var released = false;
            try
            {
                backend.Play(backend.Register(clip), 0.4f);
                var retainedTable = Field<AudioClip[]>(backend, "_clips");
                DisposeThenRelease(backend, () =>
                {
                    Assert.That(backend.RegisteredCount, Is.Zero);
                    Assert.That(Field<GameObject>(backend, "_host") == null, Is.True);
                    foreach (var entry in retainedTable)
                    {
                        Assert.That(entry == null, Is.True);
                    }
                    DestroyClip(clip);
                    released = true;
                });
                Assert.That(released, Is.True);
            }
            finally
            {
                backend.Dispose();
                DestroyClip(clip);
            }
        }

        [Test]
        public void UnityBackend_RejectsEmptyPool_NullClip_AndRegistrationOverflowBeforeMutation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UnitySoundBackend(0));
            var clip = CreateClip("registration-limit");
            using var backend = new UnitySoundBackend(1);
            try
            {
                Assert.Throws<ArgumentNullException>(() => backend.Register(null!));
                var table = Field<AudioClip[]>(backend, "_clips");
                SetField(backend, "_count", int.MaxValue);
                Assert.Throws<InvalidOperationException>(() => backend.Register(clip));
                Assert.That(Field<int>(backend, "_count"), Is.EqualTo(int.MaxValue));
                Assert.That(Field<AudioClip[]>(backend, "_clips"), Is.SameAs(table));
            }
            finally
            {
                backend.Dispose();
                DestroyClip(clip);
            }
        }

        private static void DisposeThenRelease(IDisposable backend, Action releaseManualClip)
        {
            backend.Dispose();
            releaseManualClip();
        }

        private static AudioClip CreateClip(string name) => AudioClip.Create(name, 32, 1, 8000, false);

        private static void DestroyClip(AudioClip clip)
        {
            if (clip != null)
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        // Observe private native state without adding production inspection API.
        private static T Field<T>(UnitySoundBackend backend, string name)
        {
            var field = typeof(UnitySoundBackend).GetField(name, System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!;
            return (T)field.GetValue(backend)!;
        }

        private static void SetField(UnitySoundBackend backend, string name, int value)
        {
            var field = typeof(UnitySoundBackend).GetField(name, System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!;
            field.SetValue(backend, value);
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
