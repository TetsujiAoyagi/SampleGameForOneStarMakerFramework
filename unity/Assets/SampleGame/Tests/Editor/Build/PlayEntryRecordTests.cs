#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;
using UnityEngine;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>policy と record の分離、起動の失敗の観測、RunPurpose と経路の永続。時刻は引数で注入する。</summary>
    public sealed class PlayEntryRecordTests
    {
        private string _savedRecord = "";
        private string? _savedPurpose;
        private string? _savedRoute;

        [SetUp]
        public void SetUp()
        {
            _savedRecord = SessionState.GetString(PlayEntryRecord.SessionKey, string.Empty);
            _savedPurpose = EditorUserSettings.GetConfigValue(PlayEntrySettings.RunPurposeKey);
            _savedRoute = EditorUserSettings.GetConfigValue(PlayEntrySettings.RouteKey);
            SessionState.EraseString(PlayEntryRecord.SessionKey);
            EditorUserSettings.SetConfigValue(PlayEntrySettings.RunPurposeKey, null);
            EditorUserSettings.SetConfigValue(PlayEntrySettings.RouteKey, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_savedRecord.Length == 0) SessionState.EraseString(PlayEntryRecord.SessionKey);
            else SessionState.SetString(PlayEntryRecord.SessionKey, _savedRecord);
            EditorUserSettings.SetConfigValue(PlayEntrySettings.RunPurposeKey, _savedPurpose);
            EditorUserSettings.SetConfigValue(PlayEntrySettings.RouteKey, _savedRoute);
        }

        private static PlayEntryFacts Facts(PlayEntryRoute route = PlayEntryRoute.Source, bool variant = false) =>
            new() { RunPurpose = "spring-full", Route = route, HasVariantProfile = variant };

        private static PlayEntryPlan SourcePlan() => new(PlayEntryAction.Intervene, PlayEntryRule.SourceRoute, "because", "addressables",
            new[] { new KeyValuePair<string, string>("SAMPLEGAME_CONTENT__RUNTIMEMODE", "addressables") });

        private static PlayEntryRecord.Document Current => PlayEntryRecord.Load()!;

        [Test]
        public void Planned_WritesPolicyAndRecordSeparately()
        {
            PlayEntryRecord.BeginPlanned(Facts(variant: true), SourcePlan());
            var document = Current;
            Assert.That(document.policy.action, Is.EqualTo("Intervene"));
            Assert.That(document.policy.rule, Is.EqualTo("SourceRoute"));
            Assert.That(document.policy.placedKeys, Is.EqualTo(new[] { "SAMPLEGAME_CONTENT__RUNTIMEMODE" }));
            Assert.That(document.record.state, Is.EqualTo("Planned"));
            Assert.That(document.record.initializer, Is.EqualTo(PlayEntryRecord.Unknown), "成功は Runtime の log から判定できない");
            Assert.That(document.record.variantProfileActive, Is.True);
        }

        [Test]
        public void EnteredPlay_WithoutFailure_IsEnteredPlay()
        {
            PlayEntryRecord.BeginPlanned(Facts(), SourcePlan());
            PlayEntryRecord.MarkEnteredPlay(100);
            Assert.That(Current.record.state, Is.EqualTo("EnteredPlay"));
        }

        [Test]
        public void FailureBeforeEnteredPlay_StaysStartupFailed()
        {
            PlayEntryRecord.BeginPlanned(Facts(), SourcePlan());
            PlayEntryRecord.ObserveLog("content:runtimeMode must be addressables or directory.", LogType.Exception, 50);
            PlayEntryRecord.MarkEnteredPlay(100);
            var record = Current.record;
            Assert.That(record.state, Is.EqualTo("StartupFailed"));
            Assert.That(record.exceptionCount, Is.EqualTo(1));
            Assert.That(record.firstFailure, Does.Contain("content:runtimeMode"));
            Assert.That(record.initializer, Is.EqualTo(PlayEntryRecord.Unknown));
        }

        [Test]
        public void ErrorInsideTheWindow_IsStartupFailed_AndKeepsTheFirstMessage()
        {
            PlayEntryRecord.BeginPlanned(Facts(), SourcePlan());
            PlayEntryRecord.MarkEnteredPlay(100);
            PlayEntryRecord.ObserveLog("first", LogType.Error, 100 + PlayEntryRecord.StartupObservationSeconds - 1);
            PlayEntryRecord.ObserveLog("second", LogType.Error, 100 + PlayEntryRecord.StartupObservationSeconds - 1);
            var record = Current.record;
            Assert.That(record.state, Is.EqualTo("StartupFailed"));
            Assert.That(record.errorCount, Is.EqualTo(2));
            Assert.That(record.firstFailure, Is.EqualTo("first"));
        }

        [Test]
        public void ErrorAfterTheWindow_IsNotAStartupFailure()
        {
            PlayEntryRecord.BeginPlanned(Facts(), SourcePlan());
            PlayEntryRecord.MarkEnteredPlay(100);
            PlayEntryRecord.ObserveLog("late", LogType.Error, 100 + PlayEntryRecord.StartupObservationSeconds + 1);
            Assert.That(Current.record.state, Is.EqualTo("EnteredPlay"));
            Assert.That(Current.record.errorCount, Is.Zero);
        }

        [Test]
        public void WarningsAndOrdinaryLogs_AreIgnored()
        {
            PlayEntryRecord.BeginPlanned(Facts(), SourcePlan());
            PlayEntryRecord.ObserveLog("warning", LogType.Warning, 1);
            PlayEntryRecord.ObserveLog("info", LogType.Log, 1);
            Assert.That(Current.record.errorCount, Is.Zero);
            Assert.That(Current.record.sealedLog, Is.Empty);
        }

        [Test]
        public void SealedLog_IsRecorded()
        {
            PlayEntryRecord.BeginPlanned(Facts(PlayEntryRoute.Built), SourcePlan());
            const string line = "[AppInit] Content directory mode: Addressables backend sealed after reading the configuration.";
            PlayEntryRecord.ObserveLog(line, LogType.Log, 1);
            Assert.That(Current.record.sealedLog, Is.EqualTo(line));
        }

        [Test]
        public void StopObserving_FreezesTheRecord_ButKeepsItReadable()
        {
            PlayEntryRecord.BeginPlanned(Facts(), SourcePlan());
            PlayEntryRecord.MarkEnteredPlay(100);
            PlayEntryRecord.StopObserving();
            PlayEntryRecord.ObserveLog("after stop", LogType.Error, 101);
            Assert.That(Current.record.state, Is.EqualTo("EnteredPlay"));
            Assert.That(Current.record.observing, Is.False);
        }

        [Test]
        public void Blocked_CarriesReasonAndRecovery_InTheConsoleLine()
        {
            var plan = new PlayEntryPlan(PlayEntryAction.Reject, PlayEntryRule.NoVerifiedCandidate, "No verified installed content", "directory",
                recovery: PlayEntryRecovery.VerifyPlayBuild | PlayEntryRecovery.SwitchToSource);
            PlayEntryRecord.MarkBlocked(Facts(PlayEntryRoute.Built), plan);
            var document = Current;
            Assert.That(document.record.state, Is.EqualTo("Blocked"));
            Assert.That(PlayEntryRecord.ParseRecoveries(document.policy), Is.EqualTo(plan.Recovery));

            var line = PlayEntryRecord.FormatConsoleLine(document);
            StringAssert.Contains("policy:", line);
            StringAssert.Contains("record:", line);
            StringAssert.Contains("No verified installed content", line);
            StringAssert.Contains("VerifyPlayBuild", line);
        }

        [Test]
        public void PassThroughDirectory_RevisionIsUnknown_NotGuessed()
        {
            PlayEntryRecord.BeginPlanned(Facts(), new PlayEntryPlan(PlayEntryAction.PassThrough, PlayEntryRule.ExplicitSetting, "explicit", "directory"));
            Assert.That(Current.record.revision, Is.EqualTo(PlayEntryRecord.Unknown));
        }

        [Test]
        public void BuiltPlan_RecordsTheRevision()
        {
            PlayEntryRecord.BeginPlanned(Facts(PlayEntryRoute.Built),
                new PlayEntryPlan(PlayEntryAction.Intervene, PlayEntryRule.BuiltRoute, "built", "directory", revision: "revision-1"));
            Assert.That(Current.record.revision, Is.EqualTo("revision-1"));
        }

        [Test]
        public void JsonRoundTrip_IsReadableThroughTheSessionKey()
        {
            PlayEntryRecord.BeginPlanned(Facts(), SourcePlan());
            var json = SessionState.GetString(PlayEntryRecord.SessionKey, string.Empty);
            StringAssert.Contains("\"policy\"", json);
            StringAssert.Contains("\"record\"", json);
            PlayEntryRecord.Clear();
            Assert.That(PlayEntryRecord.Load(), Is.Null);
        }

        // ═══ RunPurpose と経路の永続（P5）═══

        [Test]
        public void Settings_Defaults()
        {
            Assert.That(PlayEntrySettings.RunPurpose, Is.EqualTo("spring-full"));
            Assert.That(PlayEntrySettings.Route, Is.EqualTo(PlayEntryRoute.Source));
        }

        [Test]
        public void Settings_Persist()
        {
            PlayEntrySettings.RunPurpose = "spring-whitebox";
            PlayEntrySettings.Route = PlayEntryRoute.Built;
            Assert.That(PlayEntrySettings.RunPurpose, Is.EqualTo("spring-whitebox"));
            Assert.That(PlayEntrySettings.Route, Is.EqualTo(PlayEntryRoute.Built));
        }

        [Test]
        public void Settings_UnknownStoredValues_FallBackToDefaults()
        {
            EditorUserSettings.SetConfigValue(PlayEntrySettings.RunPurposeKey, "renamed-purpose");
            EditorUserSettings.SetConfigValue(PlayEntrySettings.RouteKey, "Elsewhere");
            Assert.That(PlayEntrySettings.RunPurpose, Is.EqualTo("spring-full"));
            Assert.That(PlayEntrySettings.Route, Is.EqualTo(PlayEntryRoute.Source));
        }

        [Test]
        public void Settings_UnknownPurpose_IsRejectedOnWrite()
        {
            Assert.Throws<ArgumentException>(() => PlayEntrySettings.RunPurpose = "renamed-purpose");
        }
    }
}
