#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using OneStarMaker.Editor.ContentOverlay;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>観測の 1 回分の結果。<see cref="Changed"/> が偽なら記録を書き換えない。</summary>
    internal readonly struct PlayerOverlayStartupStep
    {
        internal PlayerOverlayStartupStep(PlayerOverlayStartup startup, string reason, bool changed)
        { Startup = startup; Reason = reason; Changed = changed; }
        internal PlayerOverlayStartup Startup { get; }
        internal string Reason { get; }
        internal bool Changed { get; }
    }

    /// <summary>
    /// 入口が作った Player の起動の後の観測（D10）。<c>last-attempt.json</c> の <c>startup</c> を <c>Pending</c> から 1 回だけ
    /// <c>Observed</c>・<c>Failed</c>・<c>TimedOut</c> に書き換える。
    /// <para>
    /// 規則は Unity を呼ばない関数 <see cref="Step"/> にあり、製品の pump（<c>EditorApplication.update</c> と Editor の起動・domain reload
    /// の後の読み直し）と判定の test の待ちの loop が同じ口を呼ぶ（同期の test の間は update が回らないため）。
    /// 状態は記録の file だけに持つ（domain reload で handle と static を失っても、pid と開始時刻と Player の log の path から続けられる）。
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayerOverlayStartupObserver
    {
        /// <summary>起動から初回 Scene の行か process の終了までの制限（受け入れ条件の 120 秒と同じ値）。</summary>
        internal const double TimeoutSeconds = 120;
        private const double PumpIntervalSeconds = 1;
        private static double _nextPumpAt;
        private static bool _pumping;

        static PlayerOverlayStartupObserver()
        {
            // Editor の起動と domain reload の後に、観測の途中の記録があれば続ける。
            if (ReadCurrent()?.Startup == PlayerOverlayStartup.Pending) Begin();
        }

        /// <summary>
        /// 1 回分の規則。<paramref name="attempt"/> が <c>Pending</c> でなければ何もしない。
        /// 初回 Scene の行があれば <c>Observed</c>（上書きつきの起動で、その前にこの上書きの登録の行が無ければ <c>Failed</c>）、
        /// 無いまま process が終わっていれば <c>Failed</c>（理由は Player の log の <c>Exception</c> を含む最初の行）、
        /// 無いまま制限を超えれば <c>TimedOut</c>。
        /// </summary>
        /// <param name="playerLog">Player の log の今の中身（読めなければ空）。</param>
        /// <param name="processExited">process が終わった、または pid と開始時刻の process がもう無い。</param>
        /// <param name="elapsedSeconds">起動からの経過。</param>
        internal static PlayerOverlayStartupStep Step(PlayerOverlayAttempt attempt, string playerLog, bool processExited, double elapsedSeconds)
        {
            if (attempt == null) throw new ArgumentNullException(nameof(attempt));
            var current = attempt.Startup;
            if (current != PlayerOverlayStartup.Pending) return new PlayerOverlayStartupStep(current, attempt.startupReason, false);

            var lines = (playerLog ?? string.Empty).Split('\n').Select(x => x.TrimEnd('\r')).ToArray();
            var firstScene = Array.FindIndex(lines, x => x.Contains(ContentOverlayPlayConfig.PlayerFirstSceneLogMarker));
            if (firstScene >= 0)
            {
                if (!attempt.hasOverlay) return new PlayerOverlayStartupStep(PlayerOverlayStartup.Observed, string.Empty, true);
                var registered = ContentOverlayPlayConfig.RegisteredLogMarker + "identity=" + attempt.overlayIdentity + " ";
                return lines.Take(firstScene).Any(x => x.Contains(registered))
                    ? new PlayerOverlayStartupStep(PlayerOverlayStartup.Observed, string.Empty, true)
                    : new PlayerOverlayStartupStep(PlayerOverlayStartup.Failed,
                        "The Player reached its first scene without registering the overlay " + attempt.overlayIdentity + ".", true);
            }
            if (processExited)
            {
                var exception = lines.FirstOrDefault(x => x.Contains("Exception"));
                return new PlayerOverlayStartupStep(PlayerOverlayStartup.Failed,
                    exception ?? "The Player exited before its first scene (no exception line in the Player log).", true);
            }
            if (elapsedSeconds > TimeoutSeconds)
                return new PlayerOverlayStartupStep(PlayerOverlayStartup.TimedOut,
                    "The Player did not reach its first scene within " + TimeoutSeconds.ToString(CultureInfo.InvariantCulture) + " seconds.", true);
            return new PlayerOverlayStartupStep(PlayerOverlayStartup.Pending, string.Empty, false);
        }

        /// <summary>製品の pump を始める（入口が process を作った後と、読み直しで <c>Pending</c> を見つけた時）。何度呼んでも 1 つ。</summary>
        internal static void Begin()
        {
            if (_pumping) return;
            _pumping = true;
            _nextPumpAt = 0;
            EditorApplication.update += Pump;
        }

        private static void Pump()
        {
            if (EditorApplication.timeSinceStartup < _nextPumpAt) return;
            _nextPumpAt = EditorApplication.timeSinceStartup + PumpIntervalSeconds;
            PlayerOverlayAttempt? attempt;
            try { attempt = ObserveOnce(); }
            catch (Exception ex)
            {
                // 観測の失敗で update を毎 frame 落とさない。記録は Pending のまま残り、次の reload か入口の実行で読み直す。
                Debug.LogException(ex);
                attempt = null;
            }
            if (attempt?.Startup == PlayerOverlayStartup.Pending) return;
            EditorApplication.update -= Pump;
            _pumping = false;
        }

        /// <summary>
        /// 今の記録を 1 回観測し、変われば記録を書き換えて Console（と、batch でなければ dialog）に出す。返り値は観測の後の記録
        /// （記録が無ければ null）。製品の pump と判定の test の待ちの loop が呼ぶ。
        /// </summary>
        internal static PlayerOverlayAttempt? ObserveOnce()
        {
            var path = CurrentPath();
            var attempt = PlayerOverlayRecords.ReadAttempt(path);
            if (attempt == null || attempt.Startup != PlayerOverlayStartup.Pending) return attempt;

            var step = Step(attempt, ReadLog(attempt.playerLogPath), !IsAlive(attempt), Elapsed(attempt));
            if (!step.Changed) return attempt;
            // 入口がもう一度実行されて記録が置き換わっていたら、前の Player の観測の結果で上書きしない。
            var latest = PlayerOverlayRecords.ReadAttempt(path);
            if (latest == null || latest.pid != attempt.pid || latest.launchedUtcTicks != attempt.launchedUtcTicks) return latest;
            attempt.startup = step.Startup.ToString();
            attempt.startupReason = step.Reason;
            PlayerOverlayRecords.WriteAttempt(path, attempt);
            Report(attempt);
            return attempt;
        }

        private static void Report(PlayerOverlayAttempt attempt)
        {
            switch (attempt.Startup)
            {
                case PlayerOverlayStartup.Observed:
                    Debug.Log("[PlayerOverlay] The Player " + attempt.playerIdentity + " reached its first scene" +
                              (attempt.hasOverlay ? " with the overlay " + attempt.overlayIdentity + "." : " without an overlay."));
                    break;
                case PlayerOverlayStartup.Failed:
                case PlayerOverlayStartup.TimedOut:
                    var message = "[PlayerOverlay] The Player " + attempt.playerIdentity + " startup " + attempt.startup + ": " + attempt.startupReason +
                                  " (Player log: " + attempt.playerLogPath + ")";
                    Debug.LogError(message);
                    if (!Application.isBatchMode) EditorUtility.DisplayDialog("Play Playable Player With Local Edits", message, "OK");
                    break;
            }
        }

        private static string CurrentPath() =>
            PlayerOverlayRecords.LastAttemptPath(SampleGamePlayerBuildCoordinator.ArtifactsRootOf(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")), PlayerBuildKind.Playable));

        private static PlayerOverlayAttempt? ReadCurrent()
        {
            try { return PlayerOverlayRecords.ReadAttempt(CurrentPath()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        }

        /// <summary>
        /// 起動中の Player が書いている <c>-logFile</c> を読む。Player が書き込みのために開いたままなので、書き込みと削除を共有する指定で開く
        /// （共有の指定が足りないと sharing violation で読めず、観測が進まない）。
        /// </summary>
        internal static string ReadLog(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return string.Empty;
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) { return string.Empty; }
        }

        /// <summary>pid と開始時刻が記録と同じ process が動いているか（pid は再利用されるので開始時刻も見る）。</summary>
        private static bool IsAlive(PlayerOverlayAttempt attempt)
        {
            if (attempt.pid <= 0 || !long.TryParse(attempt.processStartUtcTicks, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)) return false;
            try
            {
                using var process = Process.GetProcessById(attempt.pid);
                if (process.HasExited) return false;
                return Math.Abs(process.StartTime.ToUniversalTime().Ticks - ticks) < TimeSpan.TicksPerSecond;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private static double Elapsed(PlayerOverlayAttempt attempt) =>
            long.TryParse(attempt.launchedUtcTicks, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                ? (DateTime.UtcNow.Ticks - ticks) / (double)TimeSpan.TicksPerSecond
                : double.MaxValue;
    }
}
