#nullable enable

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 任意の再生差し替え口。ミドルウェアへの互換性を保証するものではない。
    /// backend 内だけの登録値と論理ミックス経路を使う。Unity native 呼び出しはメインスレッド限定。
    /// 時間は呼び出し側の Tick で進む。UpdateSystemRuntime への登録とフレーム順序はその所有者が守る。
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
