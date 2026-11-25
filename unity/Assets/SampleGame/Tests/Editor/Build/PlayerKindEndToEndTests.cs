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
using SampleGame.Tests.Editor.Build;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 動作確認の Player（smoke）と遊べる Player（playable）を製品の coordinator で build し、起動して観測する判定用の test。
    /// <para>
    /// 1 つの test が 1 つの Unity process で 1 つのことをする（build 2 つ、起動 11）。全て <c>[Explicit]</c> で通常の回帰に混ぜない。
    /// 出力は <c>TestResults/s41/&lt;S41_RUN_ID&gt;/</c>。XML の合格は record を書き終えたことだけを示し、受け入れ条件の合否は
    /// <c>record.jsonl</c> と各 case の log・receipt で判定の担当が読む（この test は合否を判定しない）。
    /// </para>
    /// <para>
    /// 起動の case は種類ごとに 1 回だけ写した Player を使い、publish 済みの <c>artifacts/</c> の Player は起動しない。
    /// Player の folder を壊す case（content を空にする、baked の種類を書き換える）だけは、publish した Player を自分の case の folder へ
    /// 写し直して壊す（他の case が使う写しは変えない）。
    /// case は 1 つずつ走らせ、起動の前に前の case の receipt と game の log が残っていないことを確かめ、終わったら case の folder へ移す。
    /// 待ちは <see cref="Process.WaitForExit(int)"/> を刻みにして Player の log を読み直す（exit の signal を待つので sleep を使わない。
    /// 追記中の file の変更通知は遅れうるので FileSystemWatcher を起こし手にしない）。
    /// </para>
    /// </summary>
    public sealed class PlayerKindEndToEndTests
    {
        private const int TestTimeoutMs = 7200000;
        private const int MaxCopiedPathLength = 259;
        private const string FirstSceneLine = "[AppInit] Player content first scene:";
        private const string QuittingLine = PlayerProcessRun.QuittingLine;
        private const string ReceiptName = "bs4-smoke-receipt.json";
        private const string LocalDirectoryName = "OneStarMakerLocal";
        private static readonly string[] WindowArguments = { "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720" };

        // Player の log で有無と位置（閉じる前・後）を記録する語。strip の検出語は判定の最初の run で実際の文言と照らす。
        private static readonly string[] PlayerLogProbes =
        {
            FirstSceneLine, QuittingLine, "run-player-content-smoke", "unload-player-first-scene", "close-player-content-directory",
            "Editor content directory first scene", "AfterSceneLoad failed", "BeforeSceneLoad が失敗したため AfterSceneLoad をスキップします",
            "Protected Player", "ContentDeliveryException", "Exception", "MissingMethodException", "stripped", "is missing", "Could not produce class",
            // 起動の順と読んだ先（順は record の行番号で読む。どれも 1 回ずつ出ること）。
            RegisteredLine, UiCommonSourceLine, MapSourceLine, "source=active-scene", "source=player-resources", "source=addressables",
            "[AppInit] AfterSceneLoad: loading UICommon.", "Addressables backend sealed",
            // 新しい順の失敗の見え方。
            "A playable Player must not start from a scene that contains UICommon.", "The Player bootstrap scene must contain UICommon",
            "Content directory cleanup after startup failure also failed",
        };

        private const string RegisteredLine = "[AppInit] Content directory registered:";
        private const string UiCommonSourceLine = "[AppInit] Bootstrap UICommon:";
        private const string MapSourceLine = "[AppInit] Bootstrap SceneResourceMap:";
        private const string BakedConfigRelativePath = "SampleGame_Data/StreamingAssets/bs4-runtime.json";

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

        // AC9: 登録の失敗（UICommon より前）。自分の case の写しの content/ を空にする（folder だけ残す）。
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void LaunchPlayableEmptyContent() => Launch(nameof(LaunchPlayableEmptyContent), PlayerBuildKind.Playable, Env(),
            run => run.WaitForExit(TimeSpan.FromSeconds(60), "exit"), EmptyContentDirectory);

        // AC10: UICommon と map を content から読んだ後の失敗（protected でない key で scene factory を失敗させる）。
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void LaunchPlayableBrokenCompanion() => Launch(nameof(LaunchPlayableBrokenCompanion), PlayerBuildKind.Playable,
            Env("SAMPLEGAME_WORLD__CELLCOMPANIONSET", "Broken"), run => run.WaitForExit(TimeSpan.FromSeconds(120), "exit"));

        // AC14 (a): 動作確認の Player（開始 Scene は UICommon）の写しの baked の種類を playable に書き換える。
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void LaunchSmokeAsPlayable() => Launch(nameof(LaunchSmokeAsPlayable), PlayerBuildKind.Smoke, Env(),
            run => run.WaitForExit(TimeSpan.FromSeconds(60), "exit"), player => RewriteBakedPlayerKind(player, "smoke", "playable"));

        // AC14 (b): 遊べる Player（開始 Scene は空）の写しの baked の種類を smoke に書き換える。
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void LaunchPlayableAsSmoke() => Launch(nameof(LaunchPlayableAsSmoke), PlayerBuildKind.Playable, Env(),
            run => run.WaitForExit(TimeSpan.FromSeconds(60), "exit"), player => RewriteBakedPlayerKind(player, "playable", "smoke"));

        // AC6: Phase C の実行担当（AI）が desktop の操作で Title の画面を保存し、「スタート」を 1 回 click し、InGame の画面を保存して窓を閉じる。
        // test は started.json を書いて exit を待つだけ（上限 1800 秒）。
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void LaunchPlayableHuman() => Launch(nameof(LaunchPlayableHuman), PlayerBuildKind.Playable, Env(), run =>
        {
            File.WriteAllText(Path.Combine(run.CaseDirectory, "started.json"), JsonUtility.ToJson(new StartedRecord
            {
                caseName = run.Record.caseName, pid = run.Record.pid, startedUtc = run.Record.startedUtc,
            }, true));
            TestContext.Progress.WriteLine($"[S41] {run.Record.caseName} started: pid={run.Record.pid}. Take the Title screenshot, click Start once, " +
                                           "take the InGame screenshot, then close the window. Put screenshots in " + run.CaseDirectory);
            run.WaitForExit(TimeSpan.FromSeconds(1800), "human");
        });

        // ─── build ───

        /// <summary>build して run の写しを作る。上書きの判定の test も、同じ写しと record を作るためにここを呼ぶ。</summary>
        internal static PlayerBuildResult BuildAndCopy(PlayerBuildKind kind)
        {
            var run = RunDirectory();
            var name = KindName(kind);
            var copy = CopiedPlayerDirectory(run, kind);
            if (Directory.Exists(copy) || File.Exists(BuildRecordPath(run, kind)))
                Assert.Fail($"{name} is already built in this run. Re-run every step with another S41_RUN_ID instead of overwriting.");

            var result = SampleGamePlayerBuildCoordinator.Build(kind);
            // AC3 (c): build の直後に mutation の 2 つの path が残っていないか（空の親の folder と .meta の残りは既知の形で、ここでは見ない）。
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var mutationRootExists = Directory.Exists(Path.Combine(projectRoot, "Assets", "Resources", "BS4", result.Identity));
            var runtimeConfigExists = File.Exists(Path.Combine(projectRoot, "Assets", "StreamingAssets", "bs4-runtime.json"));
            CopyDirectory(result.PlayerDirectory, copy);
            var receipt = JsonUtility.FromJson<BuildReceiptScenes>(File.ReadAllText(Path.Combine(copy, "build-receipt.json")));
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
                receiptScenes = receipt.scenes, receiptBootstrapSceneName = receipt.bootstrapSceneName,
                mutationRootExistsAfterBuild = mutationRootExists, runtimeConfigExistsAfterBuild = runtimeConfigExists,
                copiesBootstrap = SampleGamePlayerBuildCoordinator.SettingsOf(kind).CopiesBootstrap,
            };
            if (result.Playable != null)
            {
                record.mutationRootFilesBeforeBuild = result.Playable.MutationRootFilesBeforeBuild;
                record.startSceneRootCount = result.Playable.StartSceneRootCount;
                record.mapSceneResourceCount = result.Playable.MapSceneResourceCount;
                record.forbiddenGuidCount = result.Playable.ForbiddenGuidCount;
                record.startScenePackCount = result.Playable.StartScenePackCount;
            }
            File.WriteAllText(BuildRecordPath(run, kind), JsonUtility.ToJson(record, true));
            // Unity 同梱の Mono と Player は長い path を開けない（起動前の path の予算の検査の対象外なのでここで止める）。
            Assert.That(longest, Is.LessThanOrEqualTo(MaxCopiedPathLength), "Copied Player path is too long: " + longest);
            return result;
        }

        private static string ReadUnityVersion(string transportDirectory)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            JsonConfigFlattener.Flatten(File.ReadAllText(Path.Combine(transportDirectory, "transport.json")), values);
            return values.TryGetValue("unityVersion", out var version) ? version : string.Empty;
        }

        // ─── launch ───

        /// <param name="breakOwnCopy">
        /// null でなければ、publish した Player（build の record の <c>playerDirectory</c>）を自分の case の folder の <c>player/</c> へ写し直し、
        /// その写しにこの操作をしてから起動する。種類ごとの共有の写しは変えない。
        /// </param>
        private static void Launch(string caseName, PlayerBuildKind kind, IReadOnlyList<KeyValuePair<string, string>> environment, Action<PlayerRun> drive,
            Action<string>? breakOwnCopy = null)
        {
            var run = RunDirectory();
            var shared = CopiedPlayerDirectory(run, kind);
            if (!File.Exists(Path.Combine(shared, "SampleGame.exe"))) Assert.Fail($"{KindName(kind)} is not built in this run: {shared}");
            var caseDirectory = Path.Combine(run, caseName);
            if (Directory.Exists(caseDirectory)) Assert.Fail($"{caseName} already ran in this run. Re-run every step with another S41_RUN_ID.");
            var player = shared;
            var ownCopyLongestPath = 0;
            if (breakOwnCopy != null)
            {
                var published = JsonUtility.FromJson<BuildRecordPlayerDirectory>(File.ReadAllText(BuildRecordPath(run, kind))).playerDirectory;
                if (!File.Exists(Path.Combine(published, "SampleGame.exe"))) Assert.Fail("The published Player of this run is missing: " + published);
                Directory.CreateDirectory(caseDirectory);
                player = Path.Combine(caseDirectory, "player");
                CopyDirectory(published, player);
                ownCopyLongestPath = Directory.EnumerateFileSystemEntries(player, "*", SearchOption.AllDirectories).Select(p => p.Length).DefaultIfEmpty(0).Max();
                // 共有の写しと同じ path の予算（Unity 同梱の Mono と Player は長い path を開けない）。
                Assert.That(ownCopyLongestPath, Is.LessThanOrEqualTo(MaxCopiedPathLength), "Case Player path is too long: " + ownCopyLongestPath);
                breakOwnCopy(player);
            }
            var exe = Path.Combine(player, "SampleGame.exe");
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
                ownPlayerCopy = breakOwnCopy != null ? player : string.Empty, ownCopyLongestPathLength = ownCopyLongestPath,
            };
            var clock = Stopwatch.StartNew();
            record.startedUtc = Utc(DateTime.UtcNow);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Player process did not start.");
            record.pid = process.Id;
            var state = new PlayerRun(new PlayerProcessRun(process, logPath, caseDirectory), record);
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
                state.CopyObservation();
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
            record.gameLog = PlayerProcessRun.ReadGameLog(Path.Combine(movedLocal, "Log"), record.quitObservedUtc, GameLogProbes);
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

        /// <summary>
        /// 起動した Player の 1 回の観測（<see cref="PlayerProcessRun"/>）と、この test の record の組。観測の本体は共有の補助にあり、
        /// ここは record の欄へ写すだけを持つ。
        /// </summary>
        private sealed class PlayerRun
        {
            private readonly PlayerProcessRun _run;

            internal PlayerRun(PlayerProcessRun run, LaunchRecord record) { _run = run; Record = record; }

            internal string CaseDirectory => _run.CaseDirectory;
            internal LaunchRecord Record { get; }
            internal string LogText => _run.LogText;

            internal bool WaitForLine(string line, TimeSpan limit, string phase) => _run.WaitForLine(line, limit, phase);
            internal bool WaitForExit(TimeSpan limit, string phase) => _run.WaitForExit(limit, phase);
            internal bool Survives(TimeSpan duration) => _run.Survives(duration);
            internal void RequestClose() => _run.RequestClose();
            internal void ReadLog() => _run.ReadLog();

            /// <summary>観測した時刻と結果を record に写す（drive の後、record を書く前に 1 回）。</summary>
            internal void CopyObservation()
            {
                Record.firstSceneLineUtc = _run.FirstSceneLineUtc;
                Record.quitObservedUtc = _run.QuitObservedUtc;
                Record.closeRequestedUtc = _run.CloseRequestedUtc;
                Record.closeMessageSent = _run.CloseMessageSent;
                Record.timeout = _run.Timeout;
                Record.timeoutPhase = _run.TimeoutPhase;
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
        private static string Utc(DateTime value) => PlayerProcessRun.Utc(value);
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

        /// <summary>AC9: 写しの <c>content/</c> の下の file と folder を全て消し、<c>content/</c> の folder だけを残す。</summary>
        private static void EmptyContentDirectory(string player)
        {
            var content = Path.Combine(player, "content");
            if (!Directory.Exists(content)) Assert.Fail("The Player has no content directory: " + content);
            foreach (var file in Directory.GetFiles(content)) File.Delete(file);
            foreach (var child in Directory.GetDirectories(content)) Directory.Delete(child, true);
        }

        /// <summary>
        /// AC14: 写しの baked config の <c>content.playerKind</c> の値だけを書き換える。build が書いた形（JsonUtility の整形）で
        /// ちょうど 1 か所に当たらなければ、壊し方が成り立たないので起動せずに止める。
        /// </summary>
        private static void RewriteBakedPlayerKind(string player, string from, string to)
        {
            var path = Path.Combine(player, BakedConfigRelativePath);
            var text = File.ReadAllText(path);
            var before = "\"playerKind\": \"" + from + "\"";
            var count = 0;
            for (var at = text.IndexOf(before, StringComparison.Ordinal); at >= 0; at = text.IndexOf(before, at + before.Length, StringComparison.Ordinal)) count++;
            if (count != 1) Assert.Fail($"Baked playerKind '{from}' must appear exactly once in {path}; found {count}.");
            File.WriteAllText(path, text.Replace(before, "\"playerKind\": \"" + to + "\""));
        }

        // ─── records（JsonUtility で 1 行に書く） ───

        [Serializable]
        private sealed class BuildRecord
        {
            public string kind = "", identity = "", playerDirectory = "", copiedPlayerDirectory = "", transportDirectory = "";
            public string unityVersion = "", editorUnityVersion = "", buildOptions = "", scriptingBackendDuringBuild = "", managedStrippingDuringBuild = "";
            public bool developmentInOptions, gameAssemblyPresent, managedDirectoryPresent;
            public int longestCopiedPathLength;
            // AC3: receipt の 2 欄、build の直後の mutation の残り、遊べる Player だけの観測（動作確認の Player では -1 と空）。
            public string[] receiptScenes = Array.Empty<string>();
            public string receiptBootstrapSceneName = "";
            public bool mutationRootExistsAfterBuild, runtimeConfigExistsAfterBuild, copiesBootstrap;
            public string[] mutationRootFilesBeforeBuild = Array.Empty<string>();
            public int startSceneRootCount = -1, mapSceneResourceCount = -1, forbiddenGuidCount = -1, startScenePackCount = -1;
        }

        [Serializable]
        private sealed class BuildReceiptScenes
        {
            public string[] scenes = Array.Empty<string>();
            public string bootstrapSceneName = "";
        }

        /// <summary>build の record の、publish した Player の directory（case 専用の写しの元）。</summary>
        [Serializable]
        private sealed class BuildRecordPlayerDirectory
        {
            public string playerDirectory = "";
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
            /// <summary>case 専用に写し直して壊した Player（AC9・AC14）。共有の写しを使った case は空。</summary>
            public string ownPlayerCopy = "";
            public int ownCopyLongestPathLength;
            public ProbeHit[] playerLog = Array.Empty<ProbeHit>();
            public PlayerGameLogLine[] gameLog = Array.Empty<PlayerGameLogLine>();
        }

        [Serializable]
        private sealed class ProbeHit
        {
            public string probe = "", text = "";
            public int line;
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
