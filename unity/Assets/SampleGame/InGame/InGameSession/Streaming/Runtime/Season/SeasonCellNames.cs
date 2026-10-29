#nullable enable

using System;
using System.Globalization;

namespace SampleGame.InGame.Streaming
{
    /// <summary>SampleGame の季節 identity 文法。格子や runtime 候補の所有者ではない。</summary>
    public static class SeasonCellNames
    {
        private const string EventInfix = "_Event_";

        public static string Season(string season) => "Season_" + CheckSeason(season);
        public static string Lighting(string season) => CheckSeason(season) + "_Lighting";
        public static string Cell(string season, int x, int y) => Format(season, "Cell", x, y);
        public static string Environment(string season, int x, int y) => Format(season, "Environment", x, y);

        /// <summary>Season 直下の major Event（独立 Event Scene）の identity。<c>{Season}_Event_{slug}</c>。</summary>
        public static string MajorEvent(string season, string slug)
            => string.IsNullOrEmpty(slug)
                ? throw new ArgumentException("Slug is empty.", nameof(slug))
                : CheckSeason(season) + EventInfix + slug;

        /// <summary>名前だけの判定。親・LoadType の構造は呼び出し側が SceneResource で見る。</summary>
        public static bool TryParseMajorEvent(string identity, out string season, out string slug)
        {
            season = slug = string.Empty;
            var index = identity == null ? -1 : identity.IndexOf(EventInfix, StringComparison.Ordinal);
            if (index <= 0 || index + EventInfix.Length >= identity!.Length || !IsSeason(identity.Substring(0, index))) return false;
            season = identity.Substring(0, index);
            slug = identity.Substring(index + EventInfix.Length);
            return true;
        }

        public static bool TryParseCell(string identity, out string season, out int x, out int y)
        {
            season = string.Empty;
            x = y = 0;
            if (identity == null) return false;
            var parts = identity.Split('_');
            if (parts.Length != 4 || !IsSeason(parts[0]) || parts[1] != "Cell"
                || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out x)
                || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out y)
                || identity != Cell(parts[0], x, y)) return false;
            season = parts[0];
            return true;
        }

        private static string Format(string season, string role, int x, int y)
        {
            if (x < 0) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0) throw new ArgumentOutOfRangeException(nameof(y));
            return CheckSeason(season) + "_" + role + "_"
                + x.ToString(CultureInfo.InvariantCulture) + "_" + y.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsSeason(string value)
            => value == "Spring" || value == "Summer" || value == "Autumn" || value == "Winter";

        private static string CheckSeason(string value)
            => IsSeason(value) ? value : throw new ArgumentException("Unknown season.", nameof(value));
    }
}
