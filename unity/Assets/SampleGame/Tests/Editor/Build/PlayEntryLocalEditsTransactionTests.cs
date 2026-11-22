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
    /// 経路 Built + local edits の順序（受け入れ条件 AC4 の (a)・(g)・(p)）。Play には入らず、port を fake にする。判定は実際の
    /// <see cref="PlayEntryDecision.Decide"/> を通し、再 Play の同期の ExitingEditMode は EnterPlaymode の fake の中から呼ぶ。
    /// </summary>
    public sealed class PlayEntryLocalEditsTransactionTests
    {
        private readonly List<string> _calls = new();
        private IReadOnlyList<KeyValuePair<string, string>> _applied = Array.Empty<KeyValuePair<string, string>>();
        private PlayEntryPlan? _planned;
        private PlayEntryPlan? _blocked;
        private string _confirmMessage = "";

        private sealed class World
        {
            internal PlayEntryRoute Route = PlayEntryRoute.BuiltWithLocalEdits;
            internal bool Prepared;
            internal bool Applied;
            internal bool FailCollect;
            internal bool SceneReloadDisabled;
            internal IReadOnlyList<string> Unsaved = Array.Empty<string>();
            internal bool AcceptSave = true;
            internal Func<PlayEntryOverlayResult> Overlay = () => Result(PlayEntryOverlayState.NotRequired);
            internal Func<bool> Busy = () => false;
            internal bool FailPrepare;

            internal PlayEntryFacts Facts()
            {
                if (FailCollect) throw new InvalidOperationException("collect failed");
                var facts = new PlayEntryFacts
                {
                    RunPurpose = "spring-full",
                    Route = Route,
                    Candidate = new PlayEntryCandidate("revision-1", PurposeLaunchSettings.DirectoryPlay("root", "digest", "revision-1", "Full",
                        SampleGameBuildPurposes.SpringFull.RuntimeDefaults, "Title"), false),
                    OpenScenes = new[] { ("Title", "Assets/SampleGame/Scenes/Title.unity") },
                    ReusableScenes = new[] { "Assets/SampleGame/Scenes/Title.unity" },
                    HasOnePlayLedger = Applied,
                    SnapshotMatchesOpenScenes = Prepared,
                    IsSceneReloadDisabled = SceneReloadDisabled,
                };
                if (Applied)
                    facts.EnvironmentLayer = new PlayEntryConfigLayer(new Dictionary<string, string> { [PlayEntryDecision.RuntimeModeKey] = "directory" });
                return facts;
            }
        }

        private static PlayEntryOverlayResult Result(PlayEntryOverlayState state, string? identity = null) =>
            new(state, identity == null ? 0 : 1, Array.Empty<PlayEntryUnbuildableRoot>(), 0.5, 1.5, identity == null ? null : 0.1,
                state == PlayEntryOverlayState.Built ? 3.0 : null,
                identity == null ? null : new PlayEntryOverlayArtifact(true, identity, @"D:\content\" + identity, "", null));

        private PlayEntryTransaction Transaction(World world)
        {
            _calls.Clear();
            PlayEntryTransaction? transaction = null;
            transaction = new PlayEntryTransaction(new PlayEntryPorts
            {
                CollectFacts = _ => world.Facts(),
                CancelPlay = () => _calls.Add("Cancel"),
                IsIdle = () => true,
                ApplyForOnePlay = values => { _calls.Add("Apply"); _applied = values; world.Applied = true; },
                EnterPlaymode = () => { _calls.Add("Enter"); transaction!.OnExitingEditMode(); },
                PublishPlanned = (_, plan) => { _calls.Add("Planned:" + plan.Rule); _planned = plan; },
                PublishBlocked = (_, plan) => { _calls.Add("Blocked:" + plan.Rule); _blocked = plan; },
                PrepareSnapshot = () =>
                {
                    _calls.Add("Prepare");
                    if (world.FailPrepare) throw new InvalidOperationException("prepare failed");
                    world.Prepared = true;
                },
                ReleaseSnapshot = () => { _calls.Add("Release"); world.Prepared = false; },
                EnumerateUnsavedAssets = () => { _calls.Add("Enumerate"); return world.Unsaved; },
                ConfirmSaveAssets = (_, message) => { _calls.Add("Confirm"); _confirmMessage = message; return world.AcceptSave; },
                DiscardSaveAssetsResponse = () => _calls.Add("Discard"),
                SaveAssets = () => _calls.Add("Save"),
                ApplyImports = () => _calls.Add("Imports"),
                IsCompilingOrUpdating = () => world.Busy(),
                RunOverlay = _ => { _calls.Add("Overlay"); return world.Overlay(); },
            });
            return transaction;
        }

        private static readonly string[] LocalEditsPorts = { "Enumerate", "Confirm", "Discard", "Save", "Imports", "Overlay" };

        private void Press(PlayEntryTransaction transaction)
        {
            transaction.OnExitingEditMode();
            transaction.OnTick();
        }

        // ─── (a) 順序 ───

        [Test]
        public void NoUnsavedAssets_Order_IsEnumerateThenSnapshotThenImportsThenOverlayThenLedger()
        {
            var world = new World { Overlay = () => Result(PlayEntryOverlayState.Built, "overlay-1") };
            Press(Transaction(world));
            Assert.That(_calls, Is.EqualTo(new[]
                { "Cancel", "Enumerate", "Discard", "Prepare", "Imports", "Overlay", "Apply", "Planned:LocalEditsRoute", "Enter" }));
        }

        [Test]
        public void UnsavedAssetsAccepted_SavesOnce_BeforeTheSnapshot()
        {
            var world = new World { Unsaved = new[] { "Assets/M.mat" }, AcceptSave = true };
            Press(Transaction(world));
            Assert.That(_calls, Is.EqualTo(new[]
                { "Cancel", "Enumerate", "Confirm", "Save", "Prepare", "Imports", "Overlay", "Apply", "Planned:LocalEditsRoute", "Enter" }));
        }

        // test の引数は public の型に限るので、internal の enum は名前で受ける。
        [TestCase(nameof(PlayEntryRoute.BuiltWithOpenScenes))]
        [TestCase(nameof(PlayEntryRoute.Built))]
        public void ExistingRoutes_CallNoneOfTheNewPorts(string route)
        {
            var world = new World { Route = (PlayEntryRoute)Enum.Parse(typeof(PlayEntryRoute), route), Unsaved = new[] { "Assets/M.mat" } };
            Press(Transaction(world));
            Assert.That(_calls.Intersect(LocalEditsPorts), Is.Empty);
        }

        [Test]
        public void RejectedDecisionOrFactFailure_CallsNoneOfTheNewPorts()
        {
            var rejected = new World { SceneReloadDisabled = true, Unsaved = new[] { "Assets/M.mat" } };
            Press(Transaction(rejected));
            Assert.That(_calls.Intersect(LocalEditsPorts), Is.Empty);
            Assert.That(_calls.Last(), Is.EqualTo("Blocked:SnapshotUnsupportedSetting"));

            var failing = new World { FailCollect = true };
            var transaction = Transaction(failing);
            transaction.OnExitingEditMode();
            transaction.OnTick();
            Assert.That(_calls.Intersect(LocalEditsPorts), Is.Empty);
        }

        // ─── (d)(b) 置く値 ───

        [Test]
        public void RegisteringPlay_PlacesTheSnapshotValuesPlusTheTwoOverlayKeys()
        {
            var world = new World { Overlay = () => Result(PlayEntryOverlayState.Reused, "overlay-1") };
            Press(Transaction(world));
            var expected = PurposeLaunchSettings.SnapshotPlay(world.Facts().Candidate!.PlacedEnvironment).Select(x => x.Key)
                .Concat(new[]
                {
                    PurposeLaunchSettings.EnvironmentName(ContentOverlayPlayConfig.OverlayPathKey),
                    PurposeLaunchSettings.EnvironmentName(ContentOverlayPlayConfig.OverlayIdentityKey),
                });
            Assert.That(_applied.Select(x => x.Key), Is.EquivalentTo(expected));
            Assert.That(_applied.Single(x => x.Key.EndsWith("CONTENTOVERLAYIDENTITY", StringComparison.Ordinal)).Value, Is.EqualTo("overlay-1"));
            Assert.That(_planned!.Overlay!.State, Is.EqualTo(PlayEntryOverlayState.Reused));
        }

        [TestCase(nameof(PlayEntryOverlayState.NotRequired))]
        [TestCase(nameof(PlayEntryOverlayState.UnbuildableOnly))]
        public void NonRegisteringPlay_PlacesTheSnapshotValuesOnly(string state)
        {
            var world = new World { Overlay = () => Result((PlayEntryOverlayState)Enum.Parse(typeof(PlayEntryOverlayState), state)) };
            Press(Transaction(world));
            var expected = PurposeLaunchSettings.SnapshotPlay(world.Facts().Candidate!.PlacedEnvironment).Select(x => x.Key);
            Assert.That(_applied.Select(x => x.Key), Is.EquivalentTo(expected));
            Assert.That(_applied.Any(x => x.Key.Contains("CONTENTOVERLAY")), Is.False);
        }

        // ─── (g)(n) 失敗 ───

        [Test]
        public void SnapshotFailure_DoesNotEnter_NorPlaceTheLedger()
        {
            var world = new World { FailPrepare = true };
            Press(Transaction(world));
            Assert.That(_calls, Does.Not.Contain("Apply").And.Not.Contain("Enter").And.Not.Contain("Overlay"));
            Assert.That(_calls, Does.Contain("Release"));
            Assert.That(_calls.Last(), Is.EqualTo("Blocked:EntryFailed"));
        }

        [Test]
        public void OverlayFailure_DoesNotEnter_ReleasesTheSnapshot_AndShowsTheReason()
        {
            var world = new World { Overlay = () => throw new PlayEntryOverlayException("The overlay build failed: SourceChangedDuringBuild Assets/M.mat: x") };
            Press(Transaction(world));
            Assert.That(_calls, Does.Not.Contain("Apply").And.Not.Contain("Enter"));
            Assert.That(_calls, Does.Contain("Release"));
            Assert.That(_blocked!.Rule, Is.EqualTo(PlayEntryRule.EntryFailed));
            Assert.That(_blocked.Reason, Does.Contain("SourceChangedDuringBuild").And.Contain("Assets/M.mat"));
        }

        [Test]
        public void PlanOrReuseCheckFailure_DoesNotEnterWithAnotherRoutesValues()
        {
            var world = new World { Overlay = () => throw new InvalidOperationException("plan failed") };
            Press(Transaction(world));
            Assert.That(_calls, Does.Not.Contain("Apply").And.Not.Contain("Enter"));
            Assert.That(_blocked!.Rule, Is.EqualTo(PlayEntryRule.EntryFailed));
        }

        [Test]
        public void CompilingAfterTheImports_StopsBeforeTheOverlay()
        {
            var world = new World { Busy = () => true };
            Press(Transaction(world));
            Assert.That(_calls, Does.Not.Contain("Overlay").And.Not.Contain("Apply").And.Not.Contain("Enter"));
            Assert.That(_calls, Does.Contain("Release"));
            Assert.That(_blocked!.Reason, Does.Contain("compiling"));
        }

        [Test]
        public void CompilingAfterTheOverlay_StopsBeforeTheLedger()
        {
            var busy = false;
            var world = new World { Busy = () => busy, Overlay = () => { busy = true; return Result(PlayEntryOverlayState.Built, "overlay-1"); } };
            Press(Transaction(world));
            Assert.That(_calls, Does.Contain("Overlay"));
            Assert.That(_calls, Does.Not.Contain("Apply").And.Not.Contain("Enter"));
            Assert.That(_calls, Does.Contain("Release"));
        }

        // ─── (p) 未保存の asset の確認 ───

        [Test]
        public void UnsavedAssetsDeclined_WritesNothing_AndBlocks()
        {
            var world = new World { Unsaved = new[] { "Assets/M.mat", "Assets/N.asset" }, AcceptSave = false };
            Press(Transaction(world));
            Assert.That(_calls, Is.EqualTo(new[] { "Cancel", "Enumerate", "Confirm", "Blocked:LocalEditsRoute" }));
            Assert.That(_blocked!.Action, Is.EqualTo(PlayEntryAction.Reject));
            Assert.That(_blocked.Reason, Does.Contain("Assets/M.mat"));
        }

        [Test]
        public void ConfirmationMessage_ListsTheAssets_AndSaysEverythingIsSaved()
        {
            var world = new World { Unsaved = new[] { "Assets/M.mat", "Assets/N.asset" } };
            Press(Transaction(world));
            Assert.That(_confirmMessage, Does.Contain("Assets/M.mat").And.Contain("Assets/N.asset"));
            Assert.That(_confirmMessage, Does.Contain(PlayEntryTransaction.SaveAllAssetsNotice));
        }

        [Test]
        public void NoUnsavedAssetsDetected_NeitherConfirmsNorSaves_AndDiscardsAPresetResponse()
        {
            var world = new World();
            Press(Transaction(world));
            Assert.That(_calls, Does.Contain("Discard"));
            Assert.That(_calls, Does.Not.Contain("Confirm").And.Not.Contain("Save"));
            // 0 件を「未保存の asset は無い」と表示しない（検出できた範囲だけを述べる）。
            Assert.That(_planned!.Warnings.Any(x => x.IndexOf("no unsaved", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
        }
    }
}
