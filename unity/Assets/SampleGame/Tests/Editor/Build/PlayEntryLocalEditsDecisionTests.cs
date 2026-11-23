#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.ContentOverlay;
using OneStarMaker.Runtime.AssetManagement;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// 経路 Built + local edits の判定と、上書きの key を明示設定に数えること（受け入れ条件 AC3 の (b)〜(e)）。今ある 3 経路の期待値は
    /// <see cref="PlayEntryDecisionTests"/> が変更なしで持つ。層の値は Runtime と同じ provider で読む。
    /// </summary>
    public sealed class PlayEntryLocalEditsDecisionTests
    {
        private const string LedgerKey = "OSM.ContentDelivery.PlayBridge.v1";
        private const string RuntimeModeEnvironment = "SAMPLEGAME_CONTENT__RUNTIMEMODE";
        private const string RevisionPathEnvironment = "SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH";
        private const string DigestEnvironment = "SAMPLEGAME_CONTENT__MANIFESTSHA256";
        private static readonly string OverlayPathEnvironment = PurposeLaunchSettings.EnvironmentName(ContentOverlayPlayConfig.OverlayPathKey);
        private static readonly string OverlayIdentityEnvironment = PurposeLaunchSettings.EnvironmentName(ContentOverlayPlayConfig.OverlayIdentityKey);

        private readonly Dictionary<string, string?> _saved = new();
        private string _savedLedger = "";
        private Func<string?>? _savedResolver;

        private string[] Isolated => new[] { RuntimeModeEnvironment, RevisionPathEnvironment, DigestEnvironment, OverlayPathEnvironment, OverlayIdentityEnvironment };

        [SetUp]
        public void SetUp()
        {
            _savedLedger = SessionState.GetString(LedgerKey, string.Empty);
            SessionState.EraseString(LedgerKey);
            _savedResolver = SceneVariantRuntimeBridge.EditorSceneVariantResolver;
            SceneVariantRuntimeBridge.EditorSceneVariantResolver = null;
            foreach (var name in Isolated)
            {
                _saved[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var pair in _saved) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            SceneVariantRuntimeBridge.EditorSceneVariantResolver = _savedResolver;
            if (_savedLedger.Length == 0) SessionState.EraseString(LedgerKey);
            else SessionState.SetString(LedgerKey, _savedLedger);
        }

        private static PlayEntryCandidate Candidate() =>
            new("revision-1", PurposeLaunchSettings.DirectoryPlay("root", "digest", "revision-1", "Full",
                SampleGameBuildPurposes.SpringFull.RuntimeDefaults, "Title"), false);

        private static PlayEntryFacts Facts(PlayEntryRoute route, bool titleOpen = true)
        {
            var facts = new PlayEntryFacts
            {
                RunPurpose = SampleGameBuildPurposes.SpringFull.Name,
                Route = route,
                IsFastMode = true,
                SourceEnvironment = PurposeLaunchSettings.PlayAs(SampleGameBuildPurposes.SpringFull).PlacedEnvironment,
                Candidate = Candidate(),
            };
            if (titleOpen)
            {
                facts.OpenScenes = new[] { ("Title", "Assets/SampleGame/Scenes/Title.unity") };
                facts.ReusableScenes = new[] { "Assets/SampleGame/Scenes/Title.unity" };
            }
            return facts;
        }

        private static PlayEntryConfigLayer EnvironmentLayer(params (string Name, string Value)[] values)
        {
            foreach (var (name, value) in values) Environment.SetEnvironmentVariable(name, value);
            return PlayEntryInterceptor.ReadEnvironmentLayer();
        }

        // ─── (b) 新しい経路は混成の経路と同じ拒否を同じ順で返す ───

        private static readonly Action<PlayEntryFacts>[] RejectingChanges =
        {
            f => f.IsSceneReloadDisabled = true,
            f => f.HasPlayModeStartScene = true,
            f => f.Candidate = null,
            f => { f.OpenScenes = new[] { ("Untitled", "") }; f.ReusableScenes = Array.Empty<string>(); },
            f => f.OpenScenes = new[] { ("UICommon", SampleGameBootstrapContentComposer.UiCommonPath) },
            f => f.ConflictReason = "conflict",
        };

        [Test]
        public void Rejections_AreTheSameAsBuiltWithOpenScenes_InTheSameOrder()
        {
            for (var i = 0; i < RejectingChanges.Length; i++)
            {
                // 1 つずつと、2 つ目以降を全部重ねた組で、どの拒否が先に当たるかも同じであること。
                foreach (var changes in new[] { new[] { RejectingChanges[i] }, RejectingChanges.Skip(i).ToArray() })
                {
                    var snapshot = Facts(PlayEntryRoute.BuiltWithOpenScenes);
                    var localEdits = Facts(PlayEntryRoute.BuiltWithLocalEdits);
                    foreach (var change in changes) { change(snapshot); change(localEdits); }
                    var expected = PlayEntryDecision.Decide(snapshot);
                    var actual = PlayEntryDecision.Decide(localEdits);
                    Assert.That(expected.Action, Is.EqualTo(PlayEntryAction.Reject), "case " + i);
                    Assert.That(actual.Action, Is.EqualTo(expected.Action), "case " + i);
                    Assert.That(actual.Rule, Is.EqualTo(expected.Rule), "case " + i);
                    Assert.That(actual.Reason, Is.EqualTo(expected.Reason), "case " + i);
                }
            }
        }

        [Test]
        public void PassingLocalEdits_IsTheNewRule_WithTheSnapshotValues()
        {
            var snapshot = PlayEntryDecision.Decide(Facts(PlayEntryRoute.BuiltWithOpenScenes));
            var plan = PlayEntryDecision.Decide(Facts(PlayEntryRoute.BuiltWithLocalEdits));
            Assert.That(plan.Action, Is.EqualTo(PlayEntryAction.Intervene));
            Assert.That(plan.Rule, Is.EqualTo(PlayEntryRule.LocalEditsRoute));
            Assert.That(plan.Environment, Is.EqualTo(snapshot.Environment), "判定の値は混成の経路と同じ。上書きの key は段の結果が足す");
            Assert.That(plan.Overlay, Is.Null);
        }

        // ─── (c) 再入の行 ───

        [Test]
        public void Reentry_PassesOnlyWithThePreparedSessionAndTheOnePlayLedger()
        {
            var facts = Facts(PlayEntryRoute.BuiltWithLocalEdits);
            facts.HasOnePlayLedger = true;
            facts.SnapshotMatchesOpenScenes = true;
            facts.EnvironmentLayer = EnvironmentLayer((RuntimeModeEnvironment, "directory"));
            Assert.That(PlayEntryDecision.Decide(facts).Rule, Is.EqualTo(PlayEntryRule.SnapshotReentry));

            facts.SnapshotMatchesOpenScenes = false;
            Assert.That(PlayEntryDecision.Decide(facts).Rule, Is.EqualTo(PlayEntryRule.DirectoryWithReusableScenes));
            facts.SnapshotMatchesOpenScenes = true;
            facts.HasOnePlayLedger = false;
            Assert.That(PlayEntryDecision.Decide(facts).Rule, Is.EqualTo(PlayEntryRule.DirectoryWithReusableScenes));
        }

        // ─── (d) 上書きの段の結果を足した値 ───

        [Test]
        public void WithOverlay_AddsTheTwoKeysOnlyWhenRegistering()
        {
            var plan = PlayEntryDecision.Decide(Facts(PlayEntryRoute.BuiltWithLocalEdits));
            var artifact = new PlayEntryOverlayArtifact(true, "overlay-1", @"D:\content\overlay-1", "", null);
            var registering = plan.WithOverlay(new PlayEntryOverlayResult(PlayEntryOverlayState.Built, 1, Array.Empty<PlayEntryUnbuildableRoot>(),
                0, 0, 0, 0, artifact));
            Assert.That(registering.Environment.Take(plan.Environment.Count), Is.EqualTo(plan.Environment));
            Assert.That(registering.Environment.Skip(plan.Environment.Count), Is.EqualTo(new[]
            {
                new KeyValuePair<string, string>(OverlayPathEnvironment, @"D:\content\overlay-1"),
                new KeyValuePair<string, string>(OverlayIdentityEnvironment, "overlay-1"),
            }));
            var notRegistering = plan.WithOverlay(new PlayEntryOverlayResult(PlayEntryOverlayState.NotRequired, 0,
                Array.Empty<PlayEntryUnbuildableRoot>(), 0, 0, null, null, null));
            Assert.That(notRegistering.Environment, Is.EqualTo(plan.Environment));
        }

        // ─── (e) 上書きの key は明示設定に入る。行の順は変えない ───

        // test の引数は public の型に限るので、経路は名前で受ける（PlayEntryRoute は internal）。
        private static readonly string[] AllRoutes =
            { nameof(PlayEntryRoute.Source), nameof(PlayEntryRoute.Built), nameof(PlayEntryRoute.BuiltWithOpenScenes), nameof(PlayEntryRoute.BuiltWithLocalEdits) };

        private static PlayEntryRoute Route(string name) => (PlayEntryRoute)Enum.Parse(typeof(PlayEntryRoute), name);

        [Test]
        public void E1_ExplicitDirectoryWithOverlayKeys_NoReusableScene_PassesThroughWithNothingWritten(
            [ValueSource(nameof(AllRoutes))] string route)
        {
            var facts = Facts(Route(route), titleOpen: false);
            facts.EnvironmentLayer = EnvironmentLayer((RuntimeModeEnvironment, "directory"), (RevisionPathEnvironment, "root"), (DigestEnvironment, "digest"),
                (OverlayPathEnvironment, "path"), (OverlayIdentityEnvironment, "identity"));
            var plan = PlayEntryDecision.Decide(facts);
            Assert.That(plan.Action, Is.EqualTo(PlayEntryAction.PassThrough));
            Assert.That(plan.Rule, Is.EqualTo(PlayEntryRule.ExplicitSetting));
            Assert.That(plan.Environment, Is.Empty);
        }

        [Test]
        public void E2_SameExplicitSettingWithTitleOpen_IsTheExistingRejection([ValueSource(nameof(AllRoutes))] string route)
        {
            var facts = Facts(Route(route));
            facts.EnvironmentLayer = EnvironmentLayer((RuntimeModeEnvironment, "directory"), (RevisionPathEnvironment, "root"), (DigestEnvironment, "digest"),
                (OverlayPathEnvironment, "path"), (OverlayIdentityEnvironment, "identity"));
            var plan = PlayEntryDecision.Decide(facts);
            Assert.That(plan.Action, Is.EqualTo(PlayEntryAction.Reject));
            Assert.That(plan.Rule, Is.EqualTo(PlayEntryRule.DirectoryWithReusableScenes));
        }

        [Test]
        public void E3_OnlyOverlayKeysWithoutMode_PassesThroughWithNothingWritten([ValueSource(nameof(AllRoutes))] string route)
        {
            var facts = Facts(Route(route));
            facts.EnvironmentLayer = EnvironmentLayer((OverlayPathEnvironment, "path"), (OverlayIdentityEnvironment, "identity"));
            Assert.That(facts.EnvironmentLayer.HasAny, Is.True);
            var plan = PlayEntryDecision.Decide(facts);
            Assert.That(plan.Action, Is.EqualTo(PlayEntryAction.PassThrough));
            Assert.That(plan.Rule, Is.EqualTo(PlayEntryRule.ExplicitSetting));
            Assert.That(plan.Environment, Is.Empty);
        }

        [Test]
        public void E4_ReentryAfterTheEntryPlacedOverlayKeys_IsTheReentryRow()
        {
            var facts = Facts(PlayEntryRoute.BuiltWithLocalEdits);
            facts.HasOnePlayLedger = true;
            facts.SnapshotMatchesOpenScenes = true;
            facts.EnvironmentLayer = EnvironmentLayer((RuntimeModeEnvironment, "directory"), (RevisionPathEnvironment, "root"), (DigestEnvironment, "digest"),
                (OverlayPathEnvironment, "path"), (OverlayIdentityEnvironment, "identity"));
            var plan = PlayEntryDecision.Decide(facts);
            Assert.That(plan.Rule, Is.EqualTo(PlayEntryRule.SnapshotReentry));
            Assert.That(plan.Rule, Is.Not.EqualTo(PlayEntryRule.ExplicitSetting));
        }

        [Test]
        public void ExplicitSettingKeys_AreTheModeKeysPlusTheOverlayKeys()
        {
            Assert.That(PlayEntryDecision.ExplicitSettingKeys, Is.EqualTo(PlayEntryDecision.ModeKeys
                .Concat(new[] { ContentOverlayPlayConfig.OverlayPathKey, ContentOverlayPlayConfig.OverlayIdentityKey })));
        }

        [Test]
        public void LocalEditsConflictKeys_AreTheSnapshotKeysPlusTheOverlayKeys()
        {
            var defaults = SampleGameBuildPurposes.SpringFull.RuntimeDefaults;
            Assert.That(PurposeLaunchSettings.LocalEditsPlayConfigKeys(defaults), Is.EqualTo(PurposeLaunchSettings.SnapshotPlayConfigKeys(defaults)
                .Concat(new[] { ContentOverlayPlayConfig.OverlayPathKey, ContentOverlayPlayConfig.OverlayIdentityKey })));
        }
    }
}
