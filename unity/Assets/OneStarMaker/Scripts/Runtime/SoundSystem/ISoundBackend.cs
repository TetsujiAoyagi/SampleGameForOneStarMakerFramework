#nullable enable

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 任意の再生差し替え口。クリップやキューの登録と寿命管理は具象バックエンドとその所有者が行う。
    /// このインターフェースを提供するだけでは、他のミドルウェアでの実装成立は保証しない。
    /// </summary>
    public interface ISoundBackend
    {
        /// <summary>
        /// 登録したバックエンドのハンドルと線形音量を渡す、割り当てのない再生要求。
        /// native バックエンドは Unity メインスレッドから呼ぶ。実際の可聴出力は保証しない。
        /// </summary>
        void Play(SoundHandle handle, float volume);
    }
}
