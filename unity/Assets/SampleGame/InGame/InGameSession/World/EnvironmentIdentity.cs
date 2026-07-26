#nullable enable

using System;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// Cell 配下の職種シーン identity（萌芽: <c>Environment_{x}_{y}</c>）の判定・整形。
    /// 距離ストリーミング境界は常に <see cref="CellIdentity"/>。こちらは作業分割用の子シーン名。
    /// </summary>
    public static class EnvironmentIdentity
    {
        /// <summary>Environment 子シーン identity のプレフィックス。</summary>
        public const string Prefix = "Environment_";

        /// <summary>identity が <c>Environment_{x}_{y}</c> 形式か。</summary>
        public static bool IsEnvironmentId(string? identity) => TryParse(identity, out _);

        /// <summary>
        /// <c>Environment_{x}_{y}</c> からグリッド座標を解析する。
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

        /// <summary>グリッド座標から <c>Environment_{x}_{y}</c> を生成する。</summary>
        public static string Format(int x, int y)
        {
            if (x < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "座標は非負整数のみ有効です。");
            }

            if (y < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(y), y, "座標は非負整数のみ有効です。");
            }

            return $"{Prefix}{x}_{y}";
        }

        /// <summary>
        /// 親 Cell identity から対応する Environment identity を得る。
        /// Cell 以外が渡されたら false。
        /// </summary>
        public static bool TryFromCellId(string? cellIdentity, out string environmentIdentity)
        {
            environmentIdentity = string.Empty;
            if (!CellIdentity.TryParse(cellIdentity, out var coordinate))
            {
                return false;
            }

            environmentIdentity = Format(coordinate.x, coordinate.y);
            return true;
        }

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
