#nullable enable

using System;

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// 1 アクションの公開値。レベルであり、押した瞬間のエッジではない。
    /// インデックスはマネージャ生存中の束ね順で、物理キーではない。
    /// </summary>
    public readonly struct InputActionValue : IEquatable<InputActionValue>
    {
        public InputActionValue(int index, InputControlMap map, InputActionValueKind kind, float x, float y)
        {
            Index = index;
            Map = map;
            Kind = kind;
            X = x;
            Y = y;
        }

        public int Index { get; }

        public InputControlMap Map { get; }

        public InputActionValueKind Kind { get; }

        /// <summary>Button では押下量、Axis2 では横成分。</summary>
        public float X { get; }

        /// <summary>Button では 0、Axis2 では縦成分。</summary>
        public float Y { get; }

        public static InputActionValue Neutral(int index, InputControlMap map, InputActionValueKind kind)
        {
            return new InputActionValue(index, map, kind, 0f, 0f);
        }

        public bool Equals(InputActionValue other)
        {
            return Index == other.Index
                   && Map == other.Map
                   && Kind == other.Kind
                   && X == other.X
                   && Y == other.Y;
        }

        public override bool Equals(object? obj)
        {
            return obj is InputActionValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Index, (int)Map, (int)Kind, X, Y);
        }
    }
}
