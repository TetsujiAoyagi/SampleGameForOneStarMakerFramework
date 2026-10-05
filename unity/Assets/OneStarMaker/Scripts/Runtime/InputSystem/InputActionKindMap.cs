#nullable enable

using System;

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// Unity の expectedControlType 文字列を公開形へ写す。
    /// 一致は序数比較である。Vector3 と Quaternion は写さず、呼び出し側が件数だけ数える。
    /// </summary>
    internal static class InputActionKindMap
    {
        internal const string ButtonControl = "Button";

        internal const string Axis2Control = "Vector2";

        public static bool TryGetKind(string? expectedControlType, out InputActionValueKind kind)
        {
            if (string.Equals(expectedControlType, ButtonControl, StringComparison.Ordinal))
            {
                kind = InputActionValueKind.Button;
                return true;
            }

            if (string.Equals(expectedControlType, Axis2Control, StringComparison.Ordinal))
            {
                kind = InputActionValueKind.Axis2;
                return true;
            }

            kind = default;
            return false;
        }
    }
}
