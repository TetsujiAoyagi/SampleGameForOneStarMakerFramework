#nullable enable

using System;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// いま鳴っている一つの再生。スロットを再利用したら世代が進み、古い値は無音になる。
    /// 優先度で弾けた再生は Invalid を返す。
    /// backend 内だけの値であり、別 backend の値を渡してはいけない。
    /// </summary>
    public readonly struct SoundVoiceId : IEquatable<SoundVoiceId>
    {
        private readonly int _slotPlusOne;
        private readonly int _generation;

        private SoundVoiceId(int slotPlusOne, int generation)
        {
            _slotPlusOne = slotPlusOne;
            _generation = generation;
        }

        public static SoundVoiceId Invalid => default;

        public bool IsValid => _slotPlusOne > 0;

        internal int Slot => _slotPlusOne - 1;

        internal int Generation => _generation;

        internal static SoundVoiceId Create(int slot, int generation)
        {
            return new SoundVoiceId(slot + 1, generation);
        }

        public bool Equals(SoundVoiceId other)
        {
            return _slotPlusOne == other._slotPlusOne && _generation == other._generation;
        }

        public override bool Equals(object? obj)
        {
            return obj is SoundVoiceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (_slotPlusOne * 397) ^ _generation;
        }

        public static bool operator ==(SoundVoiceId left, SoundVoiceId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SoundVoiceId left, SoundVoiceId right)
        {
            return !left.Equals(right);
        }
    }
}
