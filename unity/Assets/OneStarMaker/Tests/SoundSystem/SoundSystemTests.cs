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

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; i++)
            {
                player.Play(handle, 0.5f);
            }

            var after = GC.GetAllocatedBytesForCurrentThread();
            Assert.That(after, Is.EqualTo(before));
            Assert.That(backend.Calls, Is.EqualTo(2016));
            Assert.That(backend.Last, Is.EqualTo(handle));
            Assert.That(backend.Volume, Is.EqualTo(0.5f));

            player.Play(SoundHandle.Invalid, 0f);
            Assert.That(backend.Last, Is.EqualTo(SoundHandle.Invalid));
            Assert.That(backend.Volume, Is.EqualTo(0f));
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
