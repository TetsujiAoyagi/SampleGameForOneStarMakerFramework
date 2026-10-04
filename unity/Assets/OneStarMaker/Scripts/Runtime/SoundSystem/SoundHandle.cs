#nullable enable

using System;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 登録済みの音を指す値。再生の引数はこれと音量だけで、文字列もゲーム固有の列挙型も受け取らない。
    /// バックエンドが登録時に返し、ゲームはその値を保持する。列挙型を使うならゲーム側の表の添字であり、この型へは変換しない。
    /// 0 は未登録。正の値は構造上有効で、実際の登録の存在やクリップの寿命を保証しない。
    /// 値は登録したバックエンド内の 1 始まりの順序。別バックエンドへ渡すと別クリップを指し得る。
    /// </summary>
    public readonly struct SoundHandle : IEquatable<SoundHandle>
    {
        private readonly int _value;

        private SoundHandle(int value)
        {
            _value = value;
        }

        public static SoundHandle Invalid => default;

        /// <summary>値が正かどうかだけを返す。登録や backend の生存確認ではない。</summary>
        public bool IsValid => _value > 0;

        internal int Value => _value;

        internal static SoundHandle FromRegisteredCount(int registeredCount)
        {
            return new SoundHandle(registeredCount);
        }

        public bool Equals(SoundHandle other)
        {
            return _value == other._value;
        }

        public override bool Equals(object? obj)
        {
            return obj is SoundHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value;
        }

        public static bool operator ==(SoundHandle left, SoundHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SoundHandle left, SoundHandle right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// ハンドルを登録配列の添字へ直す。無効や範囲外は再生側で無音にするため、ここでは例外を作らない。
    /// </summary>
    internal static class SoundHandleIndex
    {
        public static bool TryGet(SoundHandle handle, int registeredCount, out int index)
        {
            var candidate = handle.Value - 1;
            if ((uint)candidate >= (uint)registeredCount)
            {
                index = -1;
                return false;
            }

            index = candidate;
            return true;
        }
    }
}
