#nullable enable

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 再生の差し替え口。Unity ネイティブ、CRI、Wwise などの具象型がこれを実装する。
    /// クリップやキュー名はここに置かない。それらは具象型の登録引数で、再生のたびに渡さない。
    /// Play は割り当てない。
    /// </summary>
    public interface ISoundBackend
    {
        void Play(SoundHandle handle, float volume);
    }
}
