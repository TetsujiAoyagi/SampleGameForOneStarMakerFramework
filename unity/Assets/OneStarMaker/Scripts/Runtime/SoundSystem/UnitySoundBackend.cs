#nullable enable

using System;
using UnityEngine;
using UnityEngine.Audio;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// Unity の AudioSource を固定本数だけ使い回し、領域ごとにミキサーグループへ流す。
    /// 領域の音量フェードは、露出パラメータが有るときミキサーへ送り、ソースの音量には掛けない。
    /// パラメータが無いときはソース音量に領域の音量を掛ける。二重に下げないため。
    /// 優先度で捨てた再生と、破棄後の Play は無効な識別子で戻る。例外オブジェクトを作らないため。
    /// 自然に鳴り終わったスロットは、再生位置が進んで止まっているときだけ空ける。
    /// </summary>
    public sealed class UnitySoundBackend : ISoundBackend, IDisposable
    {
        private readonly GameObject _host;
        private readonly AudioSource[] _voices;
        private readonly SoundMix _mix;
        private readonly SoundVolumeId _defaultVolume;
        private AudioClip[] _clips;
        private Route[] _routes;
        private int _count;
        private bool _disposed;

        public UnitySoundBackend(int voiceCount)
        {
            if (voiceCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(voiceCount), "同時再生数は 1 以上です。");
            }

            var host = new GameObject("OSM Sound");
            host.hideFlags = HideFlags.HideAndDontSave;
            var voices = new AudioSource[voiceCount];
            try
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.DontDestroyOnLoad(host);
                }

                for (var i = 0; i < voiceCount; i++)
                {
                    var source = host.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.loop = false;
                    source.spatialBlend = 0f;
                    voices[i] = source;
                }
            }
            catch
            {
                DestroyHost(host);
                throw;
            }

            _host = host;
            _voices = voices;
            _clips = Array.Empty<AudioClip>();
            _routes = new Route[4];
            _mix = new SoundMix(voiceCount);
            _defaultVolume = AddVolume(null!, new SoundVolumeSettings(1f, 0, SoundReverb.Off), default);
        }

        public int RegisteredCount => _count;

        public SoundVolumeId DefaultVolume => _defaultVolume;

        public SoundHandle Register(AudioClip clip)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UnitySoundBackend));
            }

            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (_count == _clips.Length)
            {
                var next = _count == 0 ? 4 : _count * 2;
                Array.Resize(ref _clips, next);
            }

            _clips[_count] = clip;
            _count++;
            return SoundHandle.FromRegisteredCount(_count);
        }

        public SoundVolumeId RegisterVolume(SoundVolumeSettings settings)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UnitySoundBackend));
            }

            return AddVolume(null!, settings, default);
        }

        public SoundVolumeId RegisterVolume(AudioMixerGroup group, SoundVolumeSettings settings, UnityMixerParameters parameters)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UnitySoundBackend));
            }

            return AddVolume(group, settings, parameters);
        }

        public bool TryGetSettings(SoundVolumeId volume, out SoundVolumeSettings settings)
        {
            return _mix.TryGetSettings(volume, out settings);
        }

        public bool TryGetAudibleGain(SoundVolumeId volume, out float gain)
        {
            return _mix.TryGetAudibleGain(volume, out gain);
        }

        public SoundVoiceId Play(SoundHandle handle, float gain)
        {
            var priority = 0;
            if (_mix.TryGetSettings(_defaultVolume, out var settings))
            {
                priority = settings.Priority;
            }

            return Play(handle, _defaultVolume, gain, priority);
        }

        public SoundVoiceId Play(SoundHandle handle, SoundVolumeId volume, float gain, int priority)
        {
            if (_disposed || !SoundHandleIndex.TryGet(handle, _count, out var clipIndex))
            {
                return SoundVoiceId.Invalid;
            }

            var clip = _clips[clipIndex];
            if (clip == null)
            {
                return SoundVoiceId.Invalid;
            }

            CollectFinished();
            var voice = _mix.TryPlay(volume, gain, priority);
            if (!voice.IsValid)
            {
                return voice;
            }

            var source = _voices[voice.Slot];
            if (source == null)
            {
                _mix.Release(voice.Slot);
                return SoundVoiceId.Invalid;
            }

            var volumeIndex = _mix.VolumeIndex(voice.Slot);
            source.Stop();
            source.clip = clip;
            source.outputAudioMixerGroup = _routes[volumeIndex].Group;
            source.volume = SourceGain(voice.Slot, volumeIndex);
            source.Play();
            return voice;
        }

        public void FadeVolume(SoundVolumeId volume, float targetGain, float seconds)
        {
            if (_disposed)
            {
                return;
            }

            _mix.FadeVolume(volume, targetGain, seconds);
            if (SoundVolumeIndex.TryGet(volume, _mix.VolumeCount, out var index))
            {
                ApplyRoute(index);
            }
        }

        public void FadeVoice(SoundVoiceId voice, float targetGain, float seconds)
        {
            if (_disposed)
            {
                return;
            }

            _mix.FadeVoice(voice, targetGain, seconds);
            ApplyVoices();
        }

        public void SetReverb(SoundVolumeId volume, SoundReverb reverb)
        {
            if (_disposed)
            {
                return;
            }

            _mix.SetReverb(volume, reverb);
            if (SoundVolumeIndex.TryGet(volume, _mix.VolumeCount, out var index))
            {
                ApplyRoute(index);
            }
        }

        public void Tick(float deltaTime)
        {
            if (_disposed)
            {
                return;
            }

            _mix.Tick(deltaTime);
            CollectFinished();
            ApplyVoices();
            for (var i = 0; i < _mix.VolumeCount; i++)
            {
                ApplyRoute(i);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DestroyHost(_host);
        }

        private SoundVolumeId AddVolume(AudioMixerGroup group, SoundVolumeSettings settings, UnityMixerParameters parameters)
        {
            var id = _mix.Add(settings);
            var index = id.Value - 1;
            if (index >= _routes.Length)
            {
                var next = _routes.Length == 0 ? 4 : _routes.Length * 2;
                while (next <= index)
                {
                    next *= 2;
                }

                Array.Resize(ref _routes, next);
            }

            var route = new Route
            {
                Gain = parameters.Gain,
                Wet = parameters.ReverbWet,
                Decay = parameters.ReverbDecay,
                Diffusion = parameters.ReverbDiffusion,
            };
            if (group != null)
            {
                route.Group = group;
                route.Mixer = group.audioMixer;
            }

            _routes[index] = route;
            ApplyRoute(index);
            return id;
        }

        private void CollectFinished()
        {
            for (var i = 0; i < _voices.Length; i++)
            {
                if (!_mix.IsActive(i))
                {
                    continue;
                }

                var source = _voices[i];
                if (source == null || (!source.isPlaying && source.timeSamples > 0))
                {
                    _mix.Release(i);
                }
            }
        }

        private void ApplyVoices()
        {
            for (var i = 0; i < _voices.Length; i++)
            {
                var source = _voices[i];
                if (source == null)
                {
                    continue;
                }

                if (!_mix.IsActive(i))
                {
                    if (source.isPlaying)
                    {
                        source.Stop();
                    }

                    continue;
                }

                source.volume = SourceGain(i, _mix.VolumeIndex(i));
            }
        }

        private float SourceGain(int slot, int volumeIndex)
        {
            var voice = _mix.VoiceGain(slot);
            if (!string.IsNullOrEmpty(_routes[volumeIndex].Gain))
            {
                return voice;
            }

            return voice * _mix.RegionGain(volumeIndex);
        }

        private void ApplyRoute(int index)
        {
            var route = _routes[index];
            if (route.Mixer == null)
            {
                return;
            }

            var audible = _mix.RegionGain(index);
            Push(route.Mixer, route.Gain, audible, ref route.SentGain, ref route.LastGain);
            if (_mix.TryGetSettings(SoundVolumeId.FromRegisteredCount(index + 1), out var settings))
            {
                var reverb = settings.Reverb;
                var wet = reverb.Enabled ? reverb.Wet : 0f;
                Push(route.Mixer, route.Wet, wet, ref route.SentWet, ref route.LastWet);
                Push(route.Mixer, route.Decay, reverb.DecaySeconds, ref route.SentDecay, ref route.LastDecay);
                Push(route.Mixer, route.Diffusion, reverb.Diffusion, ref route.SentDiffusion, ref route.LastDiffusion);
            }

            _routes[index] = route;
        }

        private static void Push(AudioMixer mixer, string parameter, float value, ref bool sent, ref float last)
        {
            if (mixer == null || string.IsNullOrEmpty(parameter) || (sent && last == value))
            {
                return;
            }

            mixer.SetFloat(parameter, value);
            last = value;
            sent = true;
        }

        private static void DestroyHost(GameObject host)
        {
            if (host == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(host);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private struct Route
        {
            public AudioMixerGroup Group;
            public AudioMixer Mixer;
            public string Gain;
            public string Wet;
            public string Decay;
            public string Diffusion;
            public bool SentGain;
            public bool SentWet;
            public bool SentDecay;
            public bool SentDiffusion;
            public float LastGain;
            public float LastWet;
            public float LastDecay;
            public float LastDiffusion;
        }
    }
}
