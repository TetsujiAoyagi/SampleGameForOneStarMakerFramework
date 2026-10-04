#nullable enable

using System;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 論理ミックス経路と再生スロットの状態。クリップもミキサーも知らない。
    /// 空きが無ければ、新しい音より高い優先度だけが埋まっているとき新しい音を捨てる。
    /// それ以外は最も低い優先度を止め、同点なら最も古いものを止める。
    /// 領域のフェードはスロットを空けない。再生のフェードが 0 に着いたらそのスロットを空ける。
    /// </summary>
    internal sealed class SoundMix
    {
        private readonly Voice[] _voices;
        private Volume[] _volumes;
        private int _volumeCount;
        private long _sequence;

        public SoundMix(int voiceCount)
        {
            if (voiceCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(voiceCount), "同時再生数は 1 以上です。");
            }

            _voices = new Voice[voiceCount];
            _volumes = Array.Empty<Volume>();
        }

        public int VoiceCount => _voices.Length;

        public int VolumeCount => _volumeCount;

        public SoundVolumeId Add(SoundVolumeSettings settings)
        {
            if (_volumeCount == int.MaxValue)
                throw new InvalidOperationException("SoundVolumeId の登録上限です。");

            if (_volumeCount == _volumes.Length)
            {
                var next = _volumeCount == 0 ? 4 : (int)Math.Min((long)_volumeCount * 2, int.MaxValue);
                Array.Resize(ref _volumes, next);
            }

            _volumes[_volumeCount] = Volume.Create(settings);
            _volumeCount++;
            return SoundVolumeId.FromRegisteredCount(_volumeCount);
        }

        public bool TryGetSettings(SoundVolumeId volume, out SoundVolumeSettings settings)
        {
            if (!SoundVolumeIndex.TryGet(volume, _volumeCount, out var index))
            {
                settings = default;
                return false;
            }

            settings = _volumes[index].Settings;
            return true;
        }

        public bool TryGetAudibleGain(SoundVolumeId volume, out float gain)
        {
            if (!SoundVolumeIndex.TryGet(volume, _volumeCount, out var index))
            {
                gain = 0f;
                return false;
            }

            gain = _volumes[index].Current;
            return true;
        }

        public void SetReverb(SoundVolumeId volume, SoundReverb reverb)
        {
            if (!SoundVolumeIndex.TryGet(volume, _volumeCount, out var index))
            {
                return;
            }

            var slot = _volumes[index];
            slot.Settings = new SoundVolumeSettings(slot.Settings.Gain, slot.Settings.Priority, reverb);
            _volumes[index] = slot;
        }

        public void FadeVolume(SoundVolumeId volume, float targetGain, float seconds)
        {
            if (!ValidFade(targetGain, seconds) || !SoundVolumeIndex.TryGet(volume, _volumeCount, out var index))
            {
                return;
            }

            var slot = _volumes[index];
            slot.Settings = new SoundVolumeSettings(targetGain, slot.Settings.Priority, slot.Settings.Reverb);
            StartFade(ref slot.Current, ref slot.Target, ref slot.Speed, targetGain, seconds);
            _volumes[index] = slot;
        }

        public void FadeVoice(SoundVoiceId voice, float targetGain, float seconds)
        {
            if (!ValidFade(targetGain, seconds) || !TrySlot(voice, out var index))
            {
                return;
            }

            var slot = _voices[index];
            StartFade(ref slot.Current, ref slot.Target, ref slot.Speed, targetGain, seconds);
            if (slot.Speed <= 0f && slot.Target == 0f)
            {
                _voices[index] = slot;
                Release(index);
                return;
            }

            _voices[index] = slot;
        }

        public SoundVoiceId TryPlay(SoundVolumeId volume, float gain, int priority)
        {
            var slot = SelectSlot(volume, gain, priority);
            if (slot < 0)
            {
                return SoundVoiceId.Invalid;
            }

            return Occupy(slot, volume.Value - 1, Math.Max(0f, Math.Min(1f, gain)), priority);
        }

        // Native callers inspect the selected components before admission mutates age/generation.
        internal int SelectSlot(SoundVolumeId volume, float gain, int priority)
        {
            if (float.IsNaN(gain) || float.IsInfinity(gain) ||
                !SoundVolumeIndex.TryGet(volume, _volumeCount, out _))
                return -1;

            var slot = FindFree();
            return slot < 0 ? FindVictim(priority) : slot;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }

            for (var i = 0; i < _volumeCount; i++)
            {
                var slot = _volumes[i];
                if (slot.Speed <= 0f)
                {
                    continue;
                }

                slot.Current = SoundFade.Advance(slot.Current, slot.Target, slot.Speed, deltaTime, out var arrived);
                if (arrived)
                {
                    slot.Speed = 0f;
                }

                _volumes[i] = slot;
            }

            for (var i = 0; i < _voices.Length; i++)
            {
                var slot = _voices[i];
                if (!slot.Active || slot.Speed <= 0f)
                {
                    continue;
                }

                slot.Current = SoundFade.Advance(slot.Current, slot.Target, slot.Speed, deltaTime, out var arrived);
                if (arrived)
                {
                    slot.Speed = 0f;
                }

                _voices[i] = slot;
                if (arrived && slot.Target == 0f)
                {
                    Release(i);
                }
            }
        }

        public bool IsActive(int slot)
        {
            return (uint)slot < (uint)_voices.Length && _voices[slot].Active;
        }

        public float VoiceGain(int slot)
        {
            return _voices[slot].Current;
        }

        public float RegionGain(int volumeIndex)
        {
            return _volumes[volumeIndex].Current;
        }

        public int VolumeIndex(int slot)
        {
            return _voices[slot].VolumeIndex;
        }

        public void Release(int slot)
        {
            if ((uint)slot >= (uint)_voices.Length || !_voices[slot].Active)
            {
                return;
            }

            var voice = _voices[slot];
            voice.Active = false;
            voice.Speed = 0f;
            voice.Generation++;
            if (voice.Generation == 0)
            {
                voice.Generation = 1;
            }

            _voices[slot] = voice;
        }

        private SoundVoiceId Occupy(int slot, int volumeIndex, float gain, int priority)
        {
            var voice = _voices[slot];
            voice.Generation++;
            if (voice.Generation == 0)
            {
                voice.Generation = 1;
            }

            voice.Active = true;
            voice.Priority = priority;
            voice.Sequence = ++_sequence;
            voice.VolumeIndex = volumeIndex;
            voice.Current = gain;
            voice.Target = gain;
            voice.Speed = 0f;
            _voices[slot] = voice;
            return SoundVoiceId.Create(slot, voice.Generation);
        }

        private int FindFree()
        {
            for (var i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].Active)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindVictim(int incoming)
        {
            var found = -1;
            var bestPriority = 0;
            var bestSequence = 0L;
            for (var i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].Active)
                {
                    continue;
                }

                var priority = _voices[i].Priority;
                var sequence = _voices[i].Sequence;
                if (found < 0 || priority < bestPriority || (priority == bestPriority && sequence < bestSequence))
                {
                    found = i;
                    bestPriority = priority;
                    bestSequence = sequence;
                }
            }

            if (found < 0 || bestPriority > incoming)
            {
                return -1;
            }

            return found;
        }

        internal bool TrySlot(SoundVoiceId voice, out int slot)
        {
            slot = voice.Slot;
            if (!voice.IsValid || (uint)slot >= (uint)_voices.Length)
            {
                return false;
            }

            var current = _voices[slot];
            return current.Active && current.Generation == voice.Generation;
        }

        private static void StartFade(ref float current, ref float target, ref float speed, float next, float seconds)
        {
            next = Math.Max(0f, Math.Min(1f, next));
            target = next;
            speed = SoundFade.Speed(current, next, seconds);
            if (speed <= 0f)
            {
                current = next;
            }
        }

        internal static bool ValidFade(float target, float seconds)
        {
            return !float.IsNaN(target) && !float.IsInfinity(target) &&
                !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds >= 0f;
        }

        private struct Voice
        {
            public int Generation;
            public bool Active;
            public int Priority;
            public long Sequence;
            public int VolumeIndex;
            public float Current;
            public float Target;
            public float Speed;
        }

        private struct Volume
        {
            public SoundVolumeSettings Settings;
            public float Current;
            public float Target;
            public float Speed;

            public static Volume Create(SoundVolumeSettings settings)
            {
                return new Volume
                {
                    Settings = settings,
                    Current = settings.Gain,
                    Target = settings.Gain,
                    Speed = 0f,
                };
            }
        }
    }
}
