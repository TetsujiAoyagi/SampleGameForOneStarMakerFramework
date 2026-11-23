#nullable enable

using System;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.ContentOverlay;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;
using UnityEngine;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// record の上書きの欄（受け入れ条件 AC4 の (l)・(o)）。入口の計画と、起動の log の上書きつきの登録の行の突き合わせ。時刻は引数で注入する。
    /// </summary>
    public sealed class PlayEntryOverlayRecordTests
    {
        private string _savedRecord = "";

        [SetUp]
        public void SetUp()
        {
            _savedRecord = SessionState.GetString(PlayEntryRecord.SessionKey, string.Empty);
            SessionState.EraseString(PlayEntryRecord.SessionKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_savedRecord.Length == 0) SessionState.EraseString(PlayEntryRecord.SessionKey);
            else SessionState.SetString(PlayEntryRecord.SessionKey, _savedRecord);
        }

        private static PlayEntryRecord.Document Current => PlayEntryRecord.Load()!;
        private static PlayEntryFacts Facts() => new() { RunPurpose = "spring-full", Route = PlayEntryRoute.BuiltWithLocalEdits };

        private static PlayEntryOverlayResult Overlay(PlayEntryOverlayState state, string? identity) =>
            new(state, identity == null ? 0 : 2,
                new[] { new PlayEntryUnbuildableRoot("B", "Scene", "Full", "SourceAbsent"), new PlayEntryUnbuildableRoot("N", "Scene", "Full", "NotInBase") },
                0.25, 1.5, identity == null ? null : 0.75, state == PlayEntryOverlayState.Built ? 12.0 : null,
                identity == null ? null : new PlayEntryOverlayArtifact(true, identity, @"D:\content\" + identity, "", null));

        private static PlayEntryPlan LocalEditsPlan(PlayEntryOverlayResult overlay) =>
            new PlayEntryPlan(PlayEntryAction.Intervene, PlayEntryRule.LocalEditsRoute, "because", PlayEntryDecision.Directory, revision: "rev-1")
                .WithOverlay(overlay);

        private static string Registered(string identity) => ContentOverlayPlayConfig.RegisteredLogMarker + "identity=" + identity +
                                                              " baseRevision=rev-1 path=D:\\content\\" + identity;

        private const string Resolution = PlayEntryRecord.ContentResolutionLogMarker + "wired=true provider=registered-content-directory";

        [Test]
        public void PlannedOverlay_MatchingRegistration_IsMatched()
        {
            PlayEntryRecord.BeginPlanned(Facts(), LocalEditsPlan(Overlay(PlayEntryOverlayState.Built, "overlay-1")));
            PlayEntryRecord.ObserveLog(Registered("overlay-1"), LogType.Log, 10);
            PlayEntryRecord.ObserveLog(Resolution, LogType.Log, 11);
            PlayEntryRecord.MarkEnteredPlay(12);
            var record = Current.record;
            Assert.That(record.overlayRegistered, Is.EqualTo("overlay-1"));
            Assert.That(record.overlayCheck, Is.EqualTo("matched"));
            Assert.That(record.state, Is.EqualTo("EnteredPlay"));
        }

        [Test]
        public void PlannedOverlay_WithoutTheRegistrationLine_IsMismatch()
        {
            PlayEntryRecord.BeginPlanned(Facts(), LocalEditsPlan(Overlay(PlayEntryOverlayState.Reused, "overlay-1")));
            PlayEntryRecord.ObserveLog(Resolution, LogType.Log, 11);
            PlayEntryRecord.MarkEnteredPlay(12);
            var record = Current.record;
            Assert.That(record.overlayCheck, Is.EqualTo("mismatch"));
            Assert.That(record.state, Is.EqualTo("StartupFailed"));
            Assert.That(record.firstFailure, Does.Contain("overlay-1"));
        }

        [Test]
        public void PlannedOverlay_DifferentIdentity_IsMismatch()
        {
            PlayEntryRecord.BeginPlanned(Facts(), LocalEditsPlan(Overlay(PlayEntryOverlayState.Built, "overlay-1")));
            PlayEntryRecord.ObserveLog(Registered("overlay-2"), LogType.Log, 10);
            PlayEntryRecord.MarkEnteredPlay(12);
            Assert.That(Current.record.overlayCheck, Is.EqualTo("mismatch"));
            Assert.That(Current.record.state, Is.EqualTo("StartupFailed"));
        }

        [Test]
        public void NoOverlayPlanned_ButRegistered_IsMismatch()
        {
            PlayEntryRecord.BeginPlanned(Facts(), LocalEditsPlan(Overlay(PlayEntryOverlayState.NotRequired, null)));
            PlayEntryRecord.ObserveLog(Registered("overlay-9"), LogType.Log, 10);
            Assert.That(Current.record.overlayCheck, Is.EqualTo("mismatch"));
            Assert.That(Current.record.state, Is.EqualTo("StartupFailed"));
        }

        [Test]
        public void NoOverlayPlanned_NoRegistration_IsNotAMismatch()
        {
            PlayEntryRecord.BeginPlanned(Facts(), LocalEditsPlan(Overlay(PlayEntryOverlayState.UnbuildableOnly, null)));
            PlayEntryRecord.ObserveLog(Resolution, LogType.Log, 11);
            PlayEntryRecord.MarkEnteredPlay(12);
            PlayEntryRecord.StopObserving();
            Assert.That(Current.record.overlayCheck, Is.Empty);
            Assert.That(Current.record.state, Is.EqualTo("EnteredPlay"));
        }

        [Test]
        public void PassThroughPlay_RecordsTheRegistration_WithoutChecking()
        {
            var plan = new PlayEntryPlan(PlayEntryAction.PassThrough, PlayEntryRule.ExplicitSetting, "explicit", PlayEntryDecision.Directory);
            PlayEntryRecord.BeginPlanned(new PlayEntryFacts { RunPurpose = "spring-full" }, plan);
            PlayEntryRecord.ObserveLog(Registered("host-overlay"), LogType.Log, 10);
            PlayEntryRecord.MarkEnteredPlay(12);
            Assert.That(Current.record.overlayRegistered, Is.EqualTo("host-overlay"));
            Assert.That(Current.record.overlayCheck, Is.Empty);
            Assert.That(Current.record.state, Is.EqualTo("EnteredPlay"));
        }

        [Test]
        public void Policy_KeepsTheFourDurations_TheGroups_AndNotRunStages()
        {
            PlayEntryRecord.BeginPlanned(Facts(), LocalEditsPlan(Overlay(PlayEntryOverlayState.Reused, "overlay-1")));
            var overlay = Current.policy.overlay;
            Assert.That(overlay.state, Is.EqualTo("Reused"));
            Assert.That(overlay.overlayRoots, Is.EqualTo(2));
            Assert.That(overlay.prepareSeconds, Is.EqualTo("0.250"));
            Assert.That(overlay.planSeconds, Is.EqualTo("1.500"));
            Assert.That(overlay.reuseCheckSeconds, Is.EqualTo("0.750"));
            Assert.That(overlay.buildSeconds, Is.EqualTo(PlayEntryOverlay.NotRun), "行わなかった build は 0 ではない");
            Assert.That(overlay.unbuildable.Select(x => x.logicalKey + ":" + x.group),
                Is.EqualTo(new[] { "B:LoadsFromBase", "N:FailsOnRequest" }));
            var line = PlayEntryRecord.FormatConsoleLine(Current);
            Assert.That(line, Does.Contain("overlay: state=Reused").And.Contain("loadsFromBase=1").And.Contain("failsOnRequest=1")
                .And.Contain("build=not-run"));
            Assert.That(Current.policy.warnings.Count(x => x.StartsWith(PlayEntryOverlay.GroupMessage(PlayEntryUnbuildableGroup.FailsOnRequest),
                StringComparison.Ordinal)), Is.EqualTo(1));
        }
    }
}
