#nullable enable

using System;
using Cysharp.Text;
using UnityEngine;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// セル identity（`Cell_{x}_{y}`）の判定・解析・整形を行う純 C# ユーティリティ。
    /// セルを画面遷移（SwitchScene / GoBack / TransitionPlan）に乗せないためのバリデータ
    /// （21-scene-streaming.md R-3 / D-5）の基盤でもある。
    /// </summary>
    public static class CellIdentity
    {
        /// <summary>セル identity のプレフィックス。</summary>
        public const string Prefix = "Cell_";

        /// <summary>
        /// identity が `Cell_{x}_{y}` 形式（x, y は非負整数）かどうかを判定する。
        /// </summary>
        public static bool IsCellId(string? identity) => TryParse(identity, out _);

        /// <summary>
        /// `Cell_{x}_{y}` 形式の identity からグリッド座標を解析する。
        /// </summary>
        public static bool TryParse(string? identity, out Vector2Int coordinate)
        {
            coordinate = default;

            if (string.IsNullOrEmpty(identity)
                || !identity!.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            var body = identity.AsSpan(Prefix.Length);
            var separator = body.IndexOf('_');
            if (separator < 0)
            {
                return false;
            }

            if (!TryParseNonNegativeInt(body.Slice(0, separator), out var x)
                || !TryParseNonNegativeInt(body.Slice(separator + 1), out var y))
            {
                return false;
            }

            coordinate = new Vector2Int(x, y);
            return true;
        }

        /// <summary>
        /// グリッド座標から `Cell_{x}_{y}` 形式の identity を生成する。
        /// </summary>
        public static string Format(int x, int y)
        {
            if (x < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "セル座標は非負整数のみ有効です。");
            }
            if (y < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "セル座標は非負整数のみ有効です。");
            }

            return ZString.Format("{0}{1}_{2}", Prefix, x, y);
        }

        /// <summary>
        /// 桁のみ（符号・空白・区切りなし）の非負整数を解析する。
        /// `Cell_-1_0` や `Cell_1_2_3` のような identity を弾くため、int.TryParse は使わない。
        /// </summary>
        private static bool TryParseNonNegativeInt(ReadOnlySpan<char> span, out int value)
        {
            value = 0;
            if (span.Length == 0)
            {
                return false;
            }

            long accumulated = 0;
            foreach (var c in span)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }

                accumulated = accumulated * 10 + (c - '0');
                if (accumulated > int.MaxValue)
                {
                    return false;
                }
            }

            value = (int)accumulated;
            return true;
        }
    }
}
