#nullable enable

using System;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// backend が返す 1 instance の識別値。0 は無効値で、作成失敗を表す。
    /// backend 内だけの値であり、別 backend の値を渡してはいけない。
    /// 破棄済みの値を backend が後で別の instance に使い回すことは許すが、
    /// 同時に生存する二つの instance に同じ値を返してはいけない（<see cref="IVfxBackend"/> の契約）。
    /// </summary>
    public readonly struct VfxInstanceId : IEquatable<VfxInstanceId>
    {
        public VfxInstanceId(ulong value)
        {
            Value = value;
        }

        public static VfxInstanceId Invalid => default;

        public ulong Value { get; }

        public bool IsValid => Value != 0;

        public bool Equals(VfxInstanceId other) => Value == other.Value;

        public override bool Equals(object? obj) => obj is VfxInstanceId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(VfxInstanceId left, VfxInstanceId right) => left.Equals(right);

        public static bool operator !=(VfxInstanceId left, VfxInstanceId right) => !left.Equals(right);
    }
}
