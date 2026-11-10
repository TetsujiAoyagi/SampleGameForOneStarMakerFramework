#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// 取り消し → tick → 適用 → 再 Play の状態機械（AC2）。Play には入らず、Editor の事実と副作用を差し替えて、再 Play の同期発火は
    /// EnterPlaymode の差し替えの中から <see cref="PlayEntryTransaction.OnExitingEditMode"/> を呼んで再現する。
    /// </summary>
    public sealed class PlayEntryTransactionTests
    {
        private const string LedgerKey = "OSM.ContentDelivery.PlayBridge.v1";
        private const string Key = "SAMPLEGAME_PLAYENTRYTEST__VALUE";
        private static readonly IReadOnlyList<KeyValuePair<string, string>> Placed =
            new[] { new KeyValuePair<string, string>(Key, "placed") };

        private readonly List<string> _calls = new();
        private readonly List<bool> _validateArguments = new();
        private PlayEntryFacts _facts = new();
        private PlayEntryPlan? _lastBlocked;
        private bool _idle;
        private bool _saveAccepted;
        private Action _enterPlaymode = () => { };
        private Action<IReadOnlyList<KeyValuePair<string, string>>> _apply = _ => { };
        private PlayEntryTransaction _transaction = null!;
        private string _savedLedger = "";
        private string? _savedValue;

        [SetUp]
        public void SetUp()
        {
            _savedLedger = SessionState.GetString(LedgerKey, string.Empty);
            SessionState.EraseString(LedgerKey);
            _savedValue = Environment.GetEnvironmentVariable(Key);
            Environment.SetEnvironmentVariable(Key, "previous");

            _calls.Clear();
            _validateArguments.Clear();
            _lastBlocked = null;
            _idle = true;
            _saveAccepted = true;
            _facts = SourceFacts();
            _enterPlaymode = ReenterSynchronously;
            _apply = _ => { };
            _transaction = new PlayEntryTransaction(new PlayEntryPorts
            {
                CollectFacts = validate => { _validateArguments.Add(validate); _calls.Add("Collect"); return _facts; },
                CancelPlay = () => _calls.Add("Cancel"),
                IsIdle = () => _idle,
                ApplyForOnePlay = values => { _calls.Add("Apply"); _apply(values); },
                AlignFastMode = () => _calls.Add("Align"),
                EnterPlaymode = () => { _calls.Add("Enter"); _enterPlaymode(); },
                PublishPlanned = (_, _) => _calls.Add("Planned"),
                PublishBlocked = (_, plan) => { _calls.Add("Blocked"); _lastBlocked = plan; },
                ConfirmSaveScenes = () => { _calls.Add("Confirm"); return _saveAccepted; },
                ReplaceWithEmptyScene = () => _calls.Add("Replace"),
            });
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(Key, _savedValue);
            if (_savedLedger.Length == 0) SessionState.EraseString(LedgerKey);
            else SessionState.SetString(LedgerKey, _savedLedger);
        }

        private static PlayEntryFacts SourceFacts(bool fastMode = true) => new() { RunPurpose = "spring-full", SourceEnvironment = Placed, IsFastMode = fastMode };

        private static PlayEntryFacts OnePlayFacts() => new() { RunPurpose = "spring-full", HasOnePlayLedger = true };

        private static PlayEntryFacts DirectoryOnePlayFacts()
        {
            var facts = OnePlayFacts();
            facts.ReusableScenes = new[] { "Title" };
            facts.EnvironmentLayer = new PlayEntryConfigLayer(new Dictionary<string, string> { [PlayEntryDecision.RuntimeModeKey] = "directory" });
            return facts;
        }

        /// <summary>EnterPlaymode の呼び出しの中で ExitingEditMode が同期で来る（Phase A の疎通で確認した順序）。</summary>
        private void ReenterSynchronously()
        {
            _facts = OnePlayFacts();
            _transaction.OnExitingEditMode();
        }

        private int Count(string call) => _calls.FindAll(x => x == call).Count;

        // ═══ 通常の流れ ═══

        [Test]
        public void SourcePlay_CancelsThenAppliesAndEntersOnce()
        {
            _transaction.OnExitingEditMode();
            Assert.That(_calls, Is.EqualTo(new[] { "Collect", "Cancel" }));
            Assert.That(_transaction.CurrentPhase, Is.EqualTo(PlayEntryTransaction.Phase.WaitingForTick));

            _transaction.OnTick();
            Assert.That(_calls, Is.EqualTo(new[] { "Collect", "Cancel", "Collect", "Apply", "Planned", "Enter", "Collect" }),
                "再 Play の同期発火は素通りし、計画を増やさない");
            Assert.That(_transaction.CurrentPhase, Is.EqualTo(PlayEntryTransaction.Phase.Idle));
            _transaction.OnTick();
            Assert.That(Count("Enter"), Is.EqualTo(1));
        }

        [Test]
        public void OnlyTheFinalDecisionValidatesCandidates()
        {
            _transaction.OnExitingEditMode();
            _transaction.OnTick();
            Assert.That(_validateArguments, Is.EqualTo(new[] { false, true, false }));
        }

        [Test]
        public void PassThrough_DoesNotCancel_AndPublishesThePlan()
        {
            _facts = OnePlayFacts();
            _transaction.OnExitingEditMode();
            Assert.That(_calls, Is.EqualTo(new[] { "Collect", "Planned" }));
            Assert.That(_transaction.CurrentPhase, Is.EqualTo(PlayEntryTransaction.Phase.Idle));
        }

        [Test]
        public void BatchPlay_LeavesNoRecord()
        {
            _facts = new PlayEntryFacts { IsBatchMode = true };
            _transaction.OnExitingEditMode();
            Assert.That(_calls, Is.EqualTo(new[] { "Collect" }));
        }

        [Test]
        public void Tick_WaitsUntilTheEditorIsIdle()
        {
            _transaction.OnExitingEditMode();
            _idle = false;
            _transaction.OnTick();
            Assert.That(Count("Enter"), Is.Zero);
            Assert.That(_transaction.CurrentPhase, Is.EqualTo(PlayEntryTransaction.Phase.WaitingForTick));
            _idle = true;
            _transaction.OnTick();
            Assert.That(Count("Enter"), Is.EqualTo(1));
        }

        [Test]
        public void FastMode_IsAlignedOnlyAfterTheFinalDecision()
        {
            _facts = SourceFacts(fastMode: false);
            _transaction.OnExitingEditMode();
            Assert.That(Count("Align"), Is.Zero);
            _transaction.OnTick();
            Assert.That(_calls.IndexOf("Align"), Is.LessThan(_calls.IndexOf("Apply")));
            Assert.That(Count("Align"), Is.EqualTo(1));
        }

        // ═══ 2 度押し・再入 ═══

        [Test]
        public void SecondExitingEditMode_WhileCancelled_OnlyCancelsAgain()
        {
            _transaction.OnExitingEditMode();
            _transaction.OnExitingEditMode();
            Assert.That(Count("Cancel"), Is.EqualTo(2));
            Assert.That(Count("Collect"), Is.EqualTo(1), "計画は増やさない");
            _transaction.OnTick();
            Assert.That(Count("Enter"), Is.EqualTo(1));
        }

        [Test]
        public void Reentry_WithReusableSceneOpened_IsRejected_AndDoesNotInterveneAgain()
        {
            _enterPlaymode = () =>
            {
                _facts = DirectoryOnePlayFacts();
                _transaction.OnExitingEditMode();
            };
            _transaction.OnExitingEditMode();
            _transaction.OnTick();
            Assert.That(Count("Blocked"), Is.EqualTo(1));
            Assert.That(_lastBlocked!.Rule, Is.EqualTo(PlayEntryRule.DirectoryWithReusableScenes));
            Assert.That(Count("Cancel"), Is.EqualTo(2));
            Assert.That(_transaction.CurrentPhase, Is.EqualTo(PlayEntryTransaction.Phase.Idle));
            _transaction.OnTick();
            Assert.That(Count("Enter"), Is.EqualTo(1), "再 Play は 1 回だけ");
        }

        [Test]
        public void OnePlayLedgerWithReusableScene_IsFinalImmediately_NotRedecidedAfterTheLedgerIsGone()
        {
            _facts = DirectoryOnePlayFacts();
            _transaction.OnExitingEditMode();
            Assert.That(Count("Blocked"), Is.EqualTo(1));
            Assert.That(_transaction.CurrentPhase, Is.EqualTo(PlayEntryTransaction.Phase.Idle));

            // HandleIdle が一回の ledger を戻した後の状態でも、別の Play として始めない。
            _facts = SourceFacts();
            _transaction.OnTick();
            Assert.That(Count("Enter"), Is.Zero);
            Assert.That(Count("Apply"), Is.Zero);
        }

        [Test]
        public void EnteredPlayMode_WhileWaiting_DoesNotReplayAfterStop()
        {
            _transaction.OnExitingEditMode();
            _transaction.OnEnteredPlayMode();
            _transaction.OnTick();
            Assert.That(Count("Enter"), Is.Zero);
        }

        // ═══ 最終判断は tick ═══

        [Test]
        public void Tick_ReDecision_RejectsWithoutSideEffects()
        {
            _transaction.OnExitingEditMode();
            _facts = new PlayEntryFacts { RunPurpose = "spring-full", ConflictReason = "conflict", IsFastMode = false };
            _transaction.OnTick();
            Assert.That(Count("Blocked"), Is.EqualTo(1));
            Assert.That(Count("Apply"), Is.Zero);
            Assert.That(Count("Enter"), Is.Zero);
            Assert.That(Count("Align"), Is.Zero, "拒否の時は Play Mode Script を変えない");
        }

        [Test]
        public void Tick_ReDecision_PassThrough_EntersWithoutPlacing()
        {
            _transaction.OnExitingEditMode();
            _facts = OnePlayFacts();
            _transaction.OnTick();
            Assert.That(Count("Apply"), Is.Zero);
            Assert.That(Count("Enter"), Is.EqualTo(1));
        }

        [Test]
        public void CollectFailure_CancelsAndBlocksAtTheTick()
        {
            var failing = new PlayEntryTransaction(new PlayEntryPorts
            {
                CollectFacts = _ => throw new InvalidOperationException("broken"),
                CancelPlay = () => _calls.Add("Cancel"),
                PublishBlocked = (_, plan) => { _calls.Add("Blocked"); _lastBlocked = plan; },
                EnterPlaymode = () => _calls.Add("Enter"),
            });
            failing.OnExitingEditMode();
            failing.OnTick();
            Assert.That(_calls, Is.EqualTo(new[] { "Cancel", "Blocked" }));
            Assert.That(_lastBlocked!.Rule, Is.EqualTo(PlayEntryRule.EntryFailed));
            Assert.That(_lastBlocked.Reason, Does.Contain("broken"));
        }

        // ═══ 例外と ledger の戻し方（実物の ContentDeliveryPlayBridge）═══

        [Test]
        public void EnterPlaymodeFailure_BlocksAndHandleIdleRestoresTheOnePlayLedger()
        {
            _apply = ContentDeliveryPlayBridge.ApplyForOnePlay;
            _enterPlaymode = () => throw new InvalidOperationException("refused");
            _transaction.OnExitingEditMode();
            _transaction.OnTick();

            Assert.That(Count("Blocked"), Is.EqualTo(1));
            Assert.That(_lastBlocked!.Reason, Does.Contain("refused"));
            Assert.That(_transaction.CurrentPhase, Is.EqualTo(PlayEntryTransaction.Phase.Idle));
            Assert.That(Environment.GetEnvironmentVariable(Key), Is.EqualTo("placed"), "入口は Reset を呼ばない");

            ContentDeliveryPlayBridge.HandleIdle(isPlaying: false, isPlayingOrWillChangePlaymode: false);
            Assert.That(Environment.GetEnvironmentVariable(Key), Is.EqualTo("previous"));
            Assert.That(ContentDeliveryPlayBridge.HasOnePlayLedger, Is.False);
        }

        [Test]
        public void ApplyFailure_Blocks_AndDoesNotEnter()
        {
            _apply = _ => throw new InvalidOperationException("apply failed");
            _transaction.OnExitingEditMode();
            _transaction.OnTick();
            Assert.That(Count("Blocked"), Is.EqualTo(1));
            Assert.That(Count("Enter"), Is.Zero);
        }

        [Test]
        public void ManualLedger_IsUntouchedByARejection_AndByHandleIdle()
        {
            Environment.SetEnvironmentVariable(Key, "window");
            SessionState.SetString(LedgerKey, "{\"keys\":[\"" + Key + "\"],\"previous\":[\"previous\"],\"installed\":[\"window\"]}");
            _facts = new PlayEntryFacts
            {
                RunPurpose = "spring-full", HasManualLedger = true, ReusableScenes = new[] { "Title" },
                EnvironmentLayer = new PlayEntryConfigLayer(new Dictionary<string, string> { [PlayEntryDecision.RuntimeModeKey] = "directory" }),
            };

            _transaction.OnExitingEditMode();
            Assert.That(_transaction.CurrentPhase, Is.EqualTo(PlayEntryTransaction.Phase.WaitingForTick), "手動の ledger は tick まで残る");
            _transaction.OnTick();
            ContentDeliveryPlayBridge.HandleIdle(isPlaying: false, isPlayingOrWillChangePlaymode: false);

            Assert.That(Count("Blocked"), Is.EqualTo(1));
            Assert.That(ContentDeliveryPlayBridge.HasManualLedger, Is.True);
            Assert.That(Environment.GetEnvironmentVariable(Key), Is.EqualTo("window"));
        }

        // ═══ 混成の経路（snapshot の準備と解除）═══

        private readonly List<string> _snapshotCalls = new();

        /// <summary>
        /// port の状態から facts を作る混成の経路の模擬。ApplyForOnePlay の後は実効 mode が directory で 1 回の ledger があり、
        /// 準備の後は session が開いている Scene と一致する。判定は実際の <see cref="PlayEntryDecision.Decide"/> を通す。
        /// </summary>
        private sealed class SnapshotWorld
        {
            internal bool Prepared;
            internal bool Applied;
            internal bool SkipPrepare;
            internal bool FailPrepare;

            internal PlayEntryFacts Facts()
            {
                var facts = new PlayEntryFacts
                {
                    RunPurpose = "spring-full",
                    Route = PlayEntryRoute.BuiltWithOpenScenes,
                    Candidate = new PlayEntryCandidate("revision-1", PurposeLaunchSettings.DirectoryPlay("root", "digest", "revision-1", "Full",
                        SampleGameBuildPurposes.SpringFull.RuntimeDefaults, "Title"), false),
                    OpenScenes = new[] { ("Title", "Assets/SampleGame/Scenes/Title.unity") },
                    ReusableScenes = new[] { "Assets/SampleGame/Scenes/Title.unity" },
                    HasOnePlayLedger = Applied,
                    SnapshotMatchesOpenScenes = Prepared,
                };
                if (Applied)
                    facts.EnvironmentLayer = new PlayEntryConfigLayer(new Dictionary<string, string> { [PlayEntryDecision.RuntimeModeKey] = "directory" });
                return facts;
            }
        }

        private PlayEntryTransaction SnapshotTransaction(SnapshotWorld world)
        {
            _snapshotCalls.Clear();
            PlayEntryTransaction? transaction = null;
            transaction = new PlayEntryTransaction(new PlayEntryPorts
            {
                CollectFacts = _ => world.Facts(),
                CancelPlay = () => _snapshotCalls.Add("Cancel"),
                IsIdle = () => _idle,
                ApplyForOnePlay = _ => { _snapshotCalls.Add("Apply"); world.Applied = true; },
                EnterPlaymode = () =>
                {
                    _snapshotCalls.Add("Enter");
                    _enterPlaymode();
                    // EnterPlaymode の中で 2 回目の ExitingEditMode が同期で来る。
                    transaction!.OnExitingEditMode();
                },
                PublishPlanned = (_, plan) => _snapshotCalls.Add("Planned:" + plan.Rule),
                PublishBlocked = (_, plan) => { _snapshotCalls.Add("Blocked:" + plan.Rule); _lastBlocked = plan; },
                PrepareSnapshot = () =>
                {
                    _snapshotCalls.Add("Prepare");
                    if (world.FailPrepare) throw new InvalidOperationException("prepare failed");
                    if (!world.SkipPrepare) world.Prepared = true;
                },
                ReleaseSnapshot = () => { _snapshotCalls.Add("Release"); world.Prepared = false; },
                CloseObservation = () => _snapshotCalls.Add("Close"),
            });
            return transaction;
        }

        private int SnapshotCount(string call) => _snapshotCalls.FindAll(x => x == call).Count;

        [Test]
        public void Snapshot_Reentry_PassesOnlyInsideTheOwnEnterPlaymode()
        {
            _enterPlaymode = () => { };
            var world = new SnapshotWorld();
            var transaction = SnapshotTransaction(world);

            transaction.OnExitingEditMode();
            Assert.That(_snapshotCalls, Is.EqualTo(new[] { "Cancel" }), "1 回目の ExitingEditMode は取り消す");
            transaction.OnTick();

            Assert.That(_snapshotCalls, Is.EqualTo(new[] { "Cancel", "Prepare", "Apply", "Planned:SnapshotRoute", "Enter" }),
                "準備 → 宣言 → ledger → 再 Play の順で、2 回目の ExitingEditMode は再入の行で素通りする");
            Assert.That(SnapshotCount("Cancel"), Is.EqualTo(1));
            Assert.That(SnapshotCount("Enter"), Is.EqualTo(1));
            Assert.That(_snapshotCalls.FindAll(x => x.StartsWith("Blocked", StringComparison.Ordinal)), Is.Empty);
        }

        [Test]
        public void Snapshot_WithoutTheSession_TheReentryIsRejected()
        {
            _enterPlaymode = () => { };
            var world = new SnapshotWorld { SkipPrepare = true };
            var transaction = SnapshotTransaction(world);
            transaction.OnExitingEditMode();
            transaction.OnTick();
            Assert.That(_snapshotCalls, Does.Contain("Blocked:DirectoryWithReusableScenes"), "session が無ければ従来の拒否");
            Assert.That(SnapshotCount("Cancel"), Is.EqualTo(2));
        }

        [Test]
        public void Snapshot_ReentryRuleOutsideTheOwnEnterPlaymode_IsCancelledAndReleased()
        {
            // 前の Play の session と ledger が残った状態で、利用者が Play を押した。
            var world = new SnapshotWorld { Prepared = true, Applied = true };
            var transaction = SnapshotTransaction(world);
            transaction.OnExitingEditMode();
            Assert.That(_snapshotCalls, Is.EqualTo(new[] { "Cancel", "Release", "Blocked:SnapshotReentry" }));
            Assert.That(_lastBlocked!.Action, Is.EqualTo(PlayEntryAction.Reject));
            Assert.That(transaction.CurrentPhase, Is.EqualTo(PlayEntryTransaction.Phase.Idle));
        }

        [Test]
        public void Snapshot_PrepareFailure_DoesNotEnter_AndReleases()
        {
            var world = new SnapshotWorld { FailPrepare = true };
            var transaction = SnapshotTransaction(world);
            transaction.OnExitingEditMode();
            transaction.OnTick();
            Assert.That(_snapshotCalls, Is.EqualTo(new[] { "Cancel", "Prepare", "Release", "Blocked:EntryFailed" }));
            Assert.That(_lastBlocked!.Reason, Does.Contain("prepare failed"));
        }

        [Test]
        public void Snapshot_EnterPlaymodeFailure_Releases()
        {
            _enterPlaymode = () => throw new InvalidOperationException("refused");
            var world = new SnapshotWorld();
            var transaction = SnapshotTransaction(world);
            transaction.OnExitingEditMode();
            transaction.OnTick();
            Assert.That(_snapshotCalls, Is.EqualTo(new[] { "Cancel", "Prepare", "Apply", "Planned:SnapshotRoute", "Enter", "Release", "Blocked:EntryFailed" }));
        }

        [Test]
        public void NotReachingPlay_ReleasesAndClosesTheObservationOnTheNextIdleTick()
        {
            _enterPlaymode = () => { };
            var world = new SnapshotWorld();
            var transaction = SnapshotTransaction(world);
            transaction.OnExitingEditMode();
            _idle = true;
            transaction.OnTick();
            _snapshotCalls.Clear();

            // EnterPlaymode の要求が Play に至らず、Editor が idle に戻った。
            transaction.OnTick();
            Assert.That(_snapshotCalls, Is.EqualTo(new[] { "Release", "Close" }));
            transaction.OnTick();
            Assert.That(_snapshotCalls, Is.EqualTo(new[] { "Release", "Close" }), "1 回だけ");
        }

        [Test]
        public void ReachingPlay_DoesNotReleaseOnTheIdleTick()
        {
            _enterPlaymode = () => { };
            var world = new SnapshotWorld();
            var transaction = SnapshotTransaction(world);
            transaction.OnExitingEditMode();
            transaction.OnTick();
            transaction.OnEnteredPlayMode();
            _snapshotCalls.Clear();
            transaction.OnTick();
            Assert.That(_snapshotCalls, Is.Empty, "Play の後の解除は EnteredEditMode が持つ");
        }

        [Test]
        public void SourcePlay_NotReachingPlay_AlsoClosesTheObservation()
        {
            var closed = 0;
            var transaction = new PlayEntryTransaction(new PlayEntryPorts
            {
                CollectFacts = _ => SourceFacts(),
                CloseObservation = () => closed++,
            });
            transaction.OnExitingEditMode();
            transaction.OnTick();
            transaction.OnTick();
            Assert.That(closed, Is.EqualTo(1), "Planned のまま Play に至らない Play の観測を閉じる");
        }

        // ═══ 回復操作 ═══

        [Test]
        public void Recovery_SaveRefused_KeepsTheSceneAndDoesNotPlay()
        {
            _saveAccepted = false;
            Assert.That(_transaction.RecoverInEmptyScene(), Is.False);
            Assert.That(_calls, Is.EqualTo(new[] { "Confirm" }));
        }

        [Test]
        public void Recovery_SaveAccepted_ReplacesTheSceneThenEnters()
        {
            _enterPlaymode = () => { };
            Assert.That(_transaction.RecoverInEmptyScene(), Is.True);
            Assert.That(_calls, Is.EqualTo(new[] { "Confirm", "Replace", "Enter" }));
        }
    }
}
