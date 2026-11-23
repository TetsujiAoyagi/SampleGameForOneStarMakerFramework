#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Foundation.Config;
using SampleGame.DependOnAll.Editor.Build;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 動作確認の Player（smoke）と遊べる Player（playable）を製品の coordinator で build し、起動して観測する判定用の test。
    /// <para>
    /// 1 つの test が 1 つの Unity process で 1 つのことをする（build 2 つ、起動 7 つ）。全て <c>[Explicit]</c> で通常の回帰に混ぜない。
    /// 出力は <c>TestResults/s41/&lt;S41_RUN_ID&gt;/</c>。XML の合格は record を書き終えたことだけを示し、受け入れ条件の合否は
    /// <c>record.jsonl</c> と各 case の log・receipt で判定の担当が読む（この test は合否を判定しない）。
    /// </para>
    /// <para>
    /// 起動の case は種類ごとに 1 回だけ写した Player を使い、publish 済みの <c>artifacts/</c> の Player は起動しない。
    /// case は 1 つずつ走らせ、起動の前に前の case の receipt と game の log が残っていないことを確かめ、終わったら case の folder へ移す。
    /// 待ちは <see cref="Process.WaitForExit(int)"/> を刻みにして Player の log を読み直す（exit の signal を待つので sleep を使わない。
    /// 追記中の file の変更通知は遅れうるので FileSystemWatcher を起こし手にしない）。
    /// </para>
    /// </summary>
    public sealed class PlayerKindEndToEndTests
    {
        private const int TestTimeoutMs = 7200000;
        private const int PollMs = 500;
        private const int MaxCopiedPathLength = 259;
        private const string FirstSceneLine = "[AppInit] Player content first scene:";
        private const string QuittingLine = "[AppInit] Application quitting.";
        private const string ReceiptName = "bs4-smoke-receipt.json";
        private const string LocalDirectoryName = "OneStarMakerLocal";
        private static readonly string[] WindowArguments = { "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720" };

        // Player の log で有無と位置（閉じる前・後）を記録する語。strip の検出語は判定の最初の run で実際の文言と照らす。
        private static readonly string[] PlayerLogProbes =
        {
            FirstSceneLine, QuittingLine, "run-player-content-smoke", "unload-player-first-scene", "close-player-content-directory",
            "Editor content directory first scene", "AfterSceneLoad failed", "BeforeSceneLoad が失敗したため AfterSceneLoad をスキップします",
            "Protected Player", "ContentDeliveryException", "Exception", "MissingMethodException", "stripped", "is missing", "Could not produce class",
        };

        // game の log（ZLogger の JSON）で、Error 以上の行のほかに記録する message。
        private static readonly string[] GameLogProbes =
        {
            "Loaded.", "OnStabledImpl HomeScene", "[InGameSession] initial season ensure started", "Player ready at Cell stream spawn",
            "Season branch ensure failed",
        };

        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void BuildSmoke() => BuildAndCopy(PlayerBuildKind.Smoke);

        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void BuildPlayable() => BuildAndCopy(PlayerBuildKind.Playable);

        // AC1: 動作確認の Player が 300 秒以内に自分で終わる。
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void LaunchSmoke() => Launch(nameof(LaunchSmoke), PlayerBuildKind.Smoke, Env(), run => run.WaitForExit(TimeSpan.FromSeconds(300), "exit"));

        // AC2: 動作確認の Player に種類の override を置くと 60 秒以内に失敗で終わる。
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void LaunchSmokeOverride() => Launch(nameof(LaunchSmokeOverride), PlayerBuildKind.Smoke,
            Env("SAMPLEGAME_CONTENT__PLAYERKIND", "playable"), run => run.WaitForExit(TimeSpan.FromSeconds(60), "exit"));

        // AC4・AC5: 初回 Scene の行を 120 秒以内に待ち、その後 60 秒終わらないことを見て、閉じる要求から 120 秒以内に終わるのを待つ。
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void LaunchPlayable() => Launch(nameof(LaunchPlayable), PlayerBuildKind.Playable, Env(), run =>
        {
            if (!run.WaitForLine(FirstSceneLine, TimeSpan.FromSeconds(120), "first-scene")) return;
            run.Record.survivedObservation = run.Survives(TimeSpan.FromSeconds(60));
            if (!run.Record.survivedObservation) return;
            run.RequestClose();
            run.WaitForExit(TimeSpan.FromSeconds(120), "close");
        });

        // AC7 (a): 遊べる Player に種類の override を置く。
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void LaunchPlayableKindOverride() => Launch(nameof(LaunchPlayableKindOverride), PlayerBuildKind.Playable,
            Env("SAMPLEGAME_CONTENT__PLAYERKIND", "smoke"), run => run.WaitForExit(TimeSpan.FromSeconds(60), "exit"));

        // AC7 (b): 遊べる Player に runtime mode の override を置く。
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void LaunchPlayableAddressables() => Launch(nameof(LaunchPlayableAddressables), PlayerBuildKind.Playable,
            Env("SAMPLEGAME_CONTENT__RUNTIMEMODE", "addressables"), run => run.WaitForExit(TimeSpan.FromSeconds(60), "exit"));

        // AC8: 存在しない installed の revision を指す。
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void LaunchPlayableMissingInstall() => Launch(nameof(LaunchPlayableMissingInstall), PlayerBuildKind.Playable,
            Env("SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH", Path.Combine(Path.GetTempPath(), "osm-s41-missing-" + Guid.NewGuid().ToString("N")),
                "SAMPLEGAME_CONTENT__MANIFESTSHA256", new string('0', 64)),
            run => run.WaitForExit(TimeSpan.FromSeconds(60), "exit"));

        // AC6: owner が Title の Start を押し、InGame の画面が出たら窓の × で閉じる。test は exit を待つだけ（上限 1800 秒）。
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void LaunchPlayableHuman() => Launch(nameof(LaunchPlayableHuman), PlayerBuildKind.Playable, Env(), run =>
        {
            File.WriteAllText(Path.Combine(run.CaseDirectory, "started.json"), JsonUtility.ToJson(new StartedRecord
            {
                caseName = run.Record.caseName, pid = run.Record.pid, startedUtc = run.Record.startedUtc,
            }, true));
            TestContext.Progress.WriteLine($"[S41] {run.Record.caseName} started: pid={run.Record.pid}. Take the Title screenshot, press Start once, " +
                                           "take the InGame screenshot, then close the window. Put screenshots in " + run.CaseDirectory);
            run.WaitForExit(TimeSpan.FromSeconds(1800), "human");
        });

        // ─── build ───

        private static void BuildAndCopy(PlayerBuildKind kind)
        {
            var run = RunDirectory();
            var name = KindName(kind);
            var copy = CopiedPlayerDirectory(run, kind);
            if (Directory.Exists(copy) || File.Exists(BuildRecordPath(run, kind)))
                Assert.Fail($"{name} is already built in this run. Re-run every step with another S41_RUN_ID instead of overwriting.");

            var result = SampleGamePlayerBuildCoordinator.Build(kind);
            CopyDirectory(result.PlayerDirectory, copy);
            var longest = Directory.EnumerateFileSystemEntries(copy, "*", SearchOption.AllDirectories).Select(p => p.Length).DefaultIfEmpty(0).Max();
            var record = new BuildRecord
            {
                kind = name, identity = result.Identity, playerDirectory = result.PlayerDirectory, copiedPlayerDirectory = copy,
                transportDirectory = result.TransportDirectory, unityVersion = ReadUnityVersion(result.TransportDirectory),
                editorUnityVersion = Application.unityVersion, buildOptions = result.BuildOptions,
                developmentInOptions = result.BuildOptions.IndexOf("Development", StringComparison.Ordinal) >= 0,
                scriptingBackendDuringBuild = result.ScriptingBackendDuringBuild, managedStrippingDuringBuild = result.ManagedStrippingDuringBuild,
                gameAssemblyPresent = File.Exists(Path.Combine(copy, "GameAssembly.dll")),
                managedDirectoryPresent = Directory.Exists(Path.Combine(copy, "SampleGame_Data", "Managed")),
                longestCopiedPathLength = longest,
            };
            File.WriteAllText(BuildRecordPath(run, kind), JsonUtility.ToJson(record, true));
            // Unity 同梱の Mono と Player は長い path を開けない（起動前の path の予算の検査の対象外なのでここで止める）。
            Assert.That(longest, Is.LessThanOrEqualTo(MaxCopiedPathLength), "Copied Player path is too long: " + longest);
        }

        private static string ReadUnityVersion(string transportDirectory)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            JsonConfigFlattener.Flatten(File.ReadAllText(Path.Combine(transportDirectory, "transport.json")), values);
            return values.TryGetValue("unityVersion", out var version) ? version : string.Empty;
        }

        // ─── launch ───

        private static void Launch(string caseName, PlayerBuildKind kind, IReadOnlyList<KeyValuePair<string, string>> environment, Action<PlayerRun> drive)
        {
            var run = RunDirectory();
            var player = CopiedPlayerDirectory(run, kind);
            var exe = Path.Combine(player, "SampleGame.exe");
            if (!File.Exists(exe)) Assert.Fail($"{KindName(kind)} is not built in this run: {exe}");
            var caseDirectory = Path.Combine(run, caseName);
            if (Directory.Exists(caseDirectory)) Assert.Fail($"{caseName} already ran in this run. Re-run every step with another S41_RUN_ID.");
            var receipt = Path.Combine(player, ReceiptName);
            var local = Path.Combine(player, LocalDirectoryName);
            if (File.Exists(receipt) || Directory.Exists(local))
                Assert.Fail("A previous case left its receipt or game log in the copied Player: " + player);
            Directory.CreateDirectory(caseDirectory);

            var logPath = Path.Combine(caseDirectory, "player.log");
            var batch = kind == PlayerBuildKind.Smoke;
            var arguments = new List<string>();
            if (batch) arguments.Add("-batchmode");
            else arguments.AddRange(WindowArguments);
            arguments.Add("-logFile");
            arguments.Add(logPath);

            var start = new ProcessStartInfo(exe)
            {
                Arguments = string.Join(" ", arguments.Select(Quote)), UseShellExecute = false, WorkingDirectory = player,
            };
            // 重ねる値は case が置いたものだけにする（この process の SAMPLEGAME_ の値を Player へ漏らさない）。
            foreach (var key in start.EnvironmentVariables.Keys.Cast<string>().Where(k => k.StartsWith("SAMPLEGAME_", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.EnvironmentVariables.Remove(key);
            foreach (var pair in environment) start.EnvironmentVariables[pair.Key] = pair.Value;

            var record = new LaunchRecord
            {
                runId = Path.GetFileName(run), caseName = caseName, kind = KindName(kind), executable = exe, arguments = start.Arguments,
                environment = environment.Select(p => p.Key + "=" + p.Value).ToArray(),
                build = File.Exists(BuildRecordPath(run, kind)) ? File.ReadAllText(BuildRecordPath(run, kind)) : string.Empty,
            };
            var clock = Stopwatch.StartNew();
            record.startedUtc = Utc(DateTime.UtcNow);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Player process did not start.");
            record.pid = process.Id;
            var state = new PlayerRun(process, logPath, caseDirectory, record);
            try
            {
                drive(state);
            }
            finally
            {
                if (!process.HasExited)
                {
                    // 制限時間を超えた起動は不合格。kill した起動の game の log は「観測なし」として扱う。
                    record.killed = true;
                    try { process.Kill(); } catch (InvalidOperationException) { }
                    process.WaitForExit();
                }
                record.endedUtc = Utc(DateTime.UtcNow);
                record.seconds = clock.Elapsed.TotalSeconds;
                record.exitCode = process.ExitCode;
                state.ReadLog();
                Finish(record, state, player, caseDirectory);
            }
        }

        private static void Finish(LaunchRecord record, PlayerRun run, string player, string caseDirectory)
        {
            var receipt = Path.Combine(player, ReceiptName);
            record.receiptPresent = File.Exists(receipt);
            if (record.receiptPresent)
            {
                record.receipt = File.ReadAllText(receipt);
                File.Move(receipt, Path.Combine(caseDirectory, ReceiptName));
            }
            var local = Path.Combine(player, LocalDirectoryName);
            var movedLocal = Path.Combine(caseDirectory, LocalDirectoryName);
            if (Directory.Exists(local)) Directory.Move(local, movedLocal);

            record.playerLog = ProbePlayerLog(run.LogText);
            record.gameLog = ReadGameLog(Path.Combine(movedLocal, "Log"), record.quitObservedUtc);
            File.AppendAllText(Path.Combine(Path.GetDirectoryName(caseDirectory)!, "record.jsonl"), JsonUtility.ToJson(record, false) + "\n");
        }

        private static ProbeHit[] ProbePlayerLog(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var quitIndex = Array.FindIndex(lines, line => line.Contains(QuittingLine));
            var hits = new List<ProbeHit>();
            foreach (var probe in PlayerLogProbes)
            {
                var found = false;
                for (var i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].Contains(probe)) continue;
                    found = true;
                    hits.Add(new ProbeHit { probe = probe, line = i + 1, beforeQuit = quitIndex < 0 || i < quitIndex, text = lines[i] });
                }
                // 行が無いことも記録する（line 0）。
                if (!found) hits.Add(new ProbeHit { probe = probe, line = 0, beforeQuit = true, text = string.Empty });
            }
            return hits.ToArray();
        }

        // record の時刻は Utc の "o"（末尾 Z）、game の log の Timestamp は +00:00 などの offset 付きで書かれる。
        // 両方を UTC の DateTime に直して比べるため AdjustToUniversal を使い、offset の無い文字列は UTC とみなす。
        // RoundtripKind は AdjustToUniversal・AssumeUniversal と組めず、.NET が ArgumentException で拒むので混ぜない。
        private const DateTimeStyles UtcTimestampStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

        private static GameLogLine[] ReadGameLog(string directory, string quitObservedUtc)
        {
            if (!Directory.Exists(directory)) return Array.Empty<GameLogLine>();
            DateTime? quit = quitObservedUtc.Length == 0 ? null
                : DateTime.Parse(quitObservedUtc, CultureInfo.InvariantCulture, UtcTimestampStyles);
            var lines = new List<GameLogLine>();
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                foreach (var raw in File.ReadLines(file))
                {
                    if (raw.Length == 0 || raw[0] != '{') continue;
                    var values = new Dictionary<string, string>(StringComparer.Ordinal);
                    try { JsonConfigFlattener.Flatten(raw, values); }
                    catch (Exception) { lines.Add(new GameLogLine { file = Path.GetFileName(file), message = "<unparsed> " + raw }); continue; }
                    var level = Value(values, "LogLevel");
                    var message = Value(values, "Message");
                    var isError = level == "Error" || level == "Critical";
                    if (!isError && !GameLogProbes.Any(probe => message.Contains(probe))) continue;
                    var timestamp = Value(values, "Timestamp");
                    var beforeQuit = true;
                    if (quit.HasValue && DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, UtcTimestampStyles, out var at))
                        beforeQuit = at < quit.Value;
                    lines.Add(new GameLogLine
                    {
                        file = Path.GetFileName(file), timestamp = timestamp, level = level, category = Value(values, "Category"),
                        message = message, exception = Value(values, "Exception:Message"), beforeQuit = beforeQuit,
                    });
                }
            }
            return lines.ToArray();
        }

        private static string Value(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out var value) ? value : string.Empty;

        /// <summary>起動した Player の 1 回の観測。待ちは全て exit の signal の刻みで、刻みごとに Player の log を読み直す。</summary>
        private sealed class PlayerRun
        {
            private readonly Process _process;
            private readonly string _logPath;

            internal PlayerRun(Process process, string logPath, string caseDirectory, LaunchRecord record)
            { _process = process; _logPath = logPath; CaseDirectory = caseDirectory; Record = record; }

            internal string CaseDirectory { get; }
            internal LaunchRecord Record { get; }
            internal string LogText { get; private set; } = string.Empty;

            /// <summary>行が出るまで待つ。出なければ（先に exit した、制限時間を超えた）偽。</summary>
            internal bool WaitForLine(string line, TimeSpan limit, string phase)
            {
                var clock = Stopwatch.StartNew();
                while (true)
                {
                    var exited = Tick();
                    if (LogText.Contains(line))
                    {
                        if (phase == "first-scene") Record.firstSceneLineUtc = Utc(DateTime.UtcNow);
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

            internal void RequestClose()
            {
                _process.Refresh();
                Record.closeRequestedUtc = Utc(DateTime.UtcNow);
                Record.closeMessageSent = _process.CloseMainWindow();
            }

            internal void ReadLog()
            {
                if (!File.Exists(_logPath)) return;
                using var stream = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                LogText = reader.ReadToEnd();
                if (Record.quitObservedUtc.Length == 0 && LogText.Contains(QuittingLine)) Record.quitObservedUtc = Utc(DateTime.UtcNow);
            }

            private bool Tick()
            {
                var exited = _process.WaitForExit(PollMs);
                ReadLog();
                return exited;
            }

            private void MarkTimeout(string phase)
            {
                Record.timeout = true;
                Record.timeoutPhase = phase;
            }
        }

        // ─── paths and helpers ───

        private static string RunDirectory()
        {
            var runId = Environment.GetEnvironmentVariable("S41_RUN_ID");
            if (string.IsNullOrWhiteSpace(runId) || runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                Assert.Fail("Set S41_RUN_ID to the same file-name-safe value for every step of one run.");
            var run = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "s41", runId!));
            Directory.CreateDirectory(run);
            return run;
        }

        private static string KindName(PlayerBuildKind kind) => kind == PlayerBuildKind.Smoke ? "smoke" : "playable";
        private static string CopiedPlayerDirectory(string run, PlayerBuildKind kind) => Path.Combine(run, KindName(kind), "player");
        private static string BuildRecordPath(string run, PlayerBuildKind kind) => Path.Combine(run, KindName(kind) + ".json");
        private static string Utc(DateTime value) => value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        private static string Quote(string value) => value.IndexOf(' ') >= 0 ? "\"" + value + "\"" : value;

        private static IReadOnlyList<KeyValuePair<string, string>> Env(params string[] pairs)
        {
            var values = new List<KeyValuePair<string, string>>();
            for (var i = 0; i + 1 < pairs.Length; i += 2) values.Add(new KeyValuePair<string, string>(pairs[i], pairs[i + 1]));
            return values;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            foreach (var child in Directory.GetDirectories(source)) CopyDirectory(child, Path.Combine(target, Path.GetFileName(child)));
        }

        // ─── records（JsonUtility で 1 行に書く） ───

        [Serializable]
        private sealed class BuildRecord
        {
            public string kind = "", identity = "", playerDirectory = "", copiedPlayerDirectory = "", transportDirectory = "";
            public string unityVersion = "", editorUnityVersion = "", buildOptions = "", scriptingBackendDuringBuild = "", managedStrippingDuringBuild = "";
            public bool developmentInOptions, gameAssemblyPresent, managedDirectoryPresent;
            public int longestCopiedPathLength;
        }

        [Serializable]
        private sealed class LaunchRecord
        {
            public string runId = "", caseName = "", kind = "", executable = "", arguments = "";
            public string[] environment = Array.Empty<string>();
            public int pid;
            public string startedUtc = "", endedUtc = "", firstSceneLineUtc = "", closeRequestedUtc = "", quitObservedUtc = "";
            public double seconds;
            public int exitCode;
            public bool killed, timeout, survivedObservation, closeMessageSent, receiptPresent;
            public string timeoutPhase = "", receipt = "", build = "";
            public ProbeHit[] playerLog = Array.Empty<ProbeHit>();
            public GameLogLine[] gameLog = Array.Empty<GameLogLine>();
        }

        [Serializable]
        private sealed class ProbeHit
        {
            public string probe = "", text = "";
            public int line;
            public bool beforeQuit;
        }

        [Serializable]
        private sealed class GameLogLine
        {
            public string file = "", timestamp = "", level = "", category = "", message = "", exception = "";
            public bool beforeQuit;
        }

        [Serializable]
        private sealed class StartedRecord
        {
            public string caseName = "", startedUtc = "";
            public int pid;
        }
    }
}
