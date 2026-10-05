#nullable enable

using System;
using UnityEngine;
using UnityEngine.Audio;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// Optional fixed native voice pool with backend-local logical mix routes.
    /// Construction, registration, playback, fades, Tick and disposal use Unity's main thread.
    /// Source gain is voice gain times route gain; authored mixer effects remain caller policy.
    /// </summary>
    /// <remarks>
    /// The creator owns the backend and host. All clips and configured groups are borrowed until Dispose.
    /// Obtain clips through IAssetManagement with AssetOwner.Manual; release IAssetHandle&lt;AudioClip&gt;
    /// only after Dispose. App requires explicit Dispose-before-ReleaseAll ordering.
    /// Scene(id) / Bind(go) auto-expiring owners are unsupported. No borrowed asset is released/destroyed here.
    /// SoundPlayer borrows this backend. Register caller-owned Tick with UpdateSystemRuntime and honor its frame order.
    /// Native Pause/Stop is backend-owned: nonplaying sources are collected at the next valid Tick.
    /// Component configuration and Play do not guarantee audible output.
    /// </remarks>
    public sealed class UnitySoundBackend : ISoundBackend, IDisposable
    {
        private readonly GameObject _host;
        private readonly AudioSource[] _voices;
        private readonly AudioReverbFilter[] _filters;
        private readonly SoundMix _mix;
        private readonly SoundVolumeId _defaultVolume;
        private AudioClip[] _clips;
        private Route[] _routes;
        private int _count;
        private bool _disposed;

        public UnitySoundBackend(int voiceCount)
        {
            if (voiceCount < 1)
                throw new ArgumentOutOfRangeException(nameof(voiceCount));
            var host = new GameObject("OSM Sound");
            try
            {
                host.hideFlags = HideFlags.HideAndDontSave;
                var voices = new AudioSource[voiceCount];
                var filters = new AudioReverbFilter[voiceCount];
                if (Application.isPlaying)
                    UnityEngine.Object.DontDestroyOnLoad(host);
                for (var i = 0; i < voiceCount; i++)
                {
                    // A filter affects sources on its GameObject; each voice needs its own child.
                    var child = new GameObject("Voice " + i);
                    child.transform.SetParent(host.transform, false);
                    child.hideFlags = HideFlags.HideAndDontSave;
                    var source = child.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.loop = false;
                    source.spatialBlend = 0f;
                    voices[i] = source;
                    filters[i] = child.AddComponent<AudioReverbFilter>();
                    ApplyReverb(filters[i], SoundReverb.Off);
                }
                _host = host;
                _voices = voices;
                _filters = filters;
                _clips = Array.Empty<AudioClip>();
                _routes = new Route[4];
                _mix = new SoundMix(voiceCount);
                _defaultVolume = AddVolume(null, new SoundVolumeSettings(1f, 0, SoundReverb.Off));
            }
            catch
            {
                DestroyHost(host);
                throw;
            }
        }

        /// <summary>Append-only borrowed clip count; zero after Dispose.</summary>
        public int RegisteredCount => _count;
        /// <summary>Ungrouped route with initial gain 1, priority 0 and reverb Off.</summary>
        public SoundVolumeId DefaultVolume => _defaultVolume;

        /// <summary>
        /// Borrow a clip for this backend's lifetime and return a one-based backend-local handle.
        /// Another backend's handle can alias another clip and must not be supplied.
        /// </summary>
        public SoundHandle Register(AudioClip clip)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UnitySoundBackend));
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));
            if (_count == int.MaxValue)
                throw new InvalidOperationException("SoundHandle registration limit reached.");
            if (_count == _clips.Length)
            {
                var next = _count == 0 ? 4 : (int)Math.Min((long)_count * 2, int.MaxValue);
                Array.Resize(ref _clips, next);
            }
            _clips[_count++] = clip;
            return SoundHandle.FromRegisteredCount(_count);
        }

        /// <summary>Create an explicitly ungrouped, backend-local logical route, with no spatial region.</summary>
        public SoundVolumeId RegisterVolume(SoundVolumeSettings settings)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UnitySoundBackend));
            return AddVolume(null, settings);
        }

        /// <summary>
        /// Borrow the destination until Dispose; caller must not destroy/reassign it during that lifetime.
        /// Null/destroyed groups are rejected. External destruction fails closed at the next valid Tick or directed Play.
        /// </summary>
        public SoundVolumeId RegisterVolume(AudioMixerGroup group, SoundVolumeSettings settings)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UnitySoundBackend));
            if (group == null)
                throw new ArgumentNullException(nameof(group));
            return AddVolume(group, settings);
        }

        public bool TryGetSettings(SoundVolumeId volume, out SoundVolumeSettings settings)
            => _mix.TryGetSettings(volume, out settings);
        /// <summary>Current logical route gain, excluding caller-authored mixer attenuation.</summary>
        public bool TryGetAudibleGain(SoundVolumeId volume, out float gain)
            => _mix.TryGetAudibleGain(volume, out gain);

        public SoundVoiceId Play(SoundHandle handle, float gain)
        {
            _mix.TryGetSettings(_defaultVolume, out var settings);
            return Play(handle, _defaultVolume, gain, settings.Priority);
        }

        /// <summary>
        /// Admit by priority and clamp finite gain to [0,1]. Invalid input, destroyed native objects,
        /// priority denial and Dispose return Invalid without consuming an admission slot.
        /// </summary>
        public SoundVoiceId Play(SoundHandle handle, SoundVolumeId volume, float gain, int priority)
        {
            if (_disposed || _host == null || float.IsNaN(gain) || float.IsInfinity(gain) ||
                !SoundHandleIndex.TryGet(handle, _count, out var clipIndex) ||
                !SoundVolumeIndex.TryGet(volume, _mix.VolumeCount, out var routeIndex))
                return SoundVoiceId.Invalid;
            var clip = _clips[clipIndex];
            if (clip == null)
                return SoundVoiceId.Invalid;
            if (!RouteAlive(routeIndex))
            {
                ClearRoute(routeIndex);
                return SoundVoiceId.Invalid;
            }
            // Never collect !isPlaying before admission: headless Play may not begin native output.
            var slot = _mix.SelectSlot(volume, gain, priority);
            if (slot < 0 || _voices[slot] == null || _filters[slot] == null)
                return SoundVoiceId.Invalid;
            var voice = _mix.TryPlay(volume, gain, priority);
            ClearNative(slot); // Evicted clip/group/effect references end before rebinding.
            var source = _voices[slot];
            source.clip = clip;
            source.outputAudioMixerGroup = _routes[routeIndex].Group;
            source.volume = _mix.VoiceGain(slot) * _mix.RegionGain(routeIndex);
            _mix.TryGetSettings(volume, out var settings);
            ApplyReverb(_filters[slot], settings.Reverb);
            source.Play();
            return voice;
        }

        public void FadeVolume(SoundVolumeId volume, float targetGain, float seconds)
        {
            if (_disposed || !SoundMix.ValidFade(targetGain, seconds) ||
                !SoundVolumeIndex.TryGet(volume, _mix.VolumeCount, out _))
                return;
            _mix.FadeVolume(volume, targetGain, seconds);
            ApplyVoices(); // Instant fades reach native values before returning.
        }
        public void FadeVoice(SoundVoiceId voice, float targetGain, float seconds)
        {
            if (_disposed || !SoundMix.ValidFade(targetGain, seconds) || !_mix.TrySlot(voice, out _))
                return;
            _mix.FadeVoice(voice, targetGain, seconds);
            ApplyVoices();
        }
        public void SetReverb(SoundVolumeId volume, SoundReverb reverb)
        {
            if (_disposed || !SoundVolumeIndex.TryGet(volume, _mix.VolumeCount, out _))
                return;
            _mix.SetReverb(volume, reverb);
            ApplyVoices();
        }

        /// <summary>
        /// Advance injected fades and reconcile stopped/destroyed native voices and destinations.
        /// Negative/nonfinite delta is inert; zero permits cleanup without fade progress.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed || deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                return;
            _mix.Tick(deltaTime);
            for (var i = 0; i < _voices.Length; i++)
            {
                if (_mix.IsActive(i) && (_host == null || _voices[i] == null || _filters[i] == null ||
                    !RouteAlive(_mix.VolumeIndex(i)) || !_voices[i].isPlaying))
                    _mix.Release(i);
            }
            ApplyVoices();
        }

        /// <summary>
        /// Synchronously stop/clear native references and borrowed tables before destroying the owned host.
        /// Deferred PlayMode destruction does not delay the caller's permission to release asset handles.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            for (var i = 0; i < _voices.Length; i++)
            {
                _mix.Release(i);
                ClearNative(i);
            }
            Array.Clear(_clips, 0, _clips.Length);
            _clips = Array.Empty<AudioClip>();
            _count = 0;
            Array.Clear(_routes, 0, _routes.Length);
            DestroyHost(_host);
        }

        private SoundVolumeId AddVolume(AudioMixerGroup? group, SoundVolumeSettings settings)
        {
            if (_mix.VolumeCount == _routes.Length)
            {
                var next = (int)Math.Min((long)_routes.Length * 2, int.MaxValue);
                Array.Resize(ref _routes, next);
            }
            var id = _mix.Add(settings);
            _routes[id.Value - 1] = new Route { Group = group, Configured = group != null };
            return id;
        }
        private bool RouteAlive(int index)
            => !_routes[index].Configured || _routes[index].Group != null;

        private void ClearRoute(int index)
        {
            for (var i = 0; i < _voices.Length; i++)
            {
                if (_mix.IsActive(i) && _mix.VolumeIndex(i) == index)
                {
                    _mix.Release(i);
                    ClearNative(i);
                }
            }
        }
        private void ApplyVoices()
        {
            for (var i = 0; i < _voices.Length; i++)
            {
                if (_mix.IsActive(i) && (_host == null || _voices[i] == null || _filters[i] == null ||
                    !RouteAlive(_mix.VolumeIndex(i))))
                    _mix.Release(i);
                if (!_mix.IsActive(i))
                {
                    ClearNative(i);
                    continue;
                }
                var route = _mix.VolumeIndex(i);
                _voices[i].volume = _mix.VoiceGain(i) * _mix.RegionGain(route);
                _mix.TryGetSettings(SoundVolumeId.FromRegisteredCount(route + 1), out var settings);
                ApplyReverb(_filters[i], settings.Reverb);
            }
        }
        private void ClearNative(int slot)
        {
            var source = _voices[slot];
            if (source != null)
            {
                source.Stop();
                source.clip = null;
                source.outputAudioMixerGroup = null;
            }
            var filter = _filters[slot];
            if (filter != null)
                ApplyReverb(filter, SoundReverb.Off);
        }
        private static void ApplyReverb(AudioReverbFilter filter, SoundReverb reverb)
        {
            filter.enabled = false;
            // Presets overwrite numeric properties; choose User first, including disabled reset.
            filter.reverbPreset = AudioReverbPreset.User;
            filter.dryLevel = 0f;
            filter.room = 0f;
            filter.roomHF = 0f;
            filter.roomLF = 0f;
            filter.decayHFRatio = 0.5f;
            filter.reflectionsLevel = -10000f;
            filter.reflectionsDelay = 0f;
            filter.reverbDelay = 0.04f;
            filter.hfReference = 5000f;
            filter.lfReference = 250f;
            filter.density = 100f;
            filter.reverbLevel = reverb.Enabled ? reverb.ReverbLevelMillibels : -10000f;
            filter.decayTime = reverb.Enabled ? reverb.DecaySeconds : 1f;
            filter.diffusion = reverb.Enabled ? reverb.DiffusionPercent : 100f;
            filter.enabled = reverb.Enabled;
        }
        private static void DestroyHost(GameObject host)
        {
            if (host == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(host);
            else
                UnityEngine.Object.DestroyImmediate(host);
        }
        private struct Route
        {
            public AudioMixerGroup? Group;
            public bool Configured;
        }
    }
}
