#nullable enable

using System;

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// 束ね時点で決まるアクションの席。名前は検索用で、毎フレームはインデックスを使う。
    /// </summary>
    internal readonly struct InputActionSlot
    {
        public InputActionSlot(int index, InputControlMap map, InputActionValueKind kind, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("アクション名が空です。", nameof(name));
            }

            Index = index;
            Map = map;
            Kind = kind;
            Name = name;
        }

        public int Index { get; }

        public InputControlMap Map { get; }

        public InputActionValueKind Kind { get; }

        public string Name { get; }
    }
}
