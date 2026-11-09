#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// Play 入口の判定表（AC1）。層の値は Runtime と同じ provider で読み、競合の理由は既存の検査の実物から作る。
    /// 開発者の Editor の ledger・環境変数・Variant profile は SetUp で退避し TearDown で戻す。
    /// </summary>
    public sealed class PlayEntryDecisionTests
    {
        private const string LedgerKey = "OSM.ContentDelivery.PlayBridge.v1";
        private const string RuntimeModeEnvironment = "SAMPLEGAME_CONTENT__RUNTIMEMODE";
        private const string RevisionPathEnvironment = "SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH";
        private const string DigestEnvironment = "SAMPLEGAME_CONTENT__MANIFESTSHA256";
        private const string CompanionEnvironment = "SAMPLEGAME_WORLD__CELLCOMPANIONSET";
        private const string VariantEnvironment = "SAMPLEGAME_ASSETS__SCENEVARIANT";
        private static readonly string[] IsolatedEnvironment =
            { RuntimeModeEnvironment, RevisionPathEnvironment, DigestEnvironment, CompanionEnvironment, VariantEnvironment };

        private readonly Dictionary<string, string?> _savedEnvironment = new();
        private string _savedLedger = "";
        private Func<string?>? _savedResolver;

        [SetUp]
        public void SetUp()
        {
            _savedLedger = SessionState.GetString(LedgerKey, string.Empty);
            SessionState.EraseString(LedgerKey);
            _savedResolver = SceneVariantRuntimeBridge.EditorSceneVariantResolver;
            SceneVariantRuntimeBridge.EditorSceneVariantResolver = null;
            foreach (var name in IsolatedEnvironment)
            {
                _savedEnvironment[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var pair in _savedEnvironment) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            SceneVariantRuntimeBridge.EditorSceneVariantResolver = _savedResolver;
            if (_savedLedger.Length == 0) SessionState.EraseString(LedgerKey);
            else SessionState.SetString(LedgerKey, _savedLedger);
        }

        private static PlayEntryCandidate Candidate(bool multiple = false) =>
            new("revision-1", PurposeLaunchSettings.DirectoryPlay("root", "digest", "revision-1", "Full",
                SampleGameBuildPurposes.SpringFull.RuntimeDefaults, "Title"), multiple);

        private static PlayEntryFacts Facts(PlayEntryRoute route = PlayEntryRoute.Source, bool fastMode = true) => new()
        {
            RunPurpose = SampleGameBuildPurposes.SpringFull.Name,
            Route = route,
            IsFastMode = fastMode,
            SourceEnvironment = PurposeLaunchSettings.PlayAs(SampleGameBuildPurposes.SpringFull).PlacedEnvironment,
        };

        private static PlayEntryConfigLayer EnvironmentLayer(params (string Name, string Value)[] values)
        {
            foreach (var (name, value) in values) Environment.SetEnvironmentVariable(name, value);
            return PlayEntryInterceptor.ReadEnvironmentLayer();
        }

        private static void AssertRejected(PlayEntryPlan plan, PlayEntryRule rule)
        {
            Assert.That(plan.Action, Is.EqualTo(PlayEntryAction.Reject));
            Assert.That(plan.Rule, Is.EqualTo(rule));
            Assert.That(plan.Environment, Is.Empty, "拒否は値を置かない");
            Assert.That(plan.AlignFastMode, Is.False, "拒否は Play Mode Script を変えない");
        }

        private static void AssertPassThrough(PlayEntryPlan plan, PlayEntryRule rule)
        {
            Assert.That(plan.Action, Is.EqualTo(PlayEntryAction.PassThrough));
            Assert.That(plan.Rule, Is.EqualTo(rule));
            Assert.That(plan.Environment, Is.Empty, "素通りは値を書かない");
            Assert.That(plan.AlignFastMode, Is.False);
        }

        // ═══ 行 1: 別の仕組みの Play ═══

        [Test]
        public void Row1_Batch_PassesThrough()
        {
            var facts = Facts();
            facts.IsBatchMode = true;
            AssertPassThrough(PlayEntryDecision.Decide(facts), PlayEntryRule.BatchMode);
        }

        [Test]
        public void Row1_TestRunnerScene_PassesThrough_EvenWhenADirectoryPlayWouldBeRejected()
        {
            Assert.That(PlayEntryDecision.IsTestRunnerScene(new[] { "Title", "InitTestScene6389" }), Is.True);
            Assert.That(PlayEntryDecision.IsTestRunnerScene(new[] { "Title", "MyInitTestScene" }), Is.False);
            var facts = Facts();
            facts.IsTestRunnerScene = true;
            facts.ReusableScenes = new[] { "Title" };
            facts.EnvironmentLayer = EnvironmentLayer((RuntimeModeEnvironment, "directory"));
            var plan = PlayEntryDecision.Decide(facts);
            AssertPassThrough(plan, PlayEntryRule.TestRunnerScene);
            Assert.That(plan.Reason, Does.Contain("InitTestScene"), "誤除外を record で説明できる");
        }

        // ═══ 行 2: directory と再利用しうる Scene（G18）═══

        [Test]
        public void Row2_ManualLedgerDirectoryWithReusableScene_IsRejected()
        {
            var facts = Facts();
            facts.HasManualLedger = true;
            facts.EnvironmentLayer = EnvironmentLayer((RuntimeModeEnvironment, "directory"));
            facts.ReusableScenes = new[] { "Assets/Scenes/Title.unity" };
            var plan = PlayEntryDecision.Decide(facts);
            AssertRejected(plan, PlayEntryRule.DirectoryWithReusableScenes);
            Assert.That(plan.Recovery, Is.EqualTo(PlayEntryRecovery.EmptyScene));
            Assert.That(plan.Reason, Does.Contain("Assets/Scenes/Title.unity"));
        }

        [Test]
        public void Row2_OnePlayLedgerDirectoryWithReusableScene_IsRejectedBeforeThePassThrough()
        {
            var facts = Facts();
            facts.HasOnePlayLedger = true;
            facts.EnvironmentLayer = EnvironmentLayer((RuntimeModeEnvironment, "directory"));
            facts.ReusableScenes = new[] { "Title" };
            AssertRejected(PlayEntryDecision.Decide(facts), PlayEntryRule.DirectoryWithReusableScenes);
        }

        [Test]
        public void Row2_PairOnlyIsAnImplicitDirectory_AndIsRejectedWithReusableScene()
        {
            var facts = Facts();
            facts.EnvironmentLayer = EnvironmentLayer((RevisionPathEnvironment, "D:\\root"), (DigestEnvironment, "abc"));
            facts.ReusableScenes = new[] { "Title" };
            Assert.That(PlayEntryDecision.EffectiveMode(facts), Is.EqualTo("directory"));
            AssertRejected(PlayEntryDecision.Decide(facts), PlayEntryRule.DirectoryWithReusableScenes);
        }

        [Test]
        public void Row2_HalfAPairIsNotADirectory_AndStaysExplicit()
        {
            var facts = Facts();
            facts.EnvironmentLayer = EnvironmentLayer((RevisionPathEnvironment, "D:\\root"));
            facts.ReusableScenes = new[] { "Title" };
            AssertPassThrough(PlayEntryDecision.Decide(facts), PlayEntryRule.ExplicitSetting);
        }

        [Test]
        public void Row2_CommandLineDirectoryOutranksEnvironmentAddressables()
        {
            var facts = Facts();
            facts.EnvironmentLayer = EnvironmentLayer((RuntimeModeEnvironment, "addressables"));
            facts.CommandLineLayer = PlayEntryInterceptor.ReadCommandLineLayer(new[] { "--content.runtimeMode=directory" });
            facts.ReusableScenes = new[] { "Title" };
            AssertRejected(PlayEntryDecision.Decide(facts), PlayEntryRule.DirectoryWithReusableScenes);
        }

        [Test]
        public void Row2_JsonDirectoryWithReusableScene_IsRejected()
        {
            var facts = Facts();
            facts.JsonLayer = PlayEntryInterceptor.ReadJsonLayer("{\"content\":{\"runtimeMode\":\"directory\"}}");
            facts.ReusableScenes = new[] { "Title" };
            AssertRejected(PlayEntryDecision.Decide(facts), PlayEntryRule.DirectoryWithReusableScenes);
        }

        // ═══ 行 3: 一回の ledger ═══

        [Test]
        public void Row3_OnePlayLedgerWithoutReusableScene_PassesThrough()
        {
            var facts = Facts(PlayEntryRoute.Built);
            facts.HasOnePlayLedger = true;
            facts.EnvironmentLayer = EnvironmentLayer((RuntimeModeEnvironment, "directory"));
            AssertPassThrough(PlayEntryDecision.Decide(facts), PlayEntryRule.OnePlayLedger);
        }

        // ═══ 行 4: 明示設定 ═══

        [Test]
        public void Row4_CommandLineLayerAlone_IsExplicit()
        {
            var facts = Facts(fastMode: true);
            facts.CommandLineLayer = PlayEntryInterceptor.ReadCommandLineLayer(new[] { "-projectPath", "x", "--content.runtimeMode=addressables" });
            facts.ConflictReason = "would be rejected if row 5 were reached";
            AssertPassThrough(PlayEntryDecision.Decide(facts), PlayEntryRule.ExplicitSetting);
        }

        [Test]
        public void Row4_EmptyValueStillCountsAsExplicit()
        {
            var facts = Facts();
            facts.JsonLayer = PlayEntryInterceptor.ReadJsonLayer("{\"content\":{\"runtimeMode\":\"\"}}");
            var plan = PlayEntryDecision.Decide(facts);
            AssertPassThrough(plan, PlayEntryRule.ExplicitSetting);
            Assert.That(plan.EffectiveMode, Is.Empty);
        }

        [Test]
        public void Row4_EnvironmentNameCaseIsIgnored()
        {
            var facts = Facts();
            facts.EnvironmentLayer = EnvironmentLayer(("samplegame_content__runtimemode", "foo"));
            var plan = PlayEntryDecision.Decide(facts);
            AssertPassThrough(plan, PlayEntryRule.ExplicitSetting);
            Assert.That(plan.EffectiveMode, Is.EqualTo("foo"), "不正な値も書き換えず Runtime の fail-closed に任せる");
        }

        [Test]
        public void Row4_ManualLedgerIsExplicitEvenWithoutModeKeys()
        {
            var facts = Facts();
            facts.HasManualLedger = true;
            AssertPassThrough(PlayEntryDecision.Decide(facts), PlayEntryRule.ExplicitSetting);
        }

        [Test]
        public void Row4_ExplicitAddressablesOutsideFastMode_WarnsWithoutRejectingOrChangingTheScript()
        {
            var facts = Facts(fastMode: false);
            facts.EnvironmentLayer = EnvironmentLayer((RuntimeModeEnvironment, "addressables"));
            var plan = PlayEntryDecision.Decide(facts);
            AssertPassThrough(plan, PlayEntryRule.ExplicitSetting);
            Assert.That(plan.Warnings, Is.EqualTo(new[] { PlayEntryDecision.ExistingBuildWarning }));
        }

        [Test]
        public void Row4_ExplicitDirectoryOutsideFastMode_HasNoExistingBuildWarning()
        {
            var facts = Facts(fastMode: false);
            facts.EnvironmentLayer = EnvironmentLayer((RuntimeModeEnvironment, "directory"));
            Assert.That(PlayEntryDecision.Decide(facts).Warnings, Is.Empty);
        }

        [Test]
        public void Row4_VariantProfileIsCarriedForTheRecord_NotRejected()
        {
            var facts = Facts();
            facts.HasVariantProfile = true;
            facts.ConflictReason = "A Variant profile is active.";
            facts.EnvironmentLayer = EnvironmentLayer((RuntimeModeEnvironment, "addressables"));
            AssertPassThrough(PlayEntryDecision.Decide(facts), PlayEntryRule.ExplicitSetting);
        }

        // ═══ 行 5: 入口が置く値との衝突 ═══

        private static IReadOnlyList<string> SourceControlled => PurposeLaunchSettings.PlayAs(SampleGameBuildPurposes.SpringFull).Controlled;

        [Test]
        public void Row5_VariantProfile_IsRejectedWithTheExistingReason()
        {
            SceneVariantRuntimeBridge.EditorSceneVariantResolver = () => "Whitebox";
            var facts = Facts();
            facts.HasVariantProfile = true;
            facts.ConflictReason = PlayAsLauncher.CheckLaunchConflicts(SourceControlled, Array.Empty<string>());
            var plan = PlayEntryDecision.Decide(facts);
            AssertRejected(plan, PlayEntryRule.ConflictingState);
            Assert.That(plan.Reason, Does.Contain("Variant profile"));
        }

        [Test]
        public void Row5_ForeignEnvironmentValue_IsRejected()
        {
            Environment.SetEnvironmentVariable(CompanionEnvironment, "Full");
            var facts = Facts();
            facts.ConflictReason = PlayAsLauncher.CheckLaunchConflicts(SourceControlled, Array.Empty<string>());
            var plan = PlayEntryDecision.Decide(facts);
            AssertRejected(plan, PlayEntryRule.ConflictingState);
            Assert.That(plan.Reason, Does.Contain(CompanionEnvironment));
            Assert.That(Environment.GetEnvironmentVariable(CompanionEnvironment), Is.EqualTo("Full"));
        }

        [Test]
        public void Row5_CommandLineControlKey_IsRejected()
        {
            var facts = Facts();
            facts.ConflictReason = PlayAsLauncher.CheckLaunchConflicts(SourceControlled, new[] { "--world.cellCompanionSet=Full" });
            var plan = PlayEntryDecision.Decide(facts);
            AssertRejected(plan, PlayEntryRule.ConflictingState);
            Assert.That(plan.Reason, Does.Contain("command line"));
        }

        [Test]
        public void Row5_NonEmptyLowerLayer_IsRejected()
        {
            var facts = Facts();
            facts.ConflictReason = PlayAsLauncher.CheckEmptyLowerLayer(
                PurposeLaunchSettings.PlayAs(SampleGameBuildPurposes.SpringFull).RequiresEmptyLowerLayer,
                "{\"assets\":{\"sceneVariant\":\"Whitebox\"}}");
            var plan = PlayEntryDecision.Decide(facts);
            AssertRejected(plan, PlayEntryRule.ConflictingState);
            Assert.That(plan.Reason, Does.Contain("assets:sceneVariant"));
        }

        // ═══ 行 6: 経路 Source ═══

        [Test]
        public void Row6_Source_PlacesThePurposeEnvironment()
        {
            var plan = PlayEntryDecision.Decide(Facts(fastMode: true));
            Assert.That(plan.Action, Is.EqualTo(PlayEntryAction.Intervene));
            Assert.That(plan.Rule, Is.EqualTo(PlayEntryRule.SourceRoute));
            Assert.That(plan.Environment, Is.EqualTo(PurposeLaunchSettings.PlayAs(SampleGameBuildPurposes.SpringFull).PlacedEnvironment));
            Assert.That(plan.Environment.Single(x => x.Key == RuntimeModeEnvironment).Value, Is.EqualTo("addressables"));
            Assert.That(plan.AlignFastMode, Is.False);
        }

        [Test]
        public void Row6_SourceOutsideFastMode_AlignsTheScript()
        {
            Assert.That(PlayEntryDecision.Decide(Facts(fastMode: false)).AlignFastMode, Is.True);
        }

        [Test]
        public void Row6_SourceWithTitleOpen_IsNotRejected()
        {
            var facts = Facts();
            facts.ReusableScenes = new[] { "Title" };
            Assert.That(PlayEntryDecision.Decide(facts).Action, Is.EqualTo(PlayEntryAction.Intervene));
        }

        // ═══ 行 7・8・9: 経路 Built ═══

        [Test]
        public void Row7_BuiltWithoutCandidate_IsRejected_AndNeverBecomesSource()
        {
            var plan = PlayEntryDecision.Decide(Facts(PlayEntryRoute.Built));
            AssertRejected(plan, PlayEntryRule.NoVerifiedCandidate);
            Assert.That(plan.Recovery, Is.EqualTo(PlayEntryRecovery.VerifyPlayBuild | PlayEntryRecovery.SwitchToSource));
        }

        [Test]
        public void Row7_ValidationFailure_CarriesTheReason()
        {
            var facts = Facts(PlayEntryRoute.Built);
            facts.CandidateFailure = "revision-1: Installed manifest digest differs.";
            var plan = PlayEntryDecision.Decide(facts);
            AssertRejected(plan, PlayEntryRule.NoVerifiedCandidate);
            Assert.That(plan.Reason, Does.Contain("digest differs"));
        }

        [Test]
        public void Row8_BuiltWithCandidateAndReusableScene_IsRejected()
        {
            var facts = Facts(PlayEntryRoute.Built);
            facts.Candidate = Candidate();
            facts.ReusableScenes = new[] { "Title" };
            var plan = PlayEntryDecision.Decide(facts);
            AssertRejected(plan, PlayEntryRule.BuiltWithReusableScenes);
            Assert.That(plan.Recovery, Is.EqualTo(PlayEntryRecovery.EmptyScene));
        }

        [Test]
        public void Row9_BuiltWithCandidate_PlacesTheVerifiedPair()
        {
            var facts = Facts(PlayEntryRoute.Built, fastMode: false);
            facts.Candidate = Candidate();
            var plan = PlayEntryDecision.Decide(facts);
            Assert.That(plan.Action, Is.EqualTo(PlayEntryAction.Intervene));
            Assert.That(plan.Rule, Is.EqualTo(PlayEntryRule.BuiltRoute));
            Assert.That(plan.Revision, Is.EqualTo("revision-1"));
            Assert.That(plan.Environment, Is.EqualTo(facts.Candidate.PlacedEnvironment));
            Assert.That(plan.AlignFastMode, Is.False, "Play Mode Script は source Play の前提");
            Assert.That(plan.Warnings, Is.Empty);
        }

        [Test]
        public void Row9_SeveralCandidates_WarnWithoutBlocking()
        {
            var facts = Facts(PlayEntryRoute.Built);
            facts.Candidate = Candidate(multiple: true);
            facts.CandidateWarnings = new[] { "Skipped while selecting content: revision-2: digest differs" };
            var plan = PlayEntryDecision.Decide(facts);
            Assert.That(plan.Action, Is.EqualTo(PlayEntryAction.Intervene));
            Assert.That(plan.Warnings, Has.Count.EqualTo(2));
            Assert.That(plan.Warnings, Has.Some.Contains("revision-2"));
            Assert.That(plan.Warnings, Has.Some.Contains("latest"));
        }

        [Test]
        public void Facts_RealEnvironmentLayer_OnlyKeepsTheThreeModeKeys()
        {
            var layer = EnvironmentLayer((CompanionEnvironment, "Full"));
            Assert.That(layer.HasAny, Is.False);
        }
    }
}
