#nullable enable

using System;

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// 公開バッファの書き込み。割り当てをしない。
    /// 非 Stable の値は直前の Stable サンプルを残さず、席の識別子だけを保って零にする。
    /// </summary>
    internal static class InputPublication
    {
        public static void WriteNeutral(ReadOnlySpan<InputActionSlot> slots, Span<InputActionValue> target)
        {
            var count = slots.Length < target.Length ? slots.Length : target.Length;
            for (var i = 0; i < count; i++)
            {
                var slot = slots[i];
                target[i] = InputActionValue.Neutral(slot.Index, slot.Map, slot.Kind);
            }
        }

        /// <summary>
        /// 有効マップ以外を零にし、有効マップの成分は残して席の識別子をカタログ側で上書きする。
        /// リーダーが無効マップへ書いた値や、別マップ時代の残りを公開しない。
        /// </summary>
        public static void KeepActiveMap(
            InputControlMap active,
            ReadOnlySpan<InputActionSlot> slots,
            Span<InputActionValue> sampled)
        {
            var count = slots.Length < sampled.Length ? slots.Length : sampled.Length;
            for (var i = 0; i < count; i++)
            {
                var slot = slots[i];
                if (slot.Map != active)
                {
                    sampled[i] = InputActionValue.Neutral(slot.Index, slot.Map, slot.Kind);
                    continue;
                }

                var read = sampled[i];
                sampled[i] = new InputActionValue(slot.Index, slot.Map, slot.Kind, read.X, read.Y);
            }
        }

        public static bool TryCopy(ReadOnlySpan<InputActionValue> source, Span<InputActionValue> destination)
        {
            if (source.Length != destination.Length)
            {
                return false;
            }

            source.CopyTo(destination);
            return true;
        }
    }
}
