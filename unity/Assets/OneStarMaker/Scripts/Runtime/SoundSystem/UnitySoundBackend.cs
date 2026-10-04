#nullable enable

using System;
using UnityEngine;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// Unity の AudioSource を固定本数だけ使い回す再生口。
    /// クリップの登録だけが配列を伸ばし得る。再生はスロットを回して clip を差し替えるだけで、割り当てない。
    /// 同時再生数は生成時の本数で、超えたら最も古いスロットを止めて使う。優先度、フェード、SoundHolder は持たない。
    /// 入れ物の破棄は呼び出し側の Dispose が行う。破棄後の Play は無音で戻る。遅れた再生で例外オブジェクトを作らないため。
    /// </summary>
    public sealed class UnitySoundBackend : ISoundBackend, IDisposable
    {
        private readonly GameObject _host;
        private readonly AudioSource[] _voices;
        private AudioClip[] _clips;
        private int _count;
        private int _cursor;
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
        }

        public int RegisteredCount => _count;

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

        public void Play(SoundHandle handle, float volume)
        {
            if (_disposed || !SoundHandleIndex.TryGet(handle, _count, out var index))
            {
                return;
            }

            var clip = _clips[index];
            if (clip == null)
            {
                return;
            }

            var slot = SoundVoiceRing.Next(ref _cursor, _voices.Length);
            var source = _voices[slot];
            if (source == null)
            {
                return;
            }

            source.clip = clip;
            source.volume = volume;
            source.Play();
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
    }
}
