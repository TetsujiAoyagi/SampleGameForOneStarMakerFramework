#nullable enable

using System;

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// ミックス上の領域。音量、優先度の既定、フェード、リバーブの設定をここに置く。
    /// ワールド座標の当たり領域ではない。リスナーの出入りで切り替える領域は後続で扱う。
    /// 0 は無効。正の値は登録順の 1 始まり。
    /// </summary>
    public readonly struct SoundVolumeId : IEquatable<SoundVolumeId>
    {
        private readonly int _value;

        private SoundVolumeId(int value)
        {
            _value = value;
        }

        public static SoundVolumeId Invalid => default;

        public bool IsValid => _value > 0;

        internal int Value => _value;

        internal static SoundVolumeId FromRegisteredCount(int registeredCount)
        {
            return new SoundVolumeId(registeredCount);
        }

        public bool Equals(SoundVolumeId other)
        {
            return _value == other._value;
        }

        public override bool Equals(object? obj)
        {
            return obj is SoundVolumeId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value;
        }

        public static bool operator ==(SoundVolumeId left, SoundVolumeId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SoundVolumeId left, SoundVolumeId right)
        {
            return !left.Equals(right);
        }
    }

    internal static class SoundVolumeIndex
    {
        public static bool TryGet(SoundVolumeId volume, int registeredCount, out int index)
        {
            var candidate = volume.Value - 1;
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
