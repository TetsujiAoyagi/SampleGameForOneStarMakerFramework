#nullable enable

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OneStarMaker.Foundation.Logging
{
    /// <summary>生成と清掃が共有する管理名。広い拡張子一致で他のファイルを巻き込まない。</summary>
    internal static class LogFileNames
    {
        private static readonly Regex ManagedName = new Regex(
            @"\A(?<notice>logging-errors_)?(?<date>[0-9]{4}-[0-9]{2}-[0-9]{2})_(?<sequence>[0-9]{3,})\.(?<extension>log|txt)\z",
            RegexOptions.CultureInvariant);

        internal static string Normal(DateTime localDate, int sequence)
            => localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "_" + sequence.ToString("000", CultureInfo.InvariantCulture) + ".log";

        internal static string Notice(DateTime localDate, long sequence)
            => "logging-errors_" + localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "_" + sequence.ToString("000", CultureInfo.InvariantCulture) + ".txt";

        internal static bool TryParse(string name, out DateTime date, out long sequence, out bool notice)
        {
            date = default;
            sequence = 0;
            notice = false;
            var match = ManagedName.Match(name);
            if (!match.Success) return false;
            notice = match.Groups["notice"].Success;
            // 実在する日付と整数を要求する。999 の次の 1000 も同じ管理対象。
            return match.Groups["extension"].Value == (notice ? "txt" : "log")
                && DateTime.TryParseExact(match.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date)
                && long.TryParse(match.Groups["sequence"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out sequence);
        }
    }
}
