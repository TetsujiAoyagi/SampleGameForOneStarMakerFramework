#nullable enable

using System;
using UnityEngine;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 固定本数の AudioSource を再生要求順に使い回す、任意の Unity ネイティブ再生口。
    /// 生成、登録、再生、破棄はすべて Unity メインスレッドで行う。
    /// 登録だけが配列を伸ばし得る。Play は割り当てず、同時再生数を超えると最も古いスロットを置き換える。
    /// </summary>
    /// <remarks>
    /// 作成者がバックエンドと host を所有する。AudioClip は借用し、ロード・解放・破棄しない。
    /// 呼び出し側は IAssetManagement と AssetOwner.Manual で取得した全登録クリップを
    /// このバックエンドの寿命中保持し、Dispose の後で IAssetHandle&lt;AudioClip&gt; を解放する。
    /// AssetOwner.App は Dispose を ReleaseAll より先に行う終了順序が明示されている場合だけ使用できる。
    /// Scene(id) / Bind(go) の自動失効する所有者は使えない。個々の登録を解除する機能はない。
    /// </remarks>
    public sealed class UnitySoundBackend : ISoundBackend, IDisposable
    {
        private readonly GameObject _host;
        private readonly AudioSource[] _voices;
        private AudioClip[] _clips;
        private int _count;
        private int _cursor;
        private bool _disposed;

        /// <summary>メインスレッドで、所有する host と指定本数の非空間 AudioSource を作る。</summary>
        public UnitySoundBackend(int voiceCount)
        {
            if (voiceCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(voiceCount), "同時再生数は 1 以上です。");
            }

            var host = new GameObject("OSM Sound");
            try
            {
                host.hideFlags = HideFlags.HideAndDontSave;
                var voices = new AudioSource[voiceCount];
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

                _host = host;
                _voices = voices;
                _clips = Array.Empty<AudioClip>();
            }
            catch
            {
                DestroyHost(host);
                throw;
            }
        }

        /// <summary>このバックエンド内の登録数。Dispose 後は 0。</summary>
        public int RegisteredCount => _count;

        /// <summary>
        /// メインスレッドでクリップを寿命全体にわたって借用し、このバックエンド内だけの 1 始まりの値を返す。
        /// 別バックエンドのハンドルは同じ値の別クリップを指し得るため、渡してはいけない。
        /// </summary>
        /// <exception cref="ObjectDisposedException">Dispose 済み。</exception>
        /// <exception cref="ArgumentNullException">クリップが null または Unity で破棄済み。</exception>
        /// <exception cref="InvalidOperationException">正のハンドル値をこれ以上登録できない。</exception>
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

            // 登録値は正の int。折り返して既存ハンドルを再利用する前に拒否する。
            if (_count == int.MaxValue)
            {
                throw new InvalidOperationException("登録数が SoundHandle の上限に達しました。");
            }

            if (_count == _clips.Length)
            {
                var next = _count == 0 ? 4 : (int)Math.Min((long)_count * 2, int.MaxValue);
                Array.Resize(ref _clips, next);
            }

            _clips[_count] = clip;
            _count++;
            return SoundHandle.FromRegisteredCount(_count);
        }

        /// <summary>
        /// メインスレッドで有限の線形音量を [0,1] に制限して再生する。可聴出力は保証しない。
        /// 非有限音量、無効・未登録ハンドル、破棄済みクリップ・host・source、Dispose 後は何もしない。
        /// </summary>
        public void Play(SoundHandle handle, float volume)
        {
            // 不正な要求が現在の音を止めたり、次のスロットを消費したりしてはいけない。
            if (_disposed || float.IsNaN(volume) || float.IsInfinity(volume) ||
                _host == null || !SoundHandleIndex.TryGet(handle, _count, out var index))
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

            // 前の native 再生と参照を切ってから次の借用クリップを結び直す。
            source.Stop();
            source.clip = null;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.Play();
        }

        /// <summary>
        /// メインスレッドで全 source を同期停止し、clip 参照と登録表を消してから所有 host を破棄する。
        /// PlayMode の Destroy が遅延しても、この呼び出しが戻れば呼び出し側はアセットハンドルを解放できる。
        /// 繰り返し呼び出しても何もしない。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var source in _voices)
            {
                if (source != null)
                {
                    source.Stop();
                    source.clip = null;
                }
            }

            // host の遅延破棄に借用クリップの解放順序を依存させない。
            Array.Clear(_clips, 0, _clips.Length);
            _clips = Array.Empty<AudioClip>();
            _count = 0;
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
