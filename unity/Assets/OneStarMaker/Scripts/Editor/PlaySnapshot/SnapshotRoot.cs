#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OneStarMaker.Editor.PlaySnapshot
{
    /// <summary>
    /// Play snapshot の置き場（<c>Assets/OsmPlaySnapshot/</c>）の folder の名前、生死の判定、回収。
    /// <para>
    /// folder 名は <c>&lt;pid&gt;_&lt;process の開始時刻の UTC ticks&gt;_&lt;乱数&gt;</c>。記録 file を持たないのは、folder を作った直後の異常終了で
    /// 記録の無い孤児が残るため。生死は folder 名の pid と開始時刻だけで決める（pid が再利用されても開始時刻が違えば死んでいる）。
    /// 置き場は project の中にあり、同じ project を 2 つの Unity process で開くことは Unity の project lock が止めるので、
    /// 他の生きている process の folder は「同じ project を開いた別の Editor」ではなく、判定の安全側として残すだけである。
    /// </para>
    /// </summary>
    internal static class SnapshotRoot
    {
        internal const string AssetPath = "Assets/OsmPlaySnapshot";

        internal enum Verdict { Keep, Collect, Malformed }

        /// <summary>この process の folder 名を作る。乱数は同じ process の中の Play ごとに folder を分けるため。</summary>
        internal static string NewFolderName(int pid, long processStartUtcTicks, string nonce) =>
            pid.ToString(CultureInfo.InvariantCulture) + "_" + processStartUtcTicks.ToString(CultureInfo.InvariantCulture) + "_" + nonce;

        internal static bool TryParse(string folderName, out int pid, out long processStartUtcTicks)
        {
            pid = 0;
            processStartUtcTicks = 0;
            var parts = folderName.Split('_');
            return parts.Length == 3
                   && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out pid) && pid > 0
                   && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out processStartUtcTicks)
                   && parts[2].Length != 0 && parts[2].All(Uri.IsHexDigit);
        }

        /// <summary>
        /// 1 つの folder を回収するか。<paramref name="liveProcessStarts"/> は生きている process の pid と開始時刻（読めた物だけ）。
        /// 現在の process の folder は、使用中の 1 つ（<paramref name="activeFolderName"/>）以外を回収する（前の Play の解除で消せなかった物）。
        /// </summary>
        internal static Verdict Classify(string folderName, IReadOnlyDictionary<int, long> liveProcessStarts, int currentPid, string? activeFolderName)
        {
            if (!TryParse(folderName, out var pid, out var started)) return Verdict.Malformed;
            if (string.Equals(folderName, activeFolderName, StringComparison.Ordinal)) return Verdict.Keep;
            if (pid == currentPid) return Verdict.Collect;
            return liveProcessStarts.TryGetValue(pid, out var liveStarted) && liveStarted == started ? Verdict.Keep : Verdict.Collect;
        }

        /// <summary>回収の結果。<see cref="Remaining"/> は消せずに残った path（次の回収で再び試し、今回の Play には使わない）。</summary>
        internal sealed class CollectResult
        {
            internal List<string> Collected { get; } = new();
            internal List<string> Remaining { get; } = new();
            internal List<string> Malformed { get; } = new();
        }

        /// <summary>
        /// <paramref name="rootFullPath"/> の直下の folder と、その <c>.meta</c> を判定して回収する。名前の形が違う物は触らず
        /// <see cref="CollectResult.Malformed"/> に返す。最後に置き場が空なら置き場自身と <c>.meta</c> も消す。
        /// </summary>
        internal static CollectResult Collect(string rootFullPath, Func<int, long?> processStartOf, int currentPid, string? activeFolderName)
        {
            var result = new CollectResult();
            if (!Directory.Exists(rootFullPath))
            {
                DeleteFileIfExists(rootFullPath + ".meta", result);
                return result;
            }

            var folders = Directory.GetDirectories(rootFullPath).Select(Path.GetFileName).Where(x => x != null).Cast<string>().ToArray();
            var metaOnly = Directory.GetFiles(rootFullPath, "*.meta").Select(Path.GetFileNameWithoutExtension).Where(x => x != null).Cast<string>()
                .Where(name => !folders.Contains(name, StringComparer.Ordinal)).ToArray();
            var otherFiles = Directory.GetFiles(rootFullPath).Where(x => !x.EndsWith(".meta", StringComparison.Ordinal)).ToArray();
            result.Malformed.AddRange(otherFiles);

            var live = new Dictionary<int, long>();
            foreach (var name in folders.Concat(metaOnly))
            {
                if (!TryParse(name, out var pid, out _) || pid == currentPid || live.ContainsKey(pid)) continue;
                var started = processStartOf(pid);
                if (started.HasValue) live[pid] = started.Value;
            }

            foreach (var name in folders)
            {
                var path = Path.Combine(rootFullPath, name);
                switch (Classify(name, live, currentPid, activeFolderName))
                {
                    case Verdict.Malformed:
                        result.Malformed.Add(path);
                        break;
                    case Verdict.Collect:
                        DeleteDirectoryIfExists(path, result);
                        DeleteFileIfExists(path + ".meta", result);
                        break;
                }
            }
            foreach (var name in metaOnly)
            {
                var path = Path.Combine(rootFullPath, name + ".meta");
                if (Classify(name, live, currentPid, activeFolderName) == Verdict.Collect) DeleteFileIfExists(path, result);
                else if (!TryParse(name, out _, out _)) result.Malformed.Add(path);
            }

            if (!Directory.EnumerateFileSystemEntries(rootFullPath).Any())
            {
                DeleteDirectoryIfExists(rootFullPath, result);
                DeleteFileIfExists(rootFullPath + ".meta", result);
            }
            return result;
        }

        /// <summary>生きている process の開始時刻（UTC ticks）。死んでいる・読めない時は null（null は回収の側に倒れる）。</summary>
        internal static long? ProcessStartOf(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return process.HasExited ? null : process.StartTime.ToUniversalTime().Ticks;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                return null;
            }
        }

        internal static long CurrentProcessStartUtcTicks()
        {
            using var process = Process.GetCurrentProcess();
            return process.StartTime.ToUniversalTime().Ticks;
        }

        private static void DeleteDirectoryIfExists(string path, CollectResult result)
        {
            if (!Directory.Exists(path)) return;
            try
            {
                Directory.Delete(path, recursive: true);
                result.Collected.Add(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Remaining.Add(path);
            }
        }

        private static void DeleteFileIfExists(string path, CollectResult result)
        {
            if (!File.Exists(path)) return;
            try
            {
                File.Delete(path);
                result.Collected.Add(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Remaining.Add(path);
            }
        }
    }
}
