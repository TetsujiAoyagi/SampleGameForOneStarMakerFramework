#nullable enable

using System;

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// 名前とマップの組。文字列連結を避け、検索時に新しいキー文字列を作らない。
    /// </summary>
    internal readonly struct InputActionNameKey : IEquatable<InputActionNameKey>
    {
        public InputActionNameKey(string name, InputControlMap map)
        {
            Name = name;
            Map = map;
        }

        public string Name { get; }

        public InputControlMap Map { get; }

        public bool Equals(InputActionNameKey other)
        {
            return Map == other.Map && string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is InputActionNameKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.Ordinal.GetHashCode(Name), (int)Map);
        }
    }
}
