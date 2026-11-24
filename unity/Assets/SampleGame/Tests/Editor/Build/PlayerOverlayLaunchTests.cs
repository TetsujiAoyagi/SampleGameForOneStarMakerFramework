#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;
using SampleGame.DependOnAll.Editor.Build;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// 入口の順と停止（D6）、再利用の包み（D8）、子 process の環境（D6 の (6)）を、port を差し替えて確かめる。Player は起動しない
    /// （process を作る port は今の process を返すだけ）。
    /// </summary>
    public sealed class PlayerOverlayLaunchTests
    {
        private const string PlayerId = "20261124T010000000Z-play";
        private const string OverlayId = "20261124T020000000Z-over";
        private const string OverlayPath = @"D:\artifacts\bs2b\content\over";

        private readonly List<string> _calls = new();
        private PlayerOverlayAttempt? _written;
        private PlayerLaunchRequest? _launched;

        private static ContentInstallResult Install() =>
            new(@"D:\cache\installed\spring-full\" + PlayerId, @"D:\cache\installed\spring-full\" + PlayerId + @"\content",
                new ValidatedContentManifest("manifest-sha", new ContentTransportManifest { contentSet = "spring-full", revision = PlayerId, target = "t" },
                    Array.Empty<ContentTransportFile>(), 0), true);

        private static readonly PlayerInputFileEntry[] Inputs = { new("ProjectSettings/TagManager.asset", "aa"), new("Packages/manifest.json", "bb") };

        private static PlayablePlayerChoice Player() =>
            PlayablePlayerLocator.Choose(new[] { new PlayablePlayerCandidate(PlayerId, @"D:\players\" + PlayerId, PlayerId, "transport-sha", true, null) });

        /// <summary>全段が通る port。overlay は計画の状態と再利用・build の返り値を選べる。</summary>
        private PlayerOverlayLaunchPorts Ports(PlayEntryOverlayPlanState plan = PlayEntryOverlayPlanState.BuildRequired,
            PlayEntryOverlayArtifact? reuse = null, Func<string, string>? digest = null, IReadOnlyList<PlayerInputFileEntry>? local = null)
        {
            var overlay = new PlayEntryOverlayPorts
            {
                Prepare = () => _calls.Add("Prepare"),
                Plan = () => { _calls.Add("Plan"); return new PlayEntryOverlayPlanSummary(plan, 1, Array.Empty<PlayEntryUnbuildableRoot>(), new[] { "Title" }); },
                AcknowledgeAndReuse = () => { _calls.Add("Reuse"); return reuse; },
                AcknowledgeAndBuild = () => { _calls.Add("Build"); return new PlayEntryOverlayArtifact(true, OverlayId, OverlayPath, "", null); },
            };
            return new PlayerOverlayLaunchPorts
            {
                LocatePlayer = () => { _calls.Add("Locate"); return Player(); },
                ResolveBase = _ => { _calls.Add("Base"); return PlayerBaseResolution.Resolved(Install()); },
                ConfirmSaveScenes = () => { _calls.Add("SaveScenes"); return true; },
                EnumerateUnsavedAssets = () => Array.Empty<string>(),
                ConfirmSaveAssets = _ => { _calls.Add("ConfirmAssets"); return true; },
                SaveAssets = () => _calls.Add("SaveAssets"),
                ReadPlayerCompatibility = _ => PlayerCompatibility.CreateRecord(PlayerId, Inputs),
                CollectPlayerInputs = () => { _calls.Add("Collect"); return local ?? Inputs; },
                OverlayPorts = _ => overlay,
                ComputeDigest = digest ?? (_ => "digest-1"),
                ReadBuildRecord = id => new PlayerOverlayBuildRecord { identity = id, digest = "digest-1" },
                WriteBuildRecord = record => _calls.Add("WriteRecord:" + record.digest),
                BeforeProcessStart = path => _calls.Add("BeforeStart"),
                InheritedEnvironment = () => new Dictionary<string, string>
                {
                    ["PATH"] = @"C:\bin",
                    ["SAMPLEGAME_CONTENT__RUNTIMEMODE"] = "directory",
                    ["samplegame_content__firstScene"] = "Title",
                    ["SAMPLEGAME_EDITOR__CONTENTOVERLAYPATH"] = @"D:\old",
                },
                NewPlayerLogPath = () => @"D:\logs\p.log",
                StartProcess = request => { _calls.Add("Start"); _launched = request; return Process.GetCurrentProcess(); },
                WriteAttempt = attempt => _written = attempt,
                BeginObserving = () => _calls.Add("Observe"),
            };
        }

        [Test]
        public void BuiltOverlay_Launches_WithThePairAndTheThreeOverlayKeysOnly()
        {
            var result = PlayerOverlayLaunch.Run(Ports());
            Assert.That(result.Outcome, Is.EqualTo(PlayerOverlayOutcome.Launched), result.Attempt.detail);
            Assert.That(result.Process, Is.Not.Null);
            Assert.That(_calls, Is.EqualTo(new[]
            {
                "Locate", "Base", "SaveScenes", "Collect", "Prepare", "Plan", "Reuse", "Build", "WriteRecord:digest-1", "BeforeStart", "Start", "Observe",
            }));
            var attempt = _written!;
            Assert.That(attempt.startup, Is.EqualTo(nameof(PlayerOverlayStartup.Pending)));
            Assert.That(attempt.reuse, Is.EqualTo("Built"));
            Assert.That(attempt.overlayRoots, Is.EqualTo(new[] { "Title" }));
            Assert.That(attempt.overlayDigest, Is.EqualTo("digest-1"));
            Assert.That(attempt.playerIdentity, Is.EqualTo(PlayerId));
            Assert.That(attempt.pid, Is.EqualTo(Process.GetCurrentProcess().Id));
            Assert.That(attempt.environmentNames, Is.EqualTo(new[]
            {
                "SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH", "SAMPLEGAME_CONTENT__MANIFESTSHA256",
                "SAMPLEGAME_EDITOR__CONTENTOVERLAYDIGEST", "SAMPLEGAME_EDITOR__CONTENTOVERLAYIDENTITY", "SAMPLEGAME_EDITOR__CONTENTOVERLAYPATH",
            }));
            var env = _launched!.Environment;
            Assert.That(env["PATH"], Is.EqualTo(@"C:\bin"), "SAMPLEGAME_ 以外の継承は残す");
            Assert.That(env["SAMPLEGAME_EDITOR__CONTENTOVERLAYPATH"], Is.EqualTo(OverlayPath), "古い上書きの key を継承しない");
            Assert.That(env["SAMPLEGAME_EDITOR__CONTENTOVERLAYDIGEST"], Is.EqualTo("digest-1"));
            Assert.That(env["SAMPLEGAME_CONTENT__MANIFESTSHA256"], Is.EqualTo("manifest-sha"));
            Assert.That(_launched.Executable, Is.EqualTo(Path.Combine(@"D:\players\" + PlayerId, "SampleGame.exe")));
            StringAssert.Contains(@"-logFile ""D:\logs\p.log""", _launched.Arguments);
        }

        [Test]
        public void NoEdits_LaunchesWithoutOverlayKeys()
        {
            var result = PlayerOverlayLaunch.Run(Ports(PlayEntryOverlayPlanState.NotRequired));
            Assert.That(result.Outcome, Is.EqualTo(PlayerOverlayOutcome.LaunchedWithoutOverlay));
            Assert.That(_written!.hasOverlay, Is.False);
            Assert.That(_written.environmentNames, Is.EqualTo(new[] { "SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH", "SAMPLEGAME_CONTENT__MANIFESTSHA256" }));
            Assert.That(_calls, Does.Not.Contain("BeforeStart"));
            Assert.That(PlayerOverlayLaunch.Run(Ports(PlayEntryOverlayPlanState.UnbuildableOnly)).Outcome, Is.EqualTo(PlayerOverlayOutcome.LaunchedWithoutOverlay));
        }

        [Test]
        public void SavingChangesAPlayerInput_StopsAsPlayerInputDiffers_BeforePrepareAndLaunch()
        {
            // 保存の port が指紋に入る file を書き換える: 照合は保存の後なので、書き換えた後の disk で止まる。
            var disk = Inputs.ToList();
            var ports = Ports(local: disk);
            ports.EnumerateUnsavedAssets = () => new[] { "Assets/Settings/PC_RPAsset.asset" };
            ports.SaveAssets = () => { _calls.Add("SaveAssets"); disk[0] = new PlayerInputFileEntry("ProjectSettings/TagManager.asset", "changed"); };
            var result = PlayerOverlayLaunch.Run(ports);
            Assert.That(result.Outcome, Is.EqualTo(PlayerOverlayOutcome.PlayerInputDiffers));
            StringAssert.Contains("ProjectSettings/TagManager.asset (changed)", result.Attempt.detail);
            Assert.That(result.Attempt.startup, Is.EqualTo(nameof(PlayerOverlayStartup.NotStarted)));
            Assert.That(_calls, Does.Not.Contain("Prepare").And.Not.Contain("Start"));
            Assert.That(_calls.IndexOf("SaveAssets"), Is.LessThan(_calls.IndexOf("Collect")));
            Assert.That(result.Process, Is.Null);
        }

        [Test]
        public void OverlayChangedBetweenTheRecordAndTheRecheck_StopsWithoutAProcess()
        {
            var calls = 0;
            var result = PlayerOverlayLaunch.Run(Ports(digest: _ => ++calls == 1 ? "digest-1" : "digest-2"));
            Assert.That(result.Outcome, Is.EqualTo(PlayerOverlayOutcome.OverlayChangedAfterBuild));
            Assert.That(_calls, Does.Contain("WriteRecord:digest-1").And.Not.Contain("Start").And.Not.Contain("BeforeStart"));
            Assert.That(result.Process, Is.Null);
        }

        [Test]
        public void ReuseWrapper_KeepsTheReuseOnlyWhenTheRecordedDigestMatches()
        {
            var reused = new PlayEntryOverlayArtifact(true, OverlayId, OverlayPath, "", null);
            var ports = Ports();
            Assert.That(PlayerOverlayLaunch.VerifiedReuse(ports, reused), Is.SameAs(reused));
            Assert.That(PlayerOverlayLaunch.VerifiedReuse(ports, null), Is.Null);
            ports.ReadBuildRecord = _ => null;
            Assert.That(PlayerOverlayLaunch.VerifiedReuse(ports, reused), Is.Null, "記録が無い・壊れている");
            ports.ReadBuildRecord = id => new PlayerOverlayBuildRecord { identity = id, digest = "other" };
            Assert.That(PlayerOverlayLaunch.VerifiedReuse(ports, reused), Is.Null, "digest が違う");
        }

        [Test]
        public void Reused_LaunchesWithoutBuilding_AndRecordsReused()
        {
            var result = PlayerOverlayLaunch.Run(Ports(reuse: new PlayEntryOverlayArtifact(true, OverlayId, OverlayPath, "", null)));
            Assert.That(result.Outcome, Is.EqualTo(PlayerOverlayOutcome.Launched));
            Assert.That(_written!.reuse, Is.EqualTo("Reused"));
            Assert.That(_calls, Does.Not.Contain("Build"));
        }

        [Test]
        public void ReuseWithAChangedDirectory_FallsThroughToBuild()
        {
            var ports = Ports(reuse: new PlayEntryOverlayArtifact(true, "old-overlay", @"D:\old", "", null));
            ports.ReadBuildRecord = id => new PlayerOverlayBuildRecord { identity = id, digest = id == "old-overlay" ? "stale" : "digest-1" };
            var result = PlayerOverlayLaunch.Run(ports);
            Assert.That(result.Outcome, Is.EqualTo(PlayerOverlayOutcome.Launched));
            Assert.That(_written!.reuse, Is.EqualTo("Built"));
            Assert.That(_written.overlayIdentity, Is.EqualTo(OverlayId));
        }

        [Test]
        public void Stops_ForNoPlayer_NoBase_InvalidBase_CancelAndPlanFailure()
        {
            var ports = Ports();
            ports.LocatePlayer = () => PlayablePlayerLocator.Choose(Array.Empty<PlayablePlayerCandidate>());
            Assert.That(PlayerOverlayLaunch.Run(ports).Outcome, Is.EqualTo(PlayerOverlayOutcome.NoPlayablePlayer));

            ports = Ports();
            ports.ResolveBase = _ => PlayerBaseResolution.NotInstalled("Install it.");
            var notInstalled = PlayerOverlayLaunch.Run(ports);
            Assert.That(notInstalled.Outcome, Is.EqualTo(PlayerOverlayOutcome.BaseNotInstalled));
            Assert.That(notInstalled.Attempt.detail, Is.EqualTo("Install it."));
            Assert.That(notInstalled.Attempt.startup, Is.EqualTo(nameof(PlayerOverlayStartup.NotStarted)));

            ports = Ports();
            ports.ResolveBase = _ => PlayerBaseResolution.Invalid("bad");
            Assert.That(PlayerOverlayLaunch.Run(ports).Outcome, Is.EqualTo(PlayerOverlayOutcome.BaseInvalid));

            ports = Ports();
            ports.ConfirmSaveScenes = () => false;
            Assert.That(PlayerOverlayLaunch.Run(ports).Outcome, Is.EqualTo(PlayerOverlayOutcome.Cancelled));

            ports = Ports();
            ports.EnumerateUnsavedAssets = () => new[] { "Assets/x.asset" };
            ports.ConfirmSaveAssets = _ => false;
            Assert.That(PlayerOverlayLaunch.Run(ports).Outcome, Is.EqualTo(PlayerOverlayOutcome.Cancelled));

            _calls.Clear();
            ports = Ports();
            ports.OverlayPorts = _ => throw new InvalidOperationException("plan broke");
            var failed = PlayerOverlayLaunch.Run(ports);
            Assert.That(failed.Outcome, Is.EqualTo(PlayerOverlayOutcome.EntryFailed));
            StringAssert.Contains("plan broke", failed.Attempt.detail);
            Assert.That(_calls, Does.Not.Contain("Start"));
        }

        [Test]
        public void NoUnsavedAssets_DoesNotAskForConfirmation()
        {
            PlayerOverlayLaunch.Run(Ports());
            Assert.That(_calls, Does.Not.Contain("ConfirmAssets").And.Not.Contain("SaveAssets"));
        }

        [Test]
        public void BuildRecord_IsNullWhenMissingCorruptOrForAnotherIdentity()
        {
            var root = Path.Combine(Path.GetTempPath(), "osm-s42-rec-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                var path = PlayerOverlayRecords.BuildRecordPath(root, OverlayId);
                Assert.That(PlayerOverlayRecords.ReadBuildRecord(path, OverlayId), Is.Null);
                PlayerOverlayRecords.WriteBuildRecord(path, new PlayerOverlayBuildRecord { identity = OverlayId, contentPath = OverlayPath, digest = "d" });
                Assert.That(PlayerOverlayRecords.ReadBuildRecord(path, OverlayId)!.digest, Is.EqualTo("d"));
                Assert.That(PlayerOverlayRecords.ReadBuildRecord(path, "other"), Is.Null);
                File.WriteAllText(path, "{not json");
                Assert.That(PlayerOverlayRecords.ReadBuildRecord(path, OverlayId), Is.Null);
                Assert.Throws<ArgumentException>(() => PlayerOverlayRecords.BuildRecordPath(root, @"..\x"));

                var attemptPath = PlayerOverlayRecords.LastAttemptPath(root);
                PlayerOverlayRecords.WriteAttempt(attemptPath, new PlayerOverlayAttempt { outcome = "Launched", startup = "Pending" });
                PlayerOverlayRecords.WriteAttempt(attemptPath, new PlayerOverlayAttempt { outcome = "Cancelled" });
                Assert.That(PlayerOverlayRecords.ReadAttempt(attemptPath)!.outcome, Is.EqualTo("Cancelled"), "置き換えで書く");
                Assert.That(Directory.GetFiles(Path.GetDirectoryName(attemptPath)!, "*.tmp"), Is.Empty);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
