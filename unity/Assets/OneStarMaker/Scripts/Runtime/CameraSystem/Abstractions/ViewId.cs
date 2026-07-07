#nullable enable

using System;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Abstractions
{
    /// <summary>
    /// View を識別する不透明な値型。内部採番の整数を包み、Backend / Host の辞書キーとして使う。
    /// 値の意味は System 内部にのみ属し、外部は等値比較のみを行う。
    /// </summary>
    public readonly struct ViewId : IEquatable<ViewId>
    {
        internal ViewId(int value) => Value = value;

        internal int Value { get; }

        public bool Equals(ViewId other) => Value == other.Value;

        public override bool Equals(object? obj) => obj is ViewId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(ViewId left, ViewId right) => left.Equals(right);

        public static bool operator !=(ViewId left, ViewId right) => !left.Equals(right);
    }
}
