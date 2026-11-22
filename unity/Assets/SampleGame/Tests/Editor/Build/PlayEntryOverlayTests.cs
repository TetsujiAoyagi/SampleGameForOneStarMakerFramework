#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.ContentOverlay;
using SampleGame.DependOnAll.Editor.Build;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// 入口の上書きの段の規則（受け入れ条件 AC4 の (b)〜(f)・(h)〜(j)・(m)〜(o)）。port を fake にし、Framework の計画の object を作らない。
    /// </summary>
    public sealed class PlayEntryOverlayTests
    {
        private readonly List<string> _calls = new();
        private double _clock;

        private PlayEntryOverlayPorts Ports(PlayEntryOverlayPlanSummary plan, PlayEntryOverlayArtifact? reuse = null, PlayEntryOverlayArtifact? build = null,
            Action? planThrows = null, Action? reuseThrows = null)
        {
            _calls.Clear();
            _clock = 0;
            return new PlayEntryOverlayPorts
            {
                // 段ごとに時計を進め、所要がどの段の間かを確かめる。
                Now = () => _clock,
                Prepare = () => { _calls.Add("Prepare"); _clock += 1; },
                Plan = () => { _calls.Add("Plan"); _clock += 2; planThrows?.Invoke(); return plan; },
                AcknowledgeAndReuse = () => { _calls.Add("Reuse"); _clock += 3; reuseThrows?.Invoke(); return reuse; },
                AcknowledgeAndBuild = () => { _calls.Add("Build"); _clock += 4; return build ?? Built("built-1"); },
            };
        }

        private static PlayEntryOverlayArtifact Built(string identity, string? recordFailure = null) =>
            new(true, identity, @"D:\artifacts\bs2b\content\" + identity, "", recordFailure);

        private static PlayEntryUnbuildableRoot Root(string key, string reason) => new(key, "Scene", "Full", reason);

        private static PlayEntryOverlayPlanSummary Plan(PlayEntryOverlayPlanState state, int roots = 0, params PlayEntryUnbuildableRoot[] unbuildable) =>
            new(state, roots, unbuildable);

        private static string Env(PlayEntryOverlayResult result, string key) =>
            result.Environment.Single(x => x.Key == PurposeLaunchSettings.EnvironmentName(key)).Value;

        [Test]
        public void NotRequired_BuildsNothing_AndPlacesNoKey()
        {
            var result = PlayEntryOverlay.Run(Ports(Plan(PlayEntryOverlayPlanState.NotRequired)));
            Assert.That(_calls, Is.EqualTo(new[] { "Prepare", "Plan" }));
            Assert.That(result.State, Is.EqualTo(PlayEntryOverlayState.NotRequired));
            Assert.That(result.Environment, Is.Empty);
            Assert.That(result.RegistersOverlay, Is.False);
        }

        [Test]
        public void BuildRequired_Reusable_BuildsNothing_AndPlacesThePreviousArtifact()
        {
            var result = PlayEntryOverlay.Run(Ports(Plan(PlayEntryOverlayPlanState.BuildRequired, 2), reuse: Built("previous")));
            Assert.That(_calls, Is.EqualTo(new[] { "Prepare", "Plan", "Reuse" }));
            Assert.That(result.State, Is.EqualTo(PlayEntryOverlayState.Reused));
            Assert.That(Env(result, ContentOverlayPlayConfig.OverlayIdentityKey), Is.EqualTo("previous"));
            Assert.That(Env(result, ContentOverlayPlayConfig.OverlayPathKey), Does.EndWith("previous"));
            Assert.That(result.OverlayRootCount, Is.EqualTo(2));
        }

        [Test]
        public void BuildRequired_NotReusable_BuildsOnce_AndPlacesThisArtifact()
        {
            var result = PlayEntryOverlay.Run(Ports(Plan(PlayEntryOverlayPlanState.BuildRequired, 1), reuse: null, build: Built("now")));
            Assert.That(_calls, Is.EqualTo(new[] { "Prepare", "Plan", "Reuse", "Build" }));
            Assert.That(result.State, Is.EqualTo(PlayEntryOverlayState.Built));
            Assert.That(Env(result, ContentOverlayPlayConfig.OverlayIdentityKey), Is.EqualTo("now"));
            Assert.That(result.BuildSeconds, Is.EqualTo(4));
        }

        [Test]
        public void UnbuildableOnly_BuildsNothing_PlacesNoKey_AndShowsTheRoots()
        {
            var result = PlayEntryOverlay.Run(Ports(Plan(PlayEntryOverlayPlanState.UnbuildableOnly, 0, Root("B", "SourceAbsent"))));
            Assert.That(_calls, Is.EqualTo(new[] { "Prepare", "Plan" }));
            Assert.That(result.State, Is.EqualTo(PlayEntryOverlayState.UnbuildableOnly));
            Assert.That(result.Environment, Is.Empty);
            var warnings = PlayEntryOverlay.Warnings(result);
            Assert.That(warnings.Single(), Does.Contain("B (Scene, Full, SourceAbsent)"));
        }

        [Test]
        public void BuildRequiredWithUnbuildableRoots_ShowsThisPlansSet_EvenWhenReused()
        {
            // 前回の build の時は 1 つ、今回は 2 つ。示すのは今回の計画の 2 つ（再利用した preflight ではない）。
            var plan = Plan(PlayEntryOverlayPlanState.BuildRequired, 1, Root("B", "SourceAbsent"), Root("D", "SourceAbsent"));
            var result = PlayEntryOverlay.Run(Ports(plan, reuse: Built("previous")));
            Assert.That(result.State, Is.EqualTo(PlayEntryOverlayState.Reused));
            Assert.That(result.Unbuildable.Select(x => x.LogicalKey), Is.EqualTo(new[] { "B", "D" }));
            Assert.That(PlayEntryOverlay.Warnings(result).Single(), Does.Contain("(2)"));
        }

        [Test]
        public void BuildFailure_Throws_WithTheIssue()
        {
            var failed = new PlayEntryOverlayArtifact(false, "x", "", "SourceChangedDuringBuild Assets/M.mat: 1 source file(s) changed", null);
            var ex = Assert.Throws<PlayEntryOverlayException>(() => PlayEntryOverlay.Run(Ports(Plan(PlayEntryOverlayPlanState.BuildRequired, 1), build: failed)));
            Assert.That(ex!.Message, Does.Contain("SourceChangedDuringBuild").And.Contain("Assets/M.mat"));
        }

        [Test]
        public void PlanOrReuseCheckFailure_Throws_WithoutAShortcutResult()
        {
            Assert.Throws<InvalidOperationException>(() =>
                PlayEntryOverlay.Run(Ports(Plan(PlayEntryOverlayPlanState.NotRequired), planThrows: () => throw new InvalidOperationException("plan"))));
            Assert.Throws<InvalidOperationException>(() =>
                PlayEntryOverlay.Run(Ports(Plan(PlayEntryOverlayPlanState.BuildRequired, 1), reuseThrows: () => throw new InvalidOperationException("reuse"))));
            Assert.That(_calls, Does.Not.Contain("Build"), "再利用の確かめが失敗した時に build へ逃げない");
        }

        [Test]
        public void ReuseRecordFailure_IsSuccessful_AndShown()
        {
            var result = PlayEntryOverlay.Run(Ports(Plan(PlayEntryOverlayPlanState.BuildRequired, 1), build: Built("now", "record locked")));
            Assert.That(result.State, Is.EqualTo(PlayEntryOverlayState.Built));
            Assert.That(result.ReuseRecordFailure, Is.EqualTo("record locked"));
            Assert.That(PlayEntryOverlay.Warnings(result).Single(), Does.Contain("record locked"));
        }

        [Test]
        public void Durations_AreSeparate_AndStagesNotRunAreNotZero()
        {
            var reused = PlayEntryOverlay.Run(Ports(Plan(PlayEntryOverlayPlanState.BuildRequired, 1), reuse: Built("previous")));
            Assert.That(reused.PrepareSeconds, Is.EqualTo(1));
            Assert.That(reused.PlanSeconds, Is.EqualTo(2));
            Assert.That(reused.ReuseCheckSeconds, Is.EqualTo(3));
            Assert.That(reused.BuildSeconds, Is.Null);
            Assert.That(PlayEntryOverlay.FormatSeconds(reused.BuildSeconds), Is.EqualTo(PlayEntryOverlay.NotRun));
            var none = PlayEntryOverlay.Run(Ports(Plan(PlayEntryOverlayPlanState.NotRequired)));
            Assert.That(none.ReuseCheckSeconds, Is.Null);
            Assert.That(PlayEntryOverlay.FormatSeconds(0), Is.EqualTo("0.000"));
        }

        [Test]
        public void UnbuildableReasons_FallIntoThreeGroups_AndNotInBaseIsNotShownAsBaseVersion()
        {
            var reasons = new[] { "SourceAbsent", "NotInLocalSelection", "NotInBase", "RecordMissing", "RecordUnsupported", "RecordInvalid" };
            var plan = Plan(PlayEntryOverlayPlanState.BuildRequired, 1, reasons.Select(x => Root("R-" + x, x)).ToArray());
            var result = PlayEntryOverlay.Run(Ports(plan, reuse: Built("previous")));
            Assert.That(result.State, Is.EqualTo(PlayEntryOverlayState.Reused), "NotInBase があっても Play に入る");
            var groups = result.Unbuildable.ToDictionary(x => x.Reason, x => x.Group);
            Assert.That(groups["SourceAbsent"], Is.EqualTo(PlayEntryUnbuildableGroup.LoadsFromBase));
            Assert.That(groups["NotInLocalSelection"], Is.EqualTo(PlayEntryUnbuildableGroup.LoadsFromBase));
            Assert.That(groups["NotInBase"], Is.EqualTo(PlayEntryUnbuildableGroup.FailsOnRequest));
            Assert.That(groups["RecordMissing"], Is.EqualTo(PlayEntryUnbuildableGroup.Undetermined));
            Assert.That(groups["RecordUnsupported"], Is.EqualTo(PlayEntryUnbuildableGroup.Undetermined));
            Assert.That(groups["RecordInvalid"], Is.EqualTo(PlayEntryUnbuildableGroup.Undetermined));
            var warnings = PlayEntryOverlay.Warnings(result);
            Assert.That(warnings.Count, Is.EqualTo(3));
            var baseLine = warnings.Single(x => x.StartsWith(PlayEntryOverlay.GroupMessage(PlayEntryUnbuildableGroup.LoadsFromBase), StringComparison.Ordinal));
            Assert.That(baseLine, Does.Not.Contain("R-NotInBase"));
            var failLine = warnings.Single(x => x.StartsWith(PlayEntryOverlay.GroupMessage(PlayEntryUnbuildableGroup.FailsOnRequest), StringComparison.Ordinal));
            Assert.That(failLine, Does.Contain("R-NotInBase"));
        }

        [Test]
        public void ConsoleLine_CutsTheRootList_AndSaysHowManyAreLeft()
        {
            var roots = Enumerable.Range(0, PlayEntryOverlay.ConsoleRootLimit + 3).Select(i => Root("R" + i, "SourceAbsent")).ToArray();
            var result = PlayEntryOverlay.Run(Ports(Plan(PlayEntryOverlayPlanState.UnbuildableOnly, 0, roots)));
            var line = PlayEntryOverlay.Warnings(result).Single();
            Assert.That(line, Does.Contain("+3 more"));
            Assert.That(line, Does.Contain("R0 ").And.Not.Contain("R" + (PlayEntryOverlay.ConsoleRootLimit + 2) + " "));
        }

        [Test]
        public void SuppliedSceneKeys_AreTheCopiedScenesInTheSceneResourceMap()
        {
            var keys = PlayEntryOverlay.SuppliedSceneKeys(new[] { "Title", "Scratch", "Title", "OutGameScene" },
                new HashSet<string>(new[] { "Title", "OutGameScene", "HomeScene" }, StringComparer.Ordinal));
            Assert.That(keys, Is.EqualTo(new[] { "OutGameScene", "Title" }));
        }
    }
}
