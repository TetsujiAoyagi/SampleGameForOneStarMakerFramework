#nullable enable

using System;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 再生箇所が具象バックエンドを見ずに鳴らすための転送。
    /// 無効な識別子もそのまま渡し、鳴らすかどうかはバックエンドが決める。転送自体は割り当てない。
    /// Tick は呼び出し側が進める。Update への登録はこの型では行わない。
    /// backend は借用するだけで所有・Dispose しない。Unity native 呼び出しはメインスレッド限定。
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

        public SoundVoiceId Play(SoundHandle handle, float gain)
        {
            return _backend.Play(handle, gain);
        }

        public SoundVoiceId Play(SoundHandle handle, SoundVolumeId volume, float gain, int priority)
        {
            return _backend.Play(handle, volume, gain, priority);
        }

        public void FadeVolume(SoundVolumeId volume, float targetGain, float seconds)
        {
            _backend.FadeVolume(volume, targetGain, seconds);
        }

        public void FadeVoice(SoundVoiceId voice, float targetGain, float seconds)
        {
            _backend.FadeVoice(voice, targetGain, seconds);
        }

        public void SetReverb(SoundVolumeId volume, SoundReverb reverb)
        {
            _backend.SetReverb(volume, reverb);
        }

        public void Tick(float deltaTime)
        {
            _backend.Tick(deltaTime);
        }
    }
}
