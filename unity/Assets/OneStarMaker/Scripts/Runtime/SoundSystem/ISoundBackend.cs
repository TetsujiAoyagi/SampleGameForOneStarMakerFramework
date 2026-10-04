#nullable enable

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 再生の差し替え口。Unity ネイティブ、CRI、Wwise などの具象型がこれを実装する。
    /// クリップ、キュー名、ミキサー型はここに置かない。領域の識別子と数値だけを渡す。
    /// Play、Fade、Tick は割り当てない。時間は Tick の引数で進む。
    /// </summary>
    public interface ISoundBackend
    {
        SoundVoiceId Play(SoundHandle handle, float gain);

        SoundVoiceId Play(SoundHandle handle, SoundVolumeId volume, float gain, int priority);

        void FadeVolume(SoundVolumeId volume, float targetGain, float seconds);

        void FadeVoice(SoundVoiceId voice, float targetGain, float seconds);

        void SetReverb(SoundVolumeId volume, SoundReverb reverb);

        void Tick(float deltaTime);
    }
}
