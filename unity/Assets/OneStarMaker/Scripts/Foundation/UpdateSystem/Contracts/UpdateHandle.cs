using System;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// UpdateSystem 内で state / mirror / structural request を結び付ける安定ハンドル。
    /// dense index をそのまま外へ漏らすと compaction 時に参照が壊れるため、
    /// 外部公開値は「slot + generation」に限定し、再利用時の ABA 問題を避ける。
    /// </summary>
    public readonly struct UpdateHandle : IEquatable<UpdateHandle>
    {
        public static readonly UpdateHandle Invalid = default;

        public UpdateHandle(int slot, uint generation)
        {
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            if (generation == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }

            Slot = slot;
            Generation = generation;
        }

        public int Slot { get; }

        public uint Generation { get; }

        public bool IsValid => Generation != 0;

        public bool Equals(UpdateHandle other)
        {
            return Slot == other.Slot && Generation == other.Generation;
        }

        public override bool Equals(object? obj)
        {
            return obj is UpdateHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Slot, Generation);
        }

        public override string ToString()
        {
            return IsValid ? $"{Slot}:{Generation}" : "invalid";
        }

        public static bool operator ==(UpdateHandle left, UpdateHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(UpdateHandle left, UpdateHandle right)
        {
            return !left.Equals(right);
        }
    }
}
