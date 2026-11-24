#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Editor.ContentOverlay;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.BuildContent.Distribution;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.Editor.Build;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// 遊べる Player に手元の上書きを重ねる入口の判定の test（受け入れ条件 AC1〜AC18 の Player の起動を要する物）。
    /// <para>
    /// 全て <c>[Explicit]</c> で通常の回帰に混ぜない。1 つの case を 1 つの Unity process で走らせる（driver <c>tools/s42-player-overlay.ps1</c>
    /// が順と編集の patch を持つ）。名前の先頭の X は Player を build する checkout、Y は上書きを作る別の checkout で走らせる。
    /// 出力は <c>TestResults/s42/&lt;S42_RUN_ID&gt;/&lt;case&gt;/</c> と <c>record.jsonl</c>。XML の合格は record を書き終えたことだけを示し、
    /// 受け入れ条件の合否は record・Player の log・game の log・<c>last-attempt.json</c>・画像で判定の担当が読む（この test は合否を判定しない）。
    /// </para>
    /// <para>
    /// 入口は製品の port（<see cref="PlayerOverlayLaunch.ProductPorts"/>）で呼ぶ。差し替えるのは、AC10 の観測の時点を取る起動の包みと、
    /// AC18 (b) の <see cref="PlayerOverlayLaunchPorts.BeforeProcessStart"/> だけである（製品が自分で行う事を test が代わりに用意しない）。
    /// 同期の case の待ちの間は <c>EditorApplication.update</c> が回らないので、待ちの刻みで製品の観測の口
    /// （<see cref="PlayerOverlayStartupObserver.ObserveOnce"/>）を呼ぶ。AC18 だけは frame を譲り、観測を製品の update に任せる。
    /// </para>
    /// </summary>
    public sealed class PlayerOverlayEndToEndTests
    {
        private const int TestTimeoutMs = 7200000;
        private const string LocalDirectoryName = "OneStarMakerLocal";
        private const string ReceiptName = "bs4-smoke-receipt.json";
        private const string LoadedMessage = "Loaded.";
        private const string HomeMessage = "OnStabledImpl HomeScene";
        /// <summary>AC16 の patch が map の <c>_generateHash</c> に書く値。</summary>
        internal const string MapToken = "s42-ac16-map-token";
        private static readonly TimeSpan StartupLimit = TimeSpan.FromSeconds(PlayerOverlayStartupObserver.TimeoutSeconds);
        private static readonly TimeSpan CloseLimit = TimeSpan.FromSeconds(120);
        private static readonly string[] GameLogProbes = { LoadedMessage, HomeMessage };
        private static readonly string[] PlayerLogProbes =
        {
            "[AppInit] Content directory registered:", ContentOverlayPlayConfig.RegisteredLogMarker, "[AppInit] Bootstrap UICommon:",
            "[AppInit] Bootstrap SceneResourceMap:", ContentOverlayPlayConfig.PlayerFirstSceneLogMarker, PlayerProcessRun.QuittingLine,
            ContentOverlayStartup.DigestMismatchReason, "Content overlay on a Player needs", "Content overlay needs both",
            "Content overlay can only be layered on a verified installed revision", "The overlay base record does not match the registered base",
            "Content overlay is only available in Editor Play Mode.", "BeforeSceneLoad", "AfterSceneLoad failed",
        };

        // ─── X: Player を build する checkout ───

        /// <summary>AC17 の 1 つ目の時点: build が返った直後（同じ Unity process）の手元の指紋と Player の compat を比べる。run の写しは AC14 が使う。</summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void X1_BuildPlayableAndCompareInputs()
        {
            var record = Begin(nameof(X1_BuildPlayableAndCompareInputs));
            var result = PlayerKindEndToEndTests.BuildAndCopy(PlayerBuildKind.Playable);
            record.playerIdentity = result.Identity;
            RecordCompatibility(record, result.PlayerDirectory);
            RecordCheckout(record);
            Finish(record);
        }

        /// <summary>AC17 の 2 つ目の時点: build の後の新しい Unity process で同じ比較をする。</summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void X2_CompareInputsInANewProcess()
        {
            var record = Begin(nameof(X2_CompareInputsInANewProcess));
            var player = PlayablePlayerLocator.Locate(PlayersRoot());
            if (!player.IsFound) Assert.Fail(player.Failure);
            record.playerIdentity = player.Identity;
            RecordCompatibility(record, player.Directory);
            Finish(record);
        }

        /// <summary>
        /// AC2: 動作確認の Player に上書きの 3 key を渡すと before-scene-load の失敗の receipt と exit 1。加えて動作確認の Player の出力に
        /// compat が無いこと、2 つの build の後に mutation の root が残っていないこと（run の写しは BuildSmoke と X1 が作る）。
        /// </summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void X3_SmokeRejectsOverlayKeys()
        {
            var record = Begin(nameof(X3_SmokeRejectsOverlayKeys));
            var s41 = S41RunDirectory();
            var smoke = Path.Combine(s41, "smoke", "player");
            if (!File.Exists(Path.Combine(smoke, "SampleGame.exe"))) Assert.Fail("Run PlayerKindEndToEndTests.BuildSmoke first: " + smoke);
            record.Note("smokeCompatPresent", File.Exists(Path.Combine(smoke, PlayerCompatibility.FileName)).ToString());
            var projectRoot = ProjectRoot();
            var bs4 = Path.Combine(projectRoot, "Assets", "Resources", "BS4");
            record.Note("mutationRootsRemaining", Directory.Exists(bs4) ? string.Join(",", Directory.GetDirectories(bs4).Select(Path.GetFileName)) : "");
            record.Note("runtimeConfigRemaining", File.Exists(Path.Combine(projectRoot, "Assets", "StreamingAssets", "bs4-runtime.json")).ToString());
            var overlay = Path.Combine(record.CaseDirectory, "no-overlay");
            DirectLaunch(record, smoke, batch: true, new Dictionary<string, string>
            {
                [Env(ContentOverlayPlayConfig.OverlayPathKey)] = overlay,
                [Env(ContentOverlayPlayConfig.OverlayIdentityKey)] = "s42-smoke-overlay",
                [Env(ContentOverlayPlayConfig.OverlayDigestKey)] = new string('0', 64),
            }, run => run.WaitForExit(TimeSpan.FromSeconds(120), "exit"));
            Finish(record);
        }

        // ─── Y: 上書きを作る checkout（X の Player と transport を artifacts/playable へ写した後） ───

        /// <summary>AC8 (d)・AC10: install の前に入口を実行すると BaseNotInstalled で process を作らない。</summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y01_EntryBeforeInstall()
        {
            var record = Begin(nameof(Y01_EntryBeforeInstall));
            var before = Snapshot();
            var result = PlayerOverlayLaunch.Run(ProductPorts(record));
            record.SetAttempt(result.Attempt);
            record.snapshotsUnchanged = Compare(record, before, Snapshot());
            Assert.That(result.Process, Is.Null, "the entry started a Player before the base was installed");
            Finish(record);
        }

        /// <summary>X の transport を Delivery の install の口で、Y の設定の cache に install する（install は disk に残る）。</summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public async Task Y02_InstallTransport()
        {
            var record = Begin(nameof(Y02_InstallTransport));
            var player = PlayablePlayerLocator.Locate(PlayersRoot());
            if (!player.IsFound) Assert.Fail(player.Failure);
            var transport = Path.Combine(PlayableRoot(), "transport", player.Identity);
            var digest = Sha256(Path.Combine(transport, "transport.json"));
            var manifest = JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(Path.Combine(transport, "transport.json")));
            var settings = ContentDeliveryWindow.LoadSettings();
            var stages = new VerifyPlayLauncher.ReceivedStages("unused", () => null, false, SampleGameBuildPurposes.FirstSceneOf, settings);
            var installed = await stages.InstallAsync(VerifyPlayLauncher.ToPublished(transport, digest, manifest), CancellationToken.None);
            record.playerIdentity = player.Identity;
            record.Note("cacheRoot", stages.CacheRoot);
            record.Note("configuredCacheRoot", settings.cacheRoot);
            record.Note("revisionRoot", installed.RevisionRoot);
            record.Note("manifestSha256", installed.ManifestSha256);
            Finish(record);
        }

        /// <summary>AC8 (a)(e)・AC10: 編集の無い Y で入口を実行すると上書きなしで base から起動し、ledger と Editor の環境を変えない。</summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y03_EntryWithoutEdits()
        {
            var record = Begin(nameof(Y03_EntryWithoutEdits));
            var ledgerBefore = SessionState.GetString(ContentDeliveryLedgerKey, "<unset>");
            var environmentBefore = SampleGameEnvironment();
            var before = Snapshot();
            EntryLaunch(record, before);
            record.Note("ledgerUnchanged", (SessionState.GetString(ContentDeliveryLedgerKey, "<unset>") == ledgerBefore).ToString());
            record.Note("editorSampleGameEnvironmentUnchanged", SampleGameEnvironment().SequenceEqual(environmentBefore).ToString());
            Finish(record);
        }

        /// <summary>
        /// AC1・AC10・AC11: Title の 2 つの token の patch を当てた Y で入口を実行する。Title の画像を取り、判定の実行担当が「スタート」を
        /// 1 回押すのを Home の行まで 180 秒待ってから閉じる。
        /// </summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y04_TitleEdits()
        {
            var record = Begin(nameof(Y04_TitleEdits));
            RecordCheckout(record);
            var before = Snapshot();
            EntryLaunch(record, before, run =>
            {
                record.screenshots.Add(CaptureWindow(run.Process, Path.Combine(record.CaseDirectory, "title.png")));
                File.WriteAllText(Path.Combine(record.CaseDirectory, "press-start.txt"), "Click Start once in the Player window (pid " + run.Process.Id + ").");
                TestContext.Progress.WriteLine("[S42] Title captured. Click Start once in the Player window (pid " + run.Process.Id + ").");
                record.Note("homeObserved", run.WaitUntil(() => GameLogHas(record.PlayerDirectory, HomeMessage), TimeSpan.FromSeconds(180), "home").ToString());
            });
            Finish(record);
        }

        /// <summary>
        /// AC18: 製品の観測の経路（EditorApplication.update）が動く。test は観測の口を呼ばず、frame を譲って <c>last-attempt.json</c> を外から読むだけ。
        /// (a) 成功の起動、(b) 入口の照合の後に上書きの 1 file を書き換えて Player が digest の不一致で止まる起動。
        /// </summary>
        [UnityTest, Explicit, Timeout(TestTimeoutMs)]
        public IEnumerator Y05_ProductObservation()
        {
            var record = Begin(nameof(Y05_ProductObservation));
            // (a)
            var ports = ProductPorts(record);
            var a = PlayerOverlayLaunch.Run(ports);
            record.Note("a.outcome", a.Attempt.outcome);
            if (a.Process != null)
            {
                yield return WaitForProductObservation(record, "a");
                CopyEvidence(record, "a", a.Attempt.playerLogPath);
                Close(record, a.Process, "a");
            }
            MoveGameLog(record, "a");

            // (b) digest の取り直しの後・process を作る前に、その起動が使う上書きの 1 file を書き換える。
            ports = ProductPorts(record);
            ports.BeforeProcessStart = overlayPath =>
            {
                var file = Directory.GetFiles(overlayPath, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal).First();
                File.AppendAllText(file, "s42-ac18b");
                record.Note("b.rewrittenFile", file);
            };
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(ContentOverlayStartup.DigestMismatchReason)));
            var b = PlayerOverlayLaunch.Run(ports);
            record.Note("b.outcome", b.Attempt.outcome);
            if (b.Process != null)
            {
                yield return WaitForProductObservation(record, "b");
                CopyEvidence(record, "b", b.Attempt.playerLogPath);
                var exited = b.Process.WaitForExit((int)CloseLimit.TotalMilliseconds);
                record.Note("b.exitCode", exited ? b.Process.ExitCode.ToString() : "running");
                if (!exited) { b.Process.Kill(); b.Process.WaitForExit(); }
            }
            MoveGameLog(record, "b");
            Finish(record);
        }

        /// <summary>AC4 (e)(i): 入口を実行して上書きを build する。</summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y06_ReuseBuilt() => ReuseStep(nameof(Y06_ReuseBuilt), addFile: false);

        /// <summary>AC4 (e)(ii): 編集を変えずに別の Unity process で入口を実行すると前の上書きを再利用する。</summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y07_ReuseReused() => ReuseStep(nameof(Y07_ReuseReused), addFile: false);

        /// <summary>AC4 (e)(iii): 前の上書きの content に file を 1 つ足してから同じ編集で入口を実行すると build し直す。</summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y08_ReuseAfterAddingAFile() => ReuseStep(nameof(Y08_ReuseAfterAddingAFile), addFile: true);

        /// <summary>
        /// AC4 (b)(c)(d): 入口が作った上書き（前の case の記録）を使い、環境変数で Player を直に起動する。(b) 1 file を書き換えて古い digest、
        /// (c) digest の key なし、(d) digest の key だけ。(b) の書き換えは起動の後に元の bytes へ戻す（後の case の再利用を壊さない）。
        /// </summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y09_DirectDigestRejections()
        {
            var record = Begin(nameof(Y09_DirectDigestRejections));
            var previous = PlayerOverlayRecords.ReadAttempt(LastAttemptPath()) ?? throw new AssertionException("No previous entry record.");
            if (!previous.hasOverlay || !Directory.Exists(previous.overlayPath)) Assert.Fail("The previous entry made no overlay: " + previous.overlayPath);
            var (player, install) = PlayerAndBase();
            var pair = Pair(install);

            var file = Directory.GetFiles(previous.overlayPath, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal).First();
            var original = File.ReadAllBytes(file);
            try
            {
                File.AppendAllText(file, "s42-ac4b");
                DirectLaunch(record.Sub("b"), player.Directory, batch: false, With(pair, previous.overlayPath, previous.overlayIdentity, previous.overlayDigest),
                    run => run.WaitForExit(StartupLimit, "exit"));
            }
            finally { File.WriteAllBytes(file, original); }
            record.Note("b.restoredDigestMatches", (ContentOverlayPlayConfig.ComputeOverlayDigest(previous.overlayPath) == previous.overlayDigest).ToString());

            DirectLaunch(record.Sub("c"), player.Directory, batch: false, With(pair, previous.overlayPath, previous.overlayIdentity, null),
                run => run.WaitForExit(StartupLimit, "exit"));
            var digestOnly = new Dictionary<string, string>(pair) { [Env(ContentOverlayPlayConfig.OverlayDigestKey)] = previous.overlayDigest };
            DirectLaunch(record.Sub("d"), player.Directory, batch: false, digestOnly, run => run.WaitForExit(StartupLimit, "exit"));
            Finish(record);
        }

        /// <summary>
        /// AC13: 上書きつきの Player A を起動し、A が Observed になった後に閉じずに編集（UICommon の UXML の patch）を足して入口をもう一度実行し、
        /// 新しい上書きの Player B を起動する。A の上書きの directory が変わらないことを見て、A と B を閉じる。game の log は合否に使わない。
        /// </summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y10_SecondPlayerWhileFirstRuns()
        {
            var record = Begin(nameof(Y10_SecondPlayerWhileFirstRuns));
            var a = PlayerOverlayLaunch.Run(ProductPorts(record.Sub("a")));
            record.Sub("a").SetAttempt(a.Attempt);
            if (a.Process == null) { Finish(record); return; }
            var runA = new PlayerProcessRun(a.Process, a.Attempt.playerLogPath, record.CaseDirectory, () => PlayerOverlayStartupObserver.ObserveOnce());
            runA.WaitForLine(ContentOverlayPlayConfig.PlayerFirstSceneLogMarker, StartupLimit, "first-scene");
            var observedA = PlayerOverlayStartupObserver.ObserveOnce() ?? a.Attempt;
            record.Sub("a").SetAttempt(observedA);
            CopyEvidence(record, "a", a.Attempt.playerLogPath);
            var aFilesBefore = Hashes(a.Attempt.overlayPath);

            ApplyPatchInsideTheTest(record, "ac12-uicommon.patch");
            var b = PlayerOverlayLaunch.Run(ProductPorts(record.Sub("b")));
            if (b.Process != null)
            {
                var runB = new PlayerProcessRun(b.Process, b.Attempt.playerLogPath, record.CaseDirectory, () => PlayerOverlayStartupObserver.ObserveOnce());
                runB.WaitForLine(ContentOverlayPlayConfig.PlayerFirstSceneLogMarker, StartupLimit, "first-scene");
                record.Sub("b").SetAttempt(PlayerOverlayStartupObserver.ObserveOnce() ?? b.Attempt);
                CopyEvidence(record, "b", b.Attempt.playerLogPath);
                record.Note("aOverlayUnchanged", Hashes(a.Attempt.overlayPath).SequenceEqual(aFilesBefore).ToString());
                Close(record, b.Process, "b");
            }
            else
            {
                record.Sub("b").SetAttempt(b.Attempt);
            }
            Close(record, a.Process, "a");
            MoveGameLog(record, "ab");
            Finish(record);
        }

        /// <summary>AC12・AC16: UICommon の UXML の token と map の hash の token の 2 つの patch を当てた 1 回の起動。Title の画像を取る。</summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y11_UiCommonAndMapEdits()
        {
            var record = Begin(nameof(Y11_UiCommonAndMapEdits));
            record.Note("expectedMapLine", "[AppInit] Bootstrap SceneResourceMap: source=content hash=" + MapToken);
            EntryLaunch(record, null,
                run => record.screenshots.Add(CaptureWindow(run.Process, Path.Combine(record.CaseDirectory, "title.png"))));
            Finish(record);
        }

        /// <summary>AC15: <c>ProjectSettings/TagManager.asset</c> に tag を足した Y で入口を実行すると PlayerInputDiffers で止まる。</summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y12_PlayerInputEdit()
        {
            var record = Begin(nameof(Y12_PlayerInputEdit));
            var result = PlayerOverlayLaunch.Run(ProductPorts(record));
            record.SetAttempt(result.Attempt);
            Assert.That(result.Process, Is.Null, "the entry started a Player with differing Player inputs");
            Finish(record);
        }

        /// <summary>AC5: Title の閉包の prefab が使う script に field を足した Y で入口を実行する（Player は build の時の script で動く）。</summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y13_ScriptFieldEdit()
        {
            var record = Begin(nameof(Y13_ScriptFieldEdit));
            EntryLaunch(record, null);
            Finish(record);
        }

        /// <summary>
        /// AC3: 同じ checkout で spring-full をもう 1 回 build・publish・install した別の identity の base に対して上書きを作り、その上書きの正しい
        /// digest と Player の base の pair を環境変数で Player へ渡す（digest は通り、base の記録で落ちる）。上書きを作るための編集は、別の base を
        /// build した後に test の中で当てる（先に当てると別の base が編集を含み、上書きが要らなくなる）。
        /// </summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public async Task Y14_OverlayForAnotherBase()
        {
            var record = Begin(nameof(Y14_OverlayForAnotherBase));
            var declaration = SampleGameBuildPurposes.SpringFull;
            var other = SampleGameContentBuild.BuildResult(declaration, BuildContentMode.Full);
            var transport = ContentTransportPublisher.Publish(other, Path.Combine(PlayableRoot(), "ac3-transport", other.Identity));
            var digest = Sha256(Path.Combine(transport, "transport.json"));
            var manifest = JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(Path.Combine(transport, "transport.json")));
            var stages = new VerifyPlayLauncher.ReceivedStages("unused", () => null, false, SampleGameBuildPurposes.FirstSceneOf, ContentDeliveryWindow.LoadSettings());
            await stages.InstallAsync(VerifyPlayLauncher.ToPublished(transport, digest, manifest), CancellationToken.None);
            var otherBase = PlayEntryCandidates.ResolvePlayerBase(stages.CacheRoot, declaration, VerifyPlayLauncher.Target, other.Identity, digest, transport);
            if (otherBase.Install == null) Assert.Fail("The other base did not resolve: " + otherBase.Failure);
            record.Note("otherBaseIdentity", other.Identity);

            ApplyPatchInsideTheTest(record, "ac12-uicommon.patch");
            var overlay = PlayEntryOverlay.Run(SampleGameContentBuild.OverlayPorts(declaration, otherBase.Install!, Array.Empty<string>(),
                SampleGameContentBuild.PlayerOverlayContentSetOf(declaration), () => EditorApplication.timeSinceStartup));
            record.Note("overlayState", overlay.State.ToString());
            if (overlay.Artifact == null) Assert.Fail("No overlay was made against the other base: " + overlay.State);
            var overlayDigest = ContentOverlayPlayConfig.ComputeOverlayDigest(overlay.Artifact!.ContentPath);
            var (player, install) = PlayerAndBase();
            DirectLaunch(record, player.Directory, batch: false, With(Pair(install), overlay.Artifact.ContentPath, overlay.Artifact.Identity, overlayDigest),
                run => run.WaitForExit(StartupLimit, "exit"));
            Finish(record);
        }

        /// <summary>AC7: 遊べる Player を installed の pair なし（同梱の directory）で、上書きの 3 key つきで起動する。</summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y15_PairlessPlayer()
        {
            var record = Begin(nameof(Y15_PairlessPlayer));
            var player = PlayablePlayerLocator.Locate(PlayersRoot());
            if (!player.IsFound) Assert.Fail(player.Failure);
            var previous = PlayerOverlayRecords.ReadAttempt(LastAttemptPath());
            var overlayPath = previous?.hasOverlay == true ? previous.overlayPath : Path.Combine(record.CaseDirectory, "no-overlay");
            DirectLaunch(record, player.Directory, batch: false, With(new Dictionary<string, string>(), overlayPath,
                previous?.overlayIdentity is { Length: > 0 } id ? id : "s42-overlay", previous?.overlayDigest is { Length: > 0 } d ? d : new string('0', 64)),
                run => run.WaitForExit(StartupLimit, "exit"));
            Finish(record);
        }

        /// <summary>
        /// AC8 (b): Editor の process の環境に古い上書きの 3 key と Editor の Play が残しうる key を置いた状態で、編集の無い Y の入口を実行する。
        /// 子の環境の掃除は入口に任せ、記録の <c>environmentNames</c> で読む。
        /// </summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y16_InheritedEnvironment()
        {
            var record = Begin(nameof(Y16_InheritedEnvironment));
            var planted = new Dictionary<string, string>
            {
                [Env(ContentOverlayPlayConfig.OverlayPathKey)] = @"D:\stale-overlay",
                [Env(ContentOverlayPlayConfig.OverlayIdentityKey)] = "stale-overlay",
                [Env(ContentOverlayPlayConfig.OverlayDigestKey)] = new string('1', 64),
                [Env("content:runtimeMode")] = "directory",
                [Env("content:firstScene")] = "Title",
                [Env("content:playerKind")] = "smoke",
            };
            var saved = planted.Keys.ToDictionary(x => x, Environment.GetEnvironmentVariable);
            try
            {
                foreach (var pair in planted) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
                EntryLaunch(record, null);
            }
            finally
            {
                foreach (var pair in saved) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
            Finish(record);
        }

        /// <summary>
        /// AC8 (c): 作り直せない root だけの計画。base にある root の Scene の file（と .meta）を Assets の外へ退かして入口を実行する。
        /// 退かした file は case の終わりに戻す（driver も git checkout で戻す）。
        /// </summary>
        [Test, Explicit, Timeout(TestTimeoutMs)]
        public void Y17_UnbuildableOnly()
        {
            var record = Begin(nameof(Y17_UnbuildableOnly));
            var scene = PickNonBootstrapScene();
            record.Note("movedScene", scene);
            var aside = Path.Combine(record.CaseDirectory, "aside");
            Directory.CreateDirectory(aside);
            var full = Path.Combine(ProjectRoot(), scene);
            File.Move(full, Path.Combine(aside, Path.GetFileName(full)));
            File.Move(full + ".meta", Path.Combine(aside, Path.GetFileName(full) + ".meta"));
            AssetDatabase.Refresh();
            // 退かした Scene が Addressables の entry なら、Refresh で Addressables が group から entry を外し、group の asset が
            // 未保存になる。入口はそれを未保存の asset として保存の確認に通し、batch の dialog は Cancel を返すので、AC8 (c) の
            // 初期状態（Assets/ の Dirty が 0 件）にならない。退かした状態を disk に保存してから、0 件であることを確かめる。
            // 保存した group の file は driver が case の後の git checkout で戻す。
            AssetDatabase.SaveAssets();
            var unsaved = PlayEntryInterceptor.EnumerateUnsavedAssets().ToArray();
            record.Note("unsavedAfterMove", string.Join(" ", unsaved));
            if (unsaved.Length != 0) Assert.Fail("AC8 (c) needs no unsaved assets before the entry runs: " + string.Join(", ", unsaved));
            try
            {
                EntryLaunch(record, null);
            }
            finally
            {
                File.Move(Path.Combine(aside, Path.GetFileName(full)), full);
                File.Move(Path.Combine(aside, Path.GetFileName(full) + ".meta"), full + ".meta");
                AssetDatabase.Refresh();
            }
            Finish(record);
        }

        // ─── 入口の起動と観測 ───

        private const string ContentDeliveryLedgerKey = "OSM.ContentDelivery.PlayBridge.v1";

        /// <summary>
        /// 製品の port で入口を実行し、Player を作ったら初回 Scene の行・game の log の Title の Loaded. を待つ（刻みで製品の観測の口を呼ぶ）。
        /// <paramref name="before"/> があれば、process を作る直前に AC10 の観測を取る。
        /// </summary>
        private static void EntryLaunch(CaseRecord record, SnapshotSet? before, Action<PlayerProcessRun>? afterLoaded = null)
        {
            var ports = ProductPorts(record);
            if (before != null)
            {
                var start = ports.StartProcess;
                ports.StartProcess = request =>
                {
                    record.snapshotsUnchanged = Compare(record, before, Snapshot());
                    return start(request);
                };
            }
            var result = PlayerOverlayLaunch.Run(ports);
            record.SetAttempt(result.Attempt);
            if (result.Process == null) return;
            record.PlayerDirectory = PlayablePlayerLocator.Locate(PlayersRoot()).Directory;
            using var process = result.Process;
            var run = new PlayerProcessRun(process, result.Attempt.playerLogPath, record.CaseDirectory, () => PlayerOverlayStartupObserver.ObserveOnce());
            try
            {
                if (run.WaitForLine(ContentOverlayPlayConfig.PlayerFirstSceneLogMarker, StartupLimit, "first-scene"))
                {
                    record.SetAttempt(PlayerOverlayStartupObserver.ObserveOnce() ?? result.Attempt);
                    record.Note("titleLoaded", run.WaitUntil(() => GameLogHas(record.PlayerDirectory, LoadedMessage), TimeSpan.FromSeconds(120), "loaded").ToString());
                    afterLoaded?.Invoke(run);
                    run.RequestClose();
                    run.WaitForExit(CloseLimit, "close");
                }
                else
                {
                    record.SetAttempt(PlayerOverlayStartupObserver.ObserveOnce() ?? result.Attempt);
                }
            }
            finally
            {
                if (!process.HasExited) { record.killed = true; try { process.Kill(); } catch (InvalidOperationException) { } process.WaitForExit(); }
                record.exitCode = process.ExitCode;
                record.closeMessageSent = run.CloseMessageSent;
                record.timeout = run.Timeout;
                record.timeoutPhase = run.TimeoutPhase;
                run.ReadLog();
                record.playerLog = Probe(run.LogText);
                CopyEvidence(record, "", result.Attempt.playerLogPath);
                MoveGameLog(record, "");
            }
        }

        /// <summary>AC4 (e) の 1 段。前の段の上書きの content に file を足す時は入口の前に足す。</summary>
        private static void ReuseStep(string caseName, bool addFile)
        {
            var record = Begin(caseName);
            if (addFile)
            {
                var previous = PlayerOverlayRecords.ReadAttempt(LastAttemptPath()) ?? throw new AssertionException("No previous entry record.");
                if (!previous.hasOverlay) Assert.Fail("The previous entry made no overlay.");
                var added = Path.Combine(previous.overlayPath, "s42-ac4e-added.txt");
                File.WriteAllText(added, "added by " + caseName);
                record.Note("addedFile", added);
                record.Note("previousOverlayIdentity", previous.overlayIdentity);
            }
            EntryLaunch(record, null);
            Finish(record);
        }

        private static IEnumerator WaitForProductObservation(CaseRecord record, string stage)
        {
            var clock = Stopwatch.StartNew();
            PlayerOverlayAttempt? attempt;
            do
            {
                yield return null;
                attempt = PlayerOverlayRecords.ReadAttempt(LastAttemptPath());
            } while (attempt != null && attempt.Startup == PlayerOverlayStartup.Pending && clock.Elapsed < StartupLimit + TimeSpan.FromSeconds(30));
            if (attempt != null) record.Sub(stage).SetAttempt(attempt);
            record.Note(stage + ".observedWithinSeconds", clock.Elapsed.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void Close(CaseRecord record, Process process, string stage)
        {
            var run = new PlayerProcessRun(process, string.Empty, string.Empty);
            run.RequestClose();
            record.Note(stage + ".closeMessageSent", run.CloseMessageSent.ToString());
            var exited = process.WaitForExit((int)CloseLimit.TotalMilliseconds);
            if (!exited) { record.killed = true; process.Kill(); process.WaitForExit(); }
            record.Note(stage + ".exitCode", process.ExitCode.ToString());
        }

        /// <summary>
        /// 環境変数で Player を直に起動する（入口を通さない case）。継承した <c>SAMPLEGAME_*</c> を消し、渡した値だけを置く。
        /// game の log と receipt は case の folder へ移す。
        /// </summary>
        private static void DirectLaunch(CaseRecord record, string playerDirectory, bool batch, IReadOnlyDictionary<string, string> environment, Action<PlayerProcessRun> drive)
        {
            record.PlayerDirectory = playerDirectory;
            if (Directory.Exists(Path.Combine(playerDirectory, LocalDirectoryName)) || File.Exists(Path.Combine(playerDirectory, ReceiptName)))
                Assert.Fail("A previous case left its game log or receipt in the Player folder: " + playerDirectory);
            Directory.CreateDirectory(record.CaseDirectory);
            var logPath = Path.Combine(record.CaseDirectory, "player.log");
            var info = new ProcessStartInfo(Path.Combine(playerDirectory, "SampleGame.exe"),
                (batch ? "-batchmode " : "") + "-logFile \"" + logPath + "\"") { UseShellExecute = false, WorkingDirectory = playerDirectory };
            foreach (var key in info.EnvironmentVariables.Keys.Cast<string>().Where(k => k.StartsWith("SAMPLEGAME_", StringComparison.OrdinalIgnoreCase)).ToArray())
                info.EnvironmentVariables.Remove(key);
            foreach (var pair in environment) info.EnvironmentVariables[pair.Key] = pair.Value;
            record.Note("directEnvironment", string.Join(";", environment.Keys.OrderBy(x => x, StringComparer.Ordinal)));
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Player process did not start.");
            var run = new PlayerProcessRun(process, logPath, record.CaseDirectory);
            try { drive(run); }
            finally
            {
                if (!process.HasExited) { record.killed = true; try { process.Kill(); } catch (InvalidOperationException) { } process.WaitForExit(); }
                record.exitCode = process.ExitCode;
                record.timeout = run.Timeout;
                record.timeoutPhase = run.TimeoutPhase;
                run.ReadLog();
                record.playerLog = Probe(run.LogText);
                var receipt = Path.Combine(playerDirectory, ReceiptName);
                if (File.Exists(receipt))
                {
                    record.receipt = File.ReadAllText(receipt);
                    File.Move(receipt, Path.Combine(record.CaseDirectory, ReceiptName));
                }
                MoveGameLog(record, "");
            }
        }

        private static PlayerOverlayLaunchPorts ProductPorts(CaseRecord record)
        {
            var ports = PlayerOverlayLaunch.ProductPorts();
            // batch には dialog が無い。止めた理由は Console と記録に残る。
            ports.Stop = message => { record.Note("stopMessage", message); Debug.LogWarning(message); };
            return ports;
        }

        // ─── AC10 の観測 ───

        private sealed class SnapshotSet
        {
            public SortedDictionary<string, string> Base = new(StringComparer.Ordinal), Player = new(StringComparer.Ordinal), Overlays = new(StringComparer.Ordinal);
            public string[] GitStatus = Array.Empty<string>();
        }

        /// <summary>base の installed revision の tree（cache の制御の file を除く）、Player の folder（OneStarMakerLocal を除く）、既にある上書きの directory、git status。</summary>
        private static SnapshotSet Snapshot()
        {
            var set = new SnapshotSet();
            var player = PlayablePlayerLocator.Locate(PlayersRoot());
            if (player.IsFound)
            {
                foreach (var pair in Hashes(player.Directory).Where(x => !x.Key.StartsWith(LocalDirectoryName + "/", StringComparison.Ordinal)))
                    set.Player[pair.Key] = pair.Value;
                var resolution = PlayEntryCandidates.ResolvePlayerBase(VerifyPlayLauncher.ResolveCacheRoot(ContentDeliveryWindow.LoadSettings().cacheRoot),
                    SampleGameBuildPurposes.SpringFull, VerifyPlayLauncher.Target, player.Identity, player.TransportManifestSha256, "");
                if (resolution.Install != null) set.Base = Hashes(resolution.Install.RevisionRoot);
            }
            var content = Path.Combine(SampleGameContentBuild.ArtifactsRoot, "content");
            if (Directory.Exists(content))
                foreach (var directory in Directory.GetDirectories(content))
                    foreach (var pair in Hashes(directory)) set.Overlays[Path.GetFileName(directory) + "/" + pair.Key] = pair.Value;
            set.GitStatus = Git("status --porcelain");
            return set;
        }

        /// <summary>前後の違いを record に書き、全て同じなら真。新しく増えた上書きの directory は数えない（入口が書いてよい物）。</summary>
        private static bool Compare(CaseRecord record, SnapshotSet before, SnapshotSet after)
        {
            var baseSame = before.Base.SequenceEqual(after.Base);
            var playerSame = before.Player.SequenceEqual(after.Player);
            var overlaysSame = before.Overlays.All(x => after.Overlays.TryGetValue(x.Key, out var v) && v == x.Value);
            var gitSame = before.GitStatus.SequenceEqual(after.GitStatus);
            record.Note("ac10.baseUnchanged", baseSame.ToString());
            record.Note("ac10.playerUnchanged", playerSame.ToString());
            record.Note("ac10.existingOverlaysUnchanged", overlaysSame.ToString());
            record.Note("ac10.gitStatusBefore", string.Join(" | ", before.GitStatus));
            record.Note("ac10.gitStatusAfter", string.Join(" | ", after.GitStatus));
            return baseSame && playerSame && overlaysSame && gitSame;
        }

        // ─── record ───

        [Serializable]
        private sealed class CaseRecord
        {
            public string runId = "", caseName = "", playerIdentity = "", startedUtc = "", endedUtc = "";
            public string attempt = "", outcome = "", startup = "", startupReason = "", detail = "", receipt = "";
            public string[] overlayRoots = Array.Empty<string>();
            public string[] environmentNames = Array.Empty<string>();
            public int exitCode = int.MinValue;
            public bool killed, timeout, closeMessageSent, snapshotsUnchanged;
            public string timeoutPhase = "";
            public List<string> notes = new();
            public List<string> screenshots = new();
            public ProbeHit[] playerLog = Array.Empty<ProbeHit>();
            public PlayerGameLogLine[] gameLog = Array.Empty<PlayerGameLogLine>();
            public List<CaseRecord> stages = new();

            [NonSerialized] public string CaseDirectory = "";
            [NonSerialized] public string PlayerDirectory = "";

            internal void Note(string key, string? value) => notes.Add(key + "=" + (value ?? "<null>"));

            internal void SetAttempt(PlayerOverlayAttempt value)
            {
                attempt = JsonUtility.ToJson(value);
                outcome = value.outcome; startup = value.startup; startupReason = value.startupReason; detail = value.detail;
                overlayRoots = value.overlayRoots; environmentNames = value.environmentNames;
                if (playerIdentity.Length == 0) playerIdentity = value.playerIdentity;
            }

            /// <summary>1 つの case の中の段（同じ case の folder を使い、record の中に入れ子で残す）。</summary>
            internal CaseRecord Sub(string name)
            {
                var existing = stages.FirstOrDefault(x => x.caseName == name);
                if (existing != null) return existing;
                var stage = new CaseRecord { runId = runId, caseName = name, CaseDirectory = Path.Combine(CaseDirectory, name) };
                stages.Add(stage);
                return stage;
            }
        }

        [Serializable]
        private sealed class ProbeHit
        {
            public string probe = "", text = "";
            public int line;
        }

        private static CaseRecord Begin(string caseName)
        {
            var run = RunDirectory();
            var caseDirectory = Path.Combine(run, caseName);
            if (Directory.Exists(caseDirectory)) Assert.Fail(caseName + " already ran in this run. Re-run every step with another S42_RUN_ID.");
            Directory.CreateDirectory(caseDirectory);
            return new CaseRecord { runId = Path.GetFileName(run), caseName = caseName, CaseDirectory = caseDirectory, startedUtc = PlayerProcessRun.Utc(DateTime.UtcNow) };
        }

        private static void Finish(CaseRecord record)
        {
            record.endedUtc = PlayerProcessRun.Utc(DateTime.UtcNow);
            var attemptPath = LastAttemptPath();
            if (File.Exists(attemptPath)) File.Copy(attemptPath, Path.Combine(record.CaseDirectory, "last-attempt.json"), true);
            var json = JsonUtility.ToJson(record, true);
            File.WriteAllText(Path.Combine(record.CaseDirectory, "record.json"), json);
            File.AppendAllText(Path.Combine(RunDirectory(), "record.jsonl"), JsonUtility.ToJson(record, false) + "\n");
        }

        /// <summary>段ごとの <c>last-attempt.json</c> と Player の log を、次の段が上書きする前に case の folder へ写す。</summary>
        private static void CopyEvidence(CaseRecord record, string stage, string playerLogPath)
        {
            var target = stage.Length == 0 ? record.CaseDirectory : Path.Combine(record.CaseDirectory, stage);
            Directory.CreateDirectory(target);
            if (File.Exists(LastAttemptPath())) File.Copy(LastAttemptPath(), Path.Combine(target, "last-attempt.json"), true);
            if (File.Exists(playerLogPath))
            {
                using var source = new FileStream(playerLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var copy = File.Create(Path.Combine(target, "player.log"));
                source.CopyTo(copy);
            }
        }

        /// <summary>Player の folder の game の log を case の folder へ移し、record に読む（起動の前に無いことは入口の前に確かめる）。</summary>
        private static void MoveGameLog(CaseRecord record, string stage)
        {
            var player = record.PlayerDirectory.Length != 0 ? record.PlayerDirectory : PlayablePlayerLocator.Locate(PlayersRoot()).Directory;
            if (player.Length == 0) return;
            var local = Path.Combine(player, LocalDirectoryName);
            if (!Directory.Exists(local)) return;
            var target = Path.Combine(record.CaseDirectory, stage.Length == 0 ? LocalDirectoryName : stage + "-" + LocalDirectoryName);
            Directory.Move(local, target);
            record.gameLog = PlayerProcessRun.ReadGameLog(Path.Combine(target, "Log"), "", GameLogProbes);
        }

        private static bool GameLogHas(string playerDirectory, string message) =>
            PlayerProcessRun.ReadGameLog(Path.Combine(playerDirectory, LocalDirectoryName, "Log"), "", GameLogProbes).Any(x => x.message.Contains(message));

        private static ProbeHit[] Probe(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var hits = new List<ProbeHit>();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var isError = line.Contains("Exception") || line.Contains("Error");
                var stripped = line.StartsWith("Shader 'Hidden/Universal Render Pipeline/", StringComparison.Ordinal)
                               && line.Contains("is not supported or has been stripped from the build");
                foreach (var probe in PlayerLogProbes.Where(line.Contains))
                    hits.Add(new ProbeHit { probe = probe, line = i + 1, text = line });
                if (isError && !stripped) hits.Add(new ProbeHit { probe = "<error-like>", line = i + 1, text = line });
            }
            return hits.ToArray();
        }

        // ─── checkout と入力の観測 ───

        private static void RecordCompatibility(CaseRecord record, string playerDirectory)
        {
            var compatibility = PlayerCompatibility.FromJson(File.ReadAllText(Path.Combine(playerDirectory, PlayerCompatibility.FileName)));
            var differences = PlayerCompatibility.Compare(compatibility, PlayerInputFiles.Collect());
            record.Note("compatFileCount", compatibility.files.Length.ToString());
            record.Note("compatDifferenceCount", differences.Count.ToString());
            foreach (var difference in differences) record.Note("compatDifference", difference);
        }

        /// <summary>AC11: この checkout の Library の path と Unity の版。</summary>
        private static void RecordCheckout(CaseRecord record)
        {
            record.Note("libraryPath", Path.Combine(ProjectRoot(), "Library"));
            record.Note("unityVersion", Application.unityVersion);
            record.Note("gitHead", string.Join("", Git("rev-parse HEAD")));
        }

        private static string PickNonBootstrapScene()
        {
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(SampleGameBootstrapContentComposer.SceneResourceMapPath)
                      ?? throw new AssertionException("SceneResourceMap is missing.");
            var firstScene = SampleGameBuildPurposes.FirstSceneOf(SampleGameBuildPurposes.SpringFull.Name);
            foreach (var resource in map.SceneResources.Where(x => x != null).Reverse())
            {
                if (resource.Identity == firstScene || resource.Identity == SampleGameBootstrapContentComposer.UiCommonLogicalKey) continue;
                var path = AssetDatabase.FindAssets(resource.Identity + " t:Scene").Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == resource.Identity && x.StartsWith("Assets/", StringComparison.Ordinal));
                if (path != null) return path;
            }
            throw new AssertionException("No non-bootstrap scene file was found in the SceneResourceMap.");
        }

        /// <summary>
        /// 実行中の test の中で patch を当てる（AC13・AC3）。Unity の外の file の書き換えと Refresh で、script の変更は含めない
        /// （compile と domain reload で test が切れる）。
        /// </summary>
        private static void ApplyPatchInsideTheTest(CaseRecord record, string patchName)
        {
            var patch = Path.Combine(ProjectRoot(), "Assets", "SampleGame", "Tests", "Editor", "Build", "PlayerOverlayEdits", patchName);
            var output = Git("apply --whitespace=nowarn \"" + patch + "\"");
            record.Note("appliedPatch", patchName + " " + string.Join(" ", output));
            AssetDatabase.Refresh();
        }

        // ─── paths and helpers ───

        private static string ProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string PlayableRoot() => SampleGamePlayerBuildCoordinator.ArtifactsRootOf(ProjectRoot(), PlayerBuildKind.Playable);
        private static string PlayersRoot() => Path.Combine(PlayableRoot(), "players");
        private static string LastAttemptPath() => PlayerOverlayRecords.LastAttemptPath(PlayableRoot());
        private static string Env(string key) => PurposeLaunchSettings.EnvironmentName(key);

        private static string RunDirectory()
        {
            var runId = Environment.GetEnvironmentVariable("S42_RUN_ID");
            if (string.IsNullOrWhiteSpace(runId) || runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                Assert.Fail("Set S42_RUN_ID to the same file-name-safe value for every step of one run.");
            var run = Path.GetFullPath(Path.Combine(ProjectRoot(), "..", "TestResults", "s42", runId!));
            Directory.CreateDirectory(run);
            return run;
        }

        private static string S41RunDirectory()
        {
            var runId = Environment.GetEnvironmentVariable("S41_RUN_ID");
            if (string.IsNullOrWhiteSpace(runId)) Assert.Fail("Set S41_RUN_ID to the run of PlayerKindEndToEndTests.BuildSmoke.");
            return Path.GetFullPath(Path.Combine(ProjectRoot(), "..", "TestResults", "s41", runId!));
        }

        private static (PlayablePlayerChoice Player, ContentInstallResult Install) PlayerAndBase()
        {
            var player = PlayablePlayerLocator.Locate(PlayersRoot());
            if (!player.IsFound) Assert.Fail(player.Failure);
            var resolution = PlayEntryCandidates.ResolvePlayerBase(VerifyPlayLauncher.ResolveCacheRoot(ContentDeliveryWindow.LoadSettings().cacheRoot),
                SampleGameBuildPurposes.SpringFull, VerifyPlayLauncher.Target, player.Identity, player.TransportManifestSha256, "");
            if (resolution.Install == null) Assert.Fail("The Player's base is not installed: " + resolution.Failure);
            return (player, resolution.Install!);
        }

        private static Dictionary<string, string> Pair(ContentInstallResult install) => new()
        {
            [Env("content:installedRevisionPath")] = install.RevisionRoot,
            [Env("content:manifestSha256")] = install.ManifestSha256,
        };

        private static Dictionary<string, string> With(Dictionary<string, string> pair, string path, string identity, string? digest)
        {
            var values = new Dictionary<string, string>(pair)
            {
                [Env(ContentOverlayPlayConfig.OverlayPathKey)] = path,
                [Env(ContentOverlayPlayConfig.OverlayIdentityKey)] = identity,
            };
            if (digest != null) values[Env(ContentOverlayPlayConfig.OverlayDigestKey)] = digest;
            return values;
        }

        private static string[] SampleGameEnvironment() =>
            Environment.GetEnvironmentVariables().Keys.Cast<string>()
                .Where(x => x.StartsWith(PurposeLaunchSettings.EnvironmentPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(x => x + "=" + Environment.GetEnvironmentVariable(x)).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        private static SortedDictionary<string, string> Hashes(string directory)
        {
            var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (!Directory.Exists(directory)) return hashes;
            var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetFullPath(file).Substring(root.Length).Replace('\\', '/');
                if (relative.StartsWith("control/", StringComparison.Ordinal)) continue;
                hashes[relative] = Sha256(file);
            }
            return hashes;
        }

        private static string Sha256(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(stream).Select(x => x.ToString("x2")));
        }

        private static string[] Git(string arguments)
        {
            var info = new ProcessStartInfo("git", arguments)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                WorkingDirectory = Path.GetFullPath(Path.Combine(ProjectRoot(), "..")),
            };
            using var process = Process.Start(info) ?? throw new InvalidOperationException("git did not start.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("git " + arguments + " failed: " + error);
            return output.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // ─── 窓の画像（画面の DC から窓の矩形を写す） ───

        /// <summary>Player の主窓を前に出し、画面の DC から窓の矩形を PNG に写す。写せなければ理由を返す（判定の担当が shell から取る代わりの手段がある）。</summary>
        private static string CaptureWindow(Process process, string path)
        {
            process.Refresh();
            var window = process.MainWindowHandle;
            if (window == IntPtr.Zero) return "no-main-window";
            SetForegroundWindow(window);
            if (!GetWindowRect(window, out var rect)) return "no-window-rect";
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return "empty-window-rect";
            var screen = GetDC(IntPtr.Zero);
            var memory = CreateCompatibleDC(screen);
            var bitmap = CreateCompatibleBitmap(screen, width, height);
            var old = SelectObject(memory, bitmap);
            try
            {
                if (!BitBlt(memory, 0, 0, width, height, screen, rect.Left, rect.Top, SrcCopy | CaptureBlt)) return "bitblt-failed";
                var header = new BitmapInfoHeader
                {
                    biSize = (uint)Marshal.SizeOf<BitmapInfoHeader>(), biWidth = width, biHeight = height, biPlanes = 1, biBitCount = 32, biCompression = 0,
                };
                var pixels = new byte[width * height * 4];
                SelectObject(memory, old);
                if (GetDIBits(memory, bitmap, 0, (uint)height, pixels, ref header, 0) == 0) return "getdibits-failed";
                // 正の高さの DIB は下の行から並ぶ（Texture2D の行の順と同じ）。
                var texture = new Texture2D(width, height, TextureFormat.BGRA32, false);
                try
                {
                    texture.LoadRawTextureData(pixels);
                    texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                return path;
            }
            finally
            {
                DeleteObject(bitmap);
                DeleteDC(memory);
                ReleaseDC(IntPtr.Zero, screen);
            }
        }

        private const uint SrcCopy = 0x00CC0020;
        private const uint CaptureBlt = 0x40000000;

        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
        }

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
        [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BitmapInfoHeader info, uint usage);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    }
}
