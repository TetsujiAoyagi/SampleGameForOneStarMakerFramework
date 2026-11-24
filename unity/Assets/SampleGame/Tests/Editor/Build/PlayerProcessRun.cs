#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OneStarMaker.Foundation.Config;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// 起動した Player の 1 回の観測。待ちは全て exit の signal の刻みで、刻みごとに Player の log を読み直す（sleep を使わない。
    /// 追記中の file の変更通知は遅れうるので FileSystemWatcher を起こし手にしない）。process は呼び手が作って渡し、寿命も呼び手が持つ。
    /// 2 つの判定の test（Player の種類、上書きの起動）が使う。観測した時刻と結果はこの object に残し、呼び手が自分の record に写す。
    /// </summary>
    internal sealed class PlayerProcessRun
    {
        internal const int PollMs = 500;
        internal const string QuittingLine = "[AppInit] Application quitting.";

        private readonly Process _process;
        private readonly string _logPath;
        private readonly Action? _onTick;

        /// <param name="onTick">刻みごとに呼ぶ（上書きの判定の test が、同期の待ちの中で入口の観測の口を呼ぶため）。</param>
        internal PlayerProcessRun(Process process, string logPath, string caseDirectory, Action? onTick = null)
        { _process = process; _logPath = logPath; CaseDirectory = caseDirectory; _onTick = onTick; }

        internal Process Process => _process;
        internal string CaseDirectory { get; }
        internal string LogText { get; private set; } = string.Empty;
        internal string FirstSceneLineUtc { get; private set; } = string.Empty;
        internal string QuitObservedUtc { get; private set; } = string.Empty;
        internal string CloseRequestedUtc { get; private set; } = string.Empty;
        internal bool CloseMessageSent { get; private set; }
        internal bool Timeout { get; private set; }
        internal string TimeoutPhase { get; private set; } = string.Empty;

        /// <summary>行が出るまで待つ。出なければ（先に exit した、制限時間を超えた）偽。</summary>
        internal bool WaitForLine(string line, TimeSpan limit, string phase)
        {
            var clock = Stopwatch.StartNew();
            while (true)
            {
                var exited = Tick();
                if (LogText.Contains(line))
                {
                    if (phase == "first-scene") FirstSceneLineUtc = Utc(DateTime.UtcNow);
                    return true;
                }
                if (exited) return false;
                if (clock.Elapsed > limit) { MarkTimeout(phase); return false; }
            }
        }

        /// <summary>exit を待つ。制限時間を超えたら timeout を記録して偽（kill は呼び出し元の finally）。</summary>
        internal bool WaitForExit(TimeSpan limit, string phase)
        {
            var clock = Stopwatch.StartNew();
            while (true)
            {
                if (Tick()) return true;
                if (clock.Elapsed > limit) { MarkTimeout(phase); return false; }
            }
        }

        /// <summary>exit を見ないまま時間が過ぎたら真。</summary>
        internal bool Survives(TimeSpan duration)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < duration)
                if (Tick()) return false;
            return true;
        }

        /// <summary>条件が真になるまで刻みで待つ（game の log の行など）。exit したか制限時間を超えたら偽。</summary>
        internal bool WaitUntil(Func<bool> condition, TimeSpan limit, string phase)
        {
            var clock = Stopwatch.StartNew();
            while (true)
            {
                var exited = Tick();
                if (condition()) return true;
                if (exited) return false;
                if (clock.Elapsed > limit) { MarkTimeout(phase); return false; }
            }
        }

        /// <summary>
        /// 主窓へ WM_CLOSE を post する（窓の × と同じ要求）。.NET の Process.CloseMainWindow の定義の動作だが、
        /// test が動く Unity 同梱の Mono の CloseMainWindow は TerminateProcess(handle, -2) で強制終了するので使わない。
        /// 主窓が無い（handle 0）か post に失敗した時は <see cref="CloseMessageSent"/> を偽として残す。
        /// </summary>
        internal void RequestClose()
        {
            _process.Refresh();
            var window = _process.MainWindowHandle;
            CloseRequestedUtc = Utc(DateTime.UtcNow);
            CloseMessageSent = window != IntPtr.Zero && PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
        }

        private const uint WmClose = 0x0010;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        internal void ReadLog()
        {
            if (!File.Exists(_logPath)) return;
            using var stream = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            LogText = reader.ReadToEnd();
            if (QuitObservedUtc.Length == 0 && LogText.Contains(QuittingLine)) QuitObservedUtc = Utc(DateTime.UtcNow);
        }

        private bool Tick()
        {
            var exited = _process.WaitForExit(PollMs);
            ReadLog();
            _onTick?.Invoke();
            return exited;
        }

        private void MarkTimeout(string phase)
        {
            Timeout = true;
            TimeoutPhase = phase;
        }

        internal static string Utc(DateTime value) => value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

        // ─── game の log（ZLogger の JSON） ───

        // record の時刻は Utc の "o"（末尾 Z）、game の log の Timestamp は +00:00 などの offset 付きで書かれる。
        // 両方を UTC の DateTime に直して比べるため AdjustToUniversal を使い、offset の無い文字列は UTC とみなす。
        // RoundtripKind は AdjustToUniversal・AssumeUniversal と組めず、.NET が ArgumentException で拒むので混ぜない。
        private const DateTimeStyles UtcTimestampStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

        /// <summary>
        /// game の log の folder の Error 以上の行と、<paramref name="probes"/> を含む行。JSON として読めない行は <c>&lt;unparsed&gt;</c> として残す。
        /// <paramref name="quitObservedUtc"/> があれば各行が終わりの要求より前かを付ける。
        /// </summary>
        internal static PlayerGameLogLine[] ReadGameLog(string directory, string quitObservedUtc, IReadOnlyList<string> probes)
        {
            if (!Directory.Exists(directory)) return Array.Empty<PlayerGameLogLine>();
            DateTime? quit = quitObservedUtc.Length == 0 ? null
                : DateTime.Parse(quitObservedUtc, CultureInfo.InvariantCulture, UtcTimestampStyles);
            var lines = new List<PlayerGameLogLine>();
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                foreach (var raw in File.ReadLines(file))
                {
                    if (raw.Length == 0 || raw[0] != '{') continue;
                    var values = new Dictionary<string, string>(StringComparer.Ordinal);
                    try { JsonConfigFlattener.Flatten(raw, values); }
                    catch (Exception) { lines.Add(new PlayerGameLogLine { file = Path.GetFileName(file), message = "<unparsed> " + raw }); continue; }
                    var level = Value(values, "LogLevel");
                    var message = Value(values, "Message");
                    var isError = level == "Error" || level == "Critical";
                    if (!isError && !probes.Any(probe => message.Contains(probe))) continue;
                    var timestamp = Value(values, "Timestamp");
                    var beforeQuit = true;
                    if (quit.HasValue && DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, UtcTimestampStyles, out var at))
                        beforeQuit = at < quit.Value;
                    lines.Add(new PlayerGameLogLine
                    {
                        file = Path.GetFileName(file), timestamp = timestamp, level = level, category = Value(values, "Category"),
                        message = message, exception = Value(values, "Exception:Message"), beforeQuit = beforeQuit,
                    });
                }
            }
            return lines.ToArray();
        }

        private static string Value(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out var value) ? value : string.Empty;
    }

    [Serializable]
    internal sealed class PlayerGameLogLine
    {
        public string file = "", timestamp = "", level = "", category = "", message = "", exception = "";
        public bool beforeQuit;
    }
}
