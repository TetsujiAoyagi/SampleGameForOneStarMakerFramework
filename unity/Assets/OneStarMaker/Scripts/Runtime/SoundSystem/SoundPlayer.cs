#nullable enable

using System;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 再生箇所が具象バックエンドを見ずに鳴らすための転送。
    /// 無効なハンドルをここで落とさず、落とすかどうかはバックエンドが決める。転送自体は割り当てない。
    /// </summary>
    public sealed class SoundPlayer
    {
        private readonly ISoundBackend _backend;

        public SoundPlayer(ISoundBackend backend)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            _backend = backend;
        }

        public void Play(SoundHandle handle, float volume)
        {
            _backend.Play(handle, volume);
        }
    }
}
