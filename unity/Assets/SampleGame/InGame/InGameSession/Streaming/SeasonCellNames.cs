#nullable enable

using System;
using System.Globalization;

namespace SampleGame.InGame.Streaming
{
    /// <summary>SampleGame の季節 identity 文法。格子や runtime 候補の所有者ではない。</summary>
    public static class SeasonCellNames
    {
        public static string Season(string season) => "Season_" + CheckSeason(season);
        public static string Lighting(string season) => CheckSeason(season) + "_Lighting";
        public static string Cell(string season, int x, int y) => Format(season, "Cell", x, y);
        public static string Environment(string season, int x, int y) => Format(season, "Environment", x, y);

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
            if (x < 0 || y < 0) throw new ArgumentOutOfRangeException(nameof(x));
            return CheckSeason(season) + "_" + role + "_"
                + x.ToString(CultureInfo.InvariantCulture) + "_" + y.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsSeason(string value)
            => value == "Spring" || value == "Summer" || value == "Autumn" || value == "Winter";

        private static string CheckSeason(string value)
            => IsSeason(value) ? value : throw new ArgumentException("Unknown season.", nameof(value));
    }
}
