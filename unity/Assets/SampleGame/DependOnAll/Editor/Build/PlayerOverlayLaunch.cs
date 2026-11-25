#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using OneStarMaker.Editor.ContentOverlay;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>子 process の起動の要求。<see cref="Environment"/> は子の環境の全体（継承した値を含み、<c>SAMPLEGAME_*</c> は入口が置いた物だけ）。</summary>
    internal sealed class PlayerLaunchRequest
    {
        internal PlayerLaunchRequest(string executable, string arguments, IReadOnlyDictionary<string, string> environment)
        { Executable = executable; Arguments = arguments; Environment = environment; }
        internal string Executable { get; }
        internal string Arguments { get; }
        internal IReadOnlyDictionary<string, string> Environment { get; }
    }

    /// <summary>入口の port。実物は <see cref="PlayerOverlayLaunch.ProductPorts"/> が作り、test は差し替える（Player を起動しない）。</summary>
    internal sealed class PlayerOverlayLaunchPorts
    {
        internal Func<PlayablePlayerChoice> LocatePlayer { get; set; } = () => throw new InvalidOperationException("LocatePlayer is not set.");
        internal Func<PlayablePlayerChoice, PlayerBaseResolution> ResolveBase { get; set; } = _ => throw new InvalidOperationException("ResolveBase is not set.");
        /// <summary>未保存の Scene の確認と保存。Cancel なら偽。</summary>
        internal Func<bool> ConfirmSaveScenes { get; set; } = () => true;
        internal Func<IReadOnlyList<string>> EnumerateUnsavedAssets { get; set; } = () => Array.Empty<string>();
        /// <summary>未保存の asset の一覧を示して「Save and continue / Cancel」を問う。Cancel なら偽。0 件の時は呼ばない。</summary>
        internal Func<IReadOnlyList<string>, bool> ConfirmSaveAssets { get; set; } = _ => false;
        internal Action SaveAssets { get; set; } = () => { };
        /// <summary>Player の folder の <c>player-compat.json</c> を読む。読めなければ例外。</summary>
        internal Func<string, PlayerCompatibilityRecord> ReadPlayerCompatibility { get; set; } = _ => throw new InvalidOperationException("ReadPlayerCompatibility is not set.");
        /// <summary>保存の後の disk から、Player の入力の指紋の file を集める（<see cref="PlayerInputFiles.Collect"/>）。</summary>
        internal Func<IReadOnlyList<PlayerInputFileEntry>> CollectPlayerInputs { get; set; } = () => throw new InvalidOperationException("CollectPlayerInputs is not set.");
        /// <summary>base の install から上書きの段の port を作る（<c>suppliedSceneKeys</c> は空、contentSet は Player の上書きの物）。</summary>
        internal Func<ContentInstallResult, PlayEntryOverlayPorts> OverlayPorts { get; set; } = _ => throw new InvalidOperationException("OverlayPorts is not set.");
        /// <summary>上書きの directory の digest（<see cref="ContentOverlayPlayConfig.ComputeOverlayDigest"/>）。</summary>
        internal Func<string, string> ComputeDigest { get; set; } = ContentOverlayPlayConfig.ComputeOverlayDigest;
        /// <summary>上書きの identity の <c>overlay-record.json</c>。無い・読めなければ null。</summary>
        internal Func<string, PlayerOverlayBuildRecord?> ReadBuildRecord { get; set; } = _ => null;
        internal Action<PlayerOverlayBuildRecord> WriteBuildRecord { get; set; } = _ => { };
        /// <summary>
        /// 起動の直前の digest の取り直しの後・process を作る前に 1 回呼ぶ。引数は上書きの directory（上書きが無ければ呼ばない）。
        /// 製品では何もしない。判定の test だけが、入口の照合を通った後の書き換えを Player が止めることを確かめるために使う。
        /// </summary>
        internal Action<string> BeforeProcessStart { get; set; } = _ => { };
        /// <summary>今の Editor の process の環境（子へ継承する元）。</summary>
        internal Func<IReadOnlyDictionary<string, string>> InheritedEnvironment { get; set; } = () => new Dictionary<string, string>();
        /// <summary>Player の log（<c>-logFile</c>）の path を 1 つ作る。</summary>
        internal Func<string> NewPlayerLogPath { get; set; } = () => throw new InvalidOperationException("NewPlayerLogPath is not set.");
        internal Func<PlayerLaunchRequest, Process> StartProcess { get; set; } = _ => throw new InvalidOperationException("StartProcess is not set.");
        internal Action<PlayerOverlayAttempt> WriteAttempt { get; set; } = _ => { };
        /// <summary>process を作った後の観測を始める（製品は <see cref="PlayerOverlayStartupObserver.Begin"/>）。</summary>
        internal Action BeginObserving { get; set; } = () => { };
        internal Action<string> Info { get; set; } = _ => { };
        internal Action<string> Warn { get; set; } = _ => { };
        /// <summary>止めた理由を Console と dialog に出す。</summary>
        internal Action<string> Stop { get; set; } = _ => { };
    }

    /// <summary>入口の 1 回の結果。<see cref="Process"/> は process を作った時だけ（判定の test が exit code を読み、閉じる要求を送る）。</summary>
    internal sealed class PlayerOverlayLaunchResult
    {
        internal PlayerOverlayLaunchResult(PlayerOverlayAttempt attempt, Process? process) { Attempt = attempt; Process = process; }
        internal PlayerOverlayAttempt Attempt { get; }
        internal Process? Process { get; }
        internal PlayerOverlayOutcome Outcome => Enum.TryParse<PlayerOverlayOutcome>(Attempt.outcome, out var value) ? value : PlayerOverlayOutcome.EntryFailed;
    }

    /// <summary>
    /// 遊べる Player に手元の編集の上書きを 1 つ重ねて起動する入口（menu 1 つ、D6）。順は、Player を決める → base を決める →
    /// 未保存の物を保存の確認に通す → 保存の後の disk で Player の入力の指紋を照合する → 上書きの段（準備・計画・認め・再利用か build）
    /// → digest を取り直す → 環境を組み立てて Player を起動する → 観測を始める。
    /// <para>
    /// 止めるのは「入れた物が信用できない」時だけで、base だけの起動に縮退しない。content の編集が無い時だけ上書きの key なしで起動する
    /// （照合の失敗の縮退ではないことを Console と記録の <c>outcome</c> で分ける）。入口は base・installed cache・Player の出力・source・
    /// 既存の上書きの directory・Delivery の ledger を書き換えず、Editor の Play の入口の状態も読まない。書くのは新しい上書きの artifacts と
    /// <c>artifacts/playable/overlays</c> の記録だけである。
    /// </para>
    /// <para>規則は既存の port と <see cref="PlayablePlayerLocator"/>・<see cref="PlayerCompatibility"/> にあり、ここは順だけを持つ。</para>
    /// </summary>
    internal static class PlayerOverlayLaunch
    {
        internal const string MenuPath = "Tools/OSM/Content/Play Playable Player With Local Edits";
        internal const string NoOverlayNotice = "上書きなし（content の編集なし）";
        private const string LogPrefix = "[PlayerOverlay] ";
        private const string DialogTitle = "Play Playable Player With Local Edits";

        [MenuItem(MenuPath)]
        private static void Menu() => Run(ProductPorts());

        internal static PlayerOverlayLaunchResult Run(PlayerOverlayLaunchPorts ports)
        {
            if (ports == null) throw new ArgumentNullException(nameof(ports));
            var attempt = new PlayerOverlayAttempt();

            // (1) Player。最新の 1 つだけを使い、古い Player へ戻らない。
            var player = ports.LocatePlayer();
            if (!player.IsFound) return Stopped(ports, attempt, PlayerOverlayOutcome.NoPlayablePlayer, player.Failure);
            attempt.playerIdentity = player.Identity;
            ports.Info(LogPrefix + "Player: " + player.Identity + " (" + player.Directory + ")");

            // (2) base。Player の identity と transport の digest に合う install だけ。
            var resolution = ports.ResolveBase(player);
            if (resolution.Install == null)
                return Stopped(ports, attempt, resolution.IsNotInstalled ? PlayerOverlayOutcome.BaseNotInstalled : PlayerOverlayOutcome.BaseInvalid,
                    resolution.Failure);
            var install = resolution.Install;

            // (3) 上書きは disk の file から作るので、未保存の物を先に確認に通す。0 件なら確認を呼ばない（batch の dialog は false を返す）。
            if (!ports.ConfirmSaveScenes()) return Stopped(ports, attempt, PlayerOverlayOutcome.Cancelled, "Saving the modified scenes was cancelled.");
            var unsaved = ports.EnumerateUnsavedAssets();
            if (unsaved.Count != 0)
            {
                if (!ports.ConfirmSaveAssets(unsaved))
                    return Stopped(ports, attempt, PlayerOverlayOutcome.Cancelled, "Saving the modified assets was cancelled: " + string.Join(", ", unsaved));
                ports.SaveAssets();
            }

            // (4) 指紋は保存の後の disk で照合する（保存の前に照合すると、Inspector で変えた Player の入力が照合の後の保存ですり抜ける）。
            //     上書きの準備（Prepare）より前に置き、設定の違う exe に上書きを作らない。
            try
            {
                PlayerCompatibilityRecord compatibility;
                try { compatibility = ports.ReadPlayerCompatibility(player.Directory); }
                catch (Exception ex) when (ex is IOException or FormatException or ArgumentException or UnauthorizedAccessException)
                {
                    return Stopped(ports, attempt, PlayerOverlayOutcome.NoPlayablePlayer,
                        "The Player's " + PlayerCompatibility.FileName + " cannot be read: " + ex.Message + ". " + PlayablePlayerLocator.BuildGuidance);
                }
                var differences = PlayerCompatibility.Compare(compatibility, ports.CollectPlayerInputs());
                if (differences.Count != 0)
                    return Stopped(ports, attempt, PlayerOverlayOutcome.PlayerInputDiffers,
                        "Player inputs other than content differ from the Player " + player.Identity + ": " + string.Join(", ", differences) +
                        ". Build the playable Player again. If Unity only rewrote a file (for example the URP global settings on the first open of a new checkout), restore it.");
            }
            catch (Exception ex)
            {
                return Stopped(ports, attempt, PlayerOverlayOutcome.EntryFailed, "Collecting the Player inputs failed: " + ex.Message);
            }

            // (4) 上書きの段。再利用の照合が通っても、記録の digest と今の digest が違えば build し直させる（Run の後では build の段に戻れない）。
            PlayEntryOverlayResult overlay;
            try
            {
                var overlayPorts = ports.OverlayPorts(install);
                var reuse = overlayPorts.AcknowledgeAndReuse;
                overlayPorts.AcknowledgeAndReuse = () => VerifiedReuse(ports, reuse());
                overlay = PlayEntryOverlay.Run(overlayPorts);
            }
            catch (Exception ex)
            {
                return Stopped(ports, attempt, PlayerOverlayOutcome.EntryFailed, "The overlay could not be planned or built: " + ex.Message);
            }
            attempt.overlayRoots = overlay.OverlayRootKeys.ToArray();
            foreach (var warning in PlayEntryOverlay.Warnings(overlay)) ports.Warn(LogPrefix + warning);

            var artifact = overlay.Artifact;
            if (artifact != null)
            {
                attempt.hasOverlay = true;
                attempt.overlayIdentity = artifact.Identity;
                attempt.overlayPath = artifact.ContentPath;
                attempt.reuse = overlay.State.ToString();
                string recorded;
                try
                {
                    if (overlay.State == PlayEntryOverlayState.Built)
                    {
                        // build の時の digest を残す（Editor の再起動をまたいで再利用の照合に使う）。記録は Run が Built を返した後に入口が書く。
                        recorded = ports.ComputeDigest(artifact.ContentPath);
                        ports.WriteBuildRecord(new PlayerOverlayBuildRecord { identity = artifact.Identity, contentPath = artifact.ContentPath, digest = recorded });
                    }
                    else
                    {
                        recorded = ports.ReadBuildRecord(artifact.Identity)?.digest
                                   ?? throw new InvalidOperationException("The reused overlay has no build record: " + artifact.Identity);
                    }
                    // (5) 起動の直前に取り直す。違えば process を作らない（Player に渡す digest は記録の値で、取り直した値ではない）。
                    var current = ports.ComputeDigest(artifact.ContentPath);
                    if (!string.Equals(current, recorded, StringComparison.Ordinal))
                        return Stopped(ports, attempt, PlayerOverlayOutcome.OverlayChangedAfterBuild,
                            "The overlay directory changed after it was built: " + artifact.ContentPath + " (recorded " + recorded + ", now " + current + ").");
                }
                catch (Exception ex)
                {
                    return Stopped(ports, attempt, PlayerOverlayOutcome.EntryFailed, "The overlay digest could not be taken: " + ex.Message);
                }
                attempt.overlayDigest = recorded;
                ports.BeforeProcessStart(artifact.ContentPath);
            }
            else
            {
                ports.Info(LogPrefix + NoOverlayNotice + ": the Player starts on the installed base only (" + overlay.State + ").");
            }

            // (6) 子の環境。継承した SAMPLEGAME_* を全て消し、installed の pair と（あれば）上書きの 3 key だけを置く。
            var environment = BuildEnvironment(ports.InheritedEnvironment(), install.RevisionRoot, install.ManifestSha256,
                artifact == null ? null : (artifact.ContentPath, artifact.Identity, attempt.overlayDigest));
            attempt.environmentNames = environment.Keys.Where(IsSampleGameVariable).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            attempt.playerLogPath = ports.NewPlayerLogPath();
            Process process;
            try
            {
                process = ports.StartProcess(new PlayerLaunchRequest(Path.Combine(player.Directory, "SampleGame.exe"),
                    "-logFile \"" + attempt.playerLogPath + "\"", environment));
            }
            catch (Exception ex)
            {
                return Stopped(ports, attempt, PlayerOverlayOutcome.EntryFailed, "The Player process could not be started: " + ex.Message);
            }

            attempt.outcome = (artifact == null ? PlayerOverlayOutcome.LaunchedWithoutOverlay : PlayerOverlayOutcome.Launched).ToString();
            attempt.startup = nameof(PlayerOverlayStartup.Pending);
            attempt.launchedUtcTicks = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            attempt.pid = process.Id;
            try { attempt.processStartUtcTicks = process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { attempt.processStartUtcTicks = string.Empty; }
            ports.WriteAttempt(attempt);
            ports.BeginObserving();
            ports.Info(LogPrefix + attempt.outcome + ": Player " + player.Identity +
                       (artifact == null ? string.Empty : " with overlay " + artifact.Identity + " (" + attempt.reuse + ")") +
                       ". Player log: " + attempt.playerLogPath);
            return new PlayerOverlayLaunchResult(attempt, process);
        }

        /// <summary>
        /// 既存の再利用の照合が返した上書きを、入口の記録の digest と今の digest で確かめる。記録が無い・読めない・digest が違えば null
        /// （既存の build の段へ進ませる）。
        /// </summary>
        internal static PlayEntryOverlayArtifact? VerifiedReuse(PlayerOverlayLaunchPorts ports, PlayEntryOverlayArtifact? reused)
        {
            if (reused == null || !reused.IsSuccess) return reused;
            var record = ports.ReadBuildRecord(reused.Identity);
            if (record == null) return null;
            return string.Equals(ports.ComputeDigest(reused.ContentPath), record.digest, StringComparison.Ordinal) ? reused : null;
        }

        /// <summary>
        /// 子 process の環境。継承した値から <c>SAMPLEGAME_</c> で始まる物を全て消し（Editor の Play が残しうる runtimeMode・firstScene・
        /// playerKind や古い上書きの key を Player へ渡さない）、installed の pair と、上書きがあれば path・identity・digest を置く。
        /// protected の値、初回 Scene、runtime の既定値、Editor の snapshot の印は置かない。
        /// </summary>
        internal static Dictionary<string, string> BuildEnvironment(IReadOnlyDictionary<string, string> inherited, string revisionRoot, string manifestSha256,
            (string Path, string Identity, string Digest)? overlay)
        {
            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in inherited)
                if (!IsSampleGameVariable(pair.Key)) environment[pair.Key] = pair.Value;
            environment[PurposeLaunchSettings.EnvironmentName("content:installedRevisionPath")] = revisionRoot;
            environment[PurposeLaunchSettings.EnvironmentName("content:manifestSha256")] = manifestSha256;
            if (overlay != null)
            {
                environment[PurposeLaunchSettings.EnvironmentName(ContentOverlayPlayConfig.OverlayPathKey)] = overlay.Value.Path;
                environment[PurposeLaunchSettings.EnvironmentName(ContentOverlayPlayConfig.OverlayIdentityKey)] = overlay.Value.Identity;
                environment[PurposeLaunchSettings.EnvironmentName(ContentOverlayPlayConfig.OverlayDigestKey)] = overlay.Value.Digest;
            }
            return environment;
        }

        private static bool IsSampleGameVariable(string name) =>
            name.StartsWith(PurposeLaunchSettings.EnvironmentPrefix, StringComparison.OrdinalIgnoreCase);

        private static PlayerOverlayLaunchResult Stopped(PlayerOverlayLaunchPorts ports, PlayerOverlayAttempt attempt, PlayerOverlayOutcome outcome, string detail)
        {
            attempt.outcome = outcome.ToString();
            attempt.detail = detail;
            attempt.startup = nameof(PlayerOverlayStartup.NotStarted);
            ports.WriteAttempt(attempt);
            ports.Stop(LogPrefix + outcome + ": " + detail);
            return new PlayerOverlayLaunchResult(attempt, null);
        }

        /// <summary>製品の port。判定の test は必要な port だけを差し替えて使う（<see cref="PlayerOverlayLaunchPorts.BeforeProcessStart"/> 等）。</summary>
        internal static PlayerOverlayLaunchPorts ProductPorts()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var playableRoot = SampleGamePlayerBuildCoordinator.ArtifactsRootOf(projectRoot, PlayerBuildKind.Playable);
            var declaration = SampleGameBuildPurposes.SpringFull;
            return new PlayerOverlayLaunchPorts
            {
                LocatePlayer = () => PlayablePlayerLocator.Locate(Path.Combine(playableRoot, "players")),
                ResolveBase = player => PlayEntryCandidates.ResolvePlayerBase(
                    VerifyPlayLauncher.ResolveCacheRoot(ContentDeliveryWindow.LoadSettings().cacheRoot), declaration, VerifyPlayLauncher.Target,
                    player.Identity, player.TransportManifestSha256, Path.Combine(playableRoot, "transport", player.Identity)),
                ConfirmSaveScenes = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo,
                // 検出は Editor の Play の入口と共有し、確認の dialog と応答はこの入口の物にする（先置きの応答の key を取り合わない）。
                EnumerateUnsavedAssets = PlayEntryInterceptor.EnumerateUnsavedAssets,
                ConfirmSaveAssets = unsaved => EditorUtility.DisplayDialog(DialogTitle,
                    "These assets have unsaved changes. The overlay is built from the files on disk:\n" + string.Join("\n", unsaved),
                    "Save and continue", "Cancel"),
                SaveAssets = () => { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); },
                ReadPlayerCompatibility = directory => PlayerCompatibility.FromJson(File.ReadAllText(Path.Combine(directory, PlayerCompatibility.FileName))),
                CollectPlayerInputs = PlayerInputFiles.Collect,
                // Player には snapshot の供給元が無いので、開いている Scene の編集も上書きに入れる（suppliedSceneKeys は空）。
                OverlayPorts = install => SampleGameContentBuild.OverlayPorts(declaration, install, Array.Empty<string>(),
                    SampleGameContentBuild.PlayerOverlayContentSetOf(declaration), () => EditorApplication.timeSinceStartup),
                ReadBuildRecord = identity => PlayerOverlayRecords.ReadBuildRecord(PlayerOverlayRecords.BuildRecordPath(playableRoot, identity), identity),
                WriteBuildRecord = record => PlayerOverlayRecords.WriteBuildRecord(PlayerOverlayRecords.BuildRecordPath(playableRoot, record.identity), record),
                InheritedEnvironment = CurrentEnvironment,
                NewPlayerLogPath = () =>
                {
                    var logs = Path.Combine(PlayerOverlayRecords.OverlaysRoot(playableRoot), "logs");
                    Directory.CreateDirectory(logs);
                    return Path.Combine(logs, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture) + "-" +
                                              Guid.NewGuid().ToString("N").Substring(0, 8) + ".log");
                },
                StartProcess = StartPlayer,
                WriteAttempt = attempt => PlayerOverlayRecords.WriteAttempt(PlayerOverlayRecords.LastAttemptPath(playableRoot), attempt),
                BeginObserving = PlayerOverlayStartupObserver.Begin,
                Info = Debug.Log,
                Warn = Debug.LogWarning,
                Stop = message =>
                {
                    Debug.LogError(message);
                    if (!Application.isBatchMode) EditorUtility.DisplayDialog(DialogTitle, message, "OK");
                },
            };
        }

        private static IReadOnlyDictionary<string, string> CurrentEnvironment()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                values[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
            return values;
        }

        /// <summary>窓つきで起動する（-batchmode なし）。環境は要求の値で置き換える（Editor の環境を足し合わせない）。</summary>
        private static Process StartPlayer(PlayerLaunchRequest request)
        {
            var info = new ProcessStartInfo(request.Executable, request.Arguments)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(request.Executable)!,
            };
            info.Environment.Clear();
            foreach (var pair in request.Environment) info.Environment[pair.Key] = pair.Value;
            return Process.Start(info) ?? throw new InvalidOperationException("Process.Start returned no process: " + request.Executable);
        }
    }
}
