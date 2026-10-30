#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.Rendering.Vfx;
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using SampleGame.InGame.Streaming;
using UnityEngine;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// major Event の driver: 受理（純関数）、1 枝 1 件、完了と終端の観測。
    /// 発行・unload・完了の付与は偽の delegate、終端は偽 terminal events。Add の task は手で完了させる。
    /// </summary>
    [TestFixture]
    public sealed class MajorEventDriverTests
    {
        private const string EventId = "Spring_Event_MultiCellProof";
        private const string OtherEventId = "Spring_Event_Other";

        private readonly List<ScriptableObject> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            _assets.Clear();
        }

        // AC1: 要求 1 回で Add 1 回。返る前に Loading で、捕捉した個体に完了の delegate が付いている。
        [Test]
        public void Request_WhileSeasonStable_IssuesOneAddAndAttachesCompletionBeforeReturn()
        {
            var f = CreateFixture();

            var result = f.Driver.Request(EventId, branchBusy: false);

            Assert.That(result, Is.EqualTo(MajorEventRequestResult.Accepted));
            Assert.That(f.Added, Is.EqualTo(new[] { EventId }));
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Loading));
            Assert.That(f.Completions.ContainsKey(f.Query.GetLoadedScene(EventId)!), Is.True);
            Assert.That(f.Logger.WarningCount, Is.Zero);
        }

        // AC2: 受理の理由（純関数）。Season・枝・停止・構造の各条件。
        [TestCase(AdmitCase.SeasonNotLoaded, nameof(MajorEventRejectReason.SeasonNotLoaded))]
        [TestCase(AdmitCase.SeasonReplaced, nameof(MajorEventRejectReason.SeasonReplaced))]
        [TestCase(AdmitCase.SeasonNotStable, nameof(MajorEventRejectReason.SeasonNotStable))]
        [TestCase(AdmitCase.BranchBusy, nameof(MajorEventRejectReason.BranchBusy))]
        [TestCase(AdmitCase.Stopped, nameof(MajorEventRejectReason.Stopped))]
        [TestCase(AdmitCase.OtherSeasonEvent, nameof(MajorEventRejectReason.NotSeasonChild))]
        [TestCase(AdmitCase.Unknown, nameof(MajorEventRejectReason.NotSeasonChild))]
        [TestCase(AdmitCase.Lighting, nameof(MajorEventRejectReason.NotOnDemand))]
        [TestCase(AdmitCase.Cell, nameof(MajorEventRejectReason.StreamByDistance))]
        public void Admit_RejectsWithReason(AdmitCase admitCase, string expected)
        {
            var f = CreateFixture();
            var season = f.Season;
            SceneBase? current = season;
            var stable = true;
            var branchBusy = false;
            var stopped = false;
            var identity = EventId;
            switch (admitCase)
            {
                case AdmitCase.SeasonNotLoaded:
                    current = null;
                    break;
                case AdmitCase.SeasonReplaced:
                    current = new TestSceneBase(season.SceneResource, f.Query, new StubController());
                    break;
                case AdmitCase.SeasonNotStable:
                    stable = false;
                    break;
                case AdmitCase.BranchBusy:
                    branchBusy = true;
                    break;
                case AdmitCase.Stopped:
                    stopped = true;
                    break;
                case AdmitCase.OtherSeasonEvent:
                    identity = "Summer_Event_X";
                    break;
                case AdmitCase.Unknown:
                    identity = "Spring_Foo";
                    break;
                case AdmitCase.Lighting:
                    identity = "Spring_Lighting";
                    break;
                case AdmitCase.Cell:
                    identity = "Spring_Cell_4_2";
                    break;
            }

            var result = MajorEventDriver.Admit(
                identity,
                season,
                current,
                stable,
                branchBusy,
                stopped,
                MajorEventDriverState.Idle,
                activeEventIdentity: null,
                out var reason);

            Assert.That(result, Is.EqualTo(MajorEventRequestResult.Rejected));
            Assert.That(reason.ToString(), Is.EqualTo(expected));
        }

        // AC2: driver の拒否は Add 0・Warning 1 回（Season・枝・停止・構造の代表）。
        [TestCase(AdmitCase.SeasonNotLoaded)]
        [TestCase(AdmitCase.SeasonReplaced)]
        [TestCase(AdmitCase.SeasonNotStable)]
        [TestCase(AdmitCase.BranchBusy)]
        [TestCase(AdmitCase.Stopped)]
        [TestCase(AdmitCase.OtherSeasonEvent)]
        [TestCase(AdmitCase.Lighting)]
        [TestCase(AdmitCase.Cell)]
        public void Request_Rejected_DoesNotIssueAndWarnsOnce(AdmitCase admitCase)
        {
            var f = CreateFixture();
            var identity = EventId;
            var branchBusy = false;
            switch (admitCase)
            {
                case AdmitCase.SeasonNotLoaded:
                    f.Query.Scenes.Remove(f.Season.SceneResource.Identity);
                    break;
                case AdmitCase.SeasonReplaced:
                    f.Query.Scenes[f.Season.SceneResource.Identity] =
                        new TestSceneBase(f.Season.SceneResource, f.Query, new StubController());
                    break;
                case AdmitCase.SeasonNotStable:
                    f.Query.Stable.Remove(f.Season.SceneResource.Identity);
                    break;
                case AdmitCase.BranchBusy:
                    branchBusy = true;
                    break;
                case AdmitCase.Stopped:
                    f.Driver.Stop();
                    break;
                case AdmitCase.OtherSeasonEvent:
                    identity = "Summer_Event_X";
                    break;
                case AdmitCase.Lighting:
                    identity = "Spring_Lighting";
                    break;
                case AdmitCase.Cell:
                    identity = "Spring_Cell_4_2";
                    break;
            }

            var result = f.Driver.Request(identity, branchBusy);

            Assert.That(result, Is.EqualTo(MajorEventRequestResult.Rejected));
            Assert.That(f.Added, Is.Empty);
            Assert.That(f.Logger.WarningCount, Is.EqualTo(1));
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Idle));
        }

        // AC2 の優先順位: 停止と枝・Season の生存は driver の状態より先に見る。消えかけの Event に
        // AlreadyActive / Ending と答えない（同じ identity の要求でも、理由は停止・枝・Season のもの）。
        [Test]
        public void Admit_StopBranchAndSeasonOutrankDriverState(
            [Values(AdmitCase.Stopped, AdmitCase.BranchBusy, AdmitCase.SeasonNotLoaded, AdmitCase.SeasonReplaced, AdmitCase.SeasonNotStable)]
            AdmitCase admitCase,
            [Values] ActiveState activeState)
        {
            var state = activeState switch
            {
                ActiveState.Loading => MajorEventDriverState.Loading,
                ActiveState.Live => MajorEventDriverState.Live,
                _ => MajorEventDriverState.Ending,
            };
            var f = CreateFixture();
            var season = f.Season;
            SceneBase? current = season;
            var stable = true;
            var branchBusy = false;
            var stopped = false;
            MajorEventRejectReason expected;
            switch (admitCase)
            {
                case AdmitCase.Stopped:
                    stopped = true;
                    expected = MajorEventRejectReason.Stopped;
                    break;
                case AdmitCase.BranchBusy:
                    branchBusy = true;
                    expected = MajorEventRejectReason.BranchBusy;
                    break;
                case AdmitCase.SeasonNotLoaded:
                    current = null;
                    expected = MajorEventRejectReason.SeasonNotLoaded;
                    break;
                case AdmitCase.SeasonReplaced:
                    current = new TestSceneBase(season.SceneResource, f.Query, new StubController());
                    expected = MajorEventRejectReason.SeasonReplaced;
                    break;
                default:
                    stable = false;
                    expected = MajorEventRejectReason.SeasonNotStable;
                    break;
            }

            var result = MajorEventDriver.Admit(
                EventId,
                season,
                current,
                stable,
                branchBusy,
                stopped,
                state,
                activeEventIdentity: EventId,
                out var reason);

            Assert.That(result, Is.EqualTo(MajorEventRequestResult.Rejected));
            Assert.That(reason, Is.EqualTo(expected));
        }

        // AC2 の優先順位: Season の生存は構造より先（Idle の Season 未ロード × Lighting は SeasonNotLoaded）、
        // 段 2 の内部は枝が先（枝の切替中 × Season 未ロードは BranchBusy）。
        [TestCase("Spring_Lighting", false, nameof(MajorEventRejectReason.SeasonNotLoaded))]
        [TestCase(EventId, true, nameof(MajorEventRejectReason.BranchBusy))]
        public void Admit_SeasonNotLoaded_CombinedOrder(string identity, bool branchBusy, string expected)
        {
            var f = CreateFixture();

            var result = MajorEventDriver.Admit(
                identity,
                f.Season,
                currentSeason: null,
                seasonStable: false,
                branchBusy,
                stopped: false,
                MajorEventDriverState.Idle,
                activeEventIdentity: null,
                out var reason);

            Assert.That(result, Is.EqualTo(MajorEventRequestResult.Rejected));
            Assert.That(reason.ToString(), Is.EqualTo(expected));
        }

        // AC2（Request 経由）: Loading の間に Season の unload が始まる（Query が Season を返さない）と、同じ identity の
        // 要求は Rejected(SeasonNotLoaded)・Warning 1 回で、Add も unload も増えず、状態は Loading のまま（拒否は遷移させない）。
        [Test]
        public void Request_WhileLoading_AfterSeasonUnloadStarts_IsRejectedWithoutTransition()
        {
            var f = CreateFixture();
            Assert.That(f.Driver.Request(EventId, false), Is.EqualTo(MajorEventRequestResult.Accepted));
            f.Query.Scenes.Remove(f.Season.SceneResource.Identity);
            f.Query.Stable.Remove(f.Season.SceneResource.Identity);

            var result = f.Driver.Request(EventId, false);

            Assert.That(result, Is.EqualTo(MajorEventRequestResult.Rejected));
            Assert.That(f.Logger.WarningCount, Is.EqualTo(1));
            Assert.That(f.Logger.Messages, Has.Exactly(1).Contains(nameof(MajorEventRejectReason.SeasonNotLoaded)));
            Assert.That(f.Added, Is.EqualTo(new[] { EventId }));
            Assert.That(f.Unloaded, Is.Empty);
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Loading));
        }

        // AC3: Loading・Live の同じ identity は AlreadyActive。同じ同期区間の 2 回で Add 1。
        [Test]
        public void Request_WhileLoadingOrLive_IsAlreadyActive_AndIssuesNoSecondAdd()
        {
            var f = CreateFixture();

            Assert.That(f.Driver.Request(EventId, false), Is.EqualTo(MajorEventRequestResult.Accepted));
            Assert.That(f.Driver.Request(EventId, false), Is.EqualTo(MajorEventRequestResult.AlreadyActive));
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Loading));

            f.CompleteAdd(stable: true);
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Live));
            Assert.That(f.Driver.Request(EventId, false), Is.EqualTo(MajorEventRequestResult.AlreadyActive));
            Assert.That(f.Added.Count, Is.EqualTo(1));
            Assert.That(f.Logger.WarningCount, Is.Zero);
        }

        // AC3: 完了の後（Ending）の要求は Rejected で Add 0。終端の観測の後は Add 1（Removed / CancelCleanedUp）。
        [TestCase(false)]
        [TestCase(true)]
        public void Request_AfterCompletion_RejectedUntilTerminal_ThenIssuesAgain(bool cancelCleanedUp)
        {
            var terminal = cancelCleanedUp ? SceneTerminalKind.CancelCleanedUp : SceneTerminalKind.Removed;
            var f = CreateFixture();
            f.Driver.Request(EventId, false);
            f.CompleteAdd(stable: true);

            f.NotifyCompletion();
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Ending));
            Assert.That(f.Unloaded, Is.EqualTo(new[] { EventId }));
            Assert.That(f.Driver.Request(EventId, false), Is.EqualTo(MajorEventRequestResult.Rejected));
            Assert.That(f.Added.Count, Is.EqualTo(1));

            f.EmitTerminal(EventId, terminal);
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Idle));
            Assert.That(f.Driver.Request(EventId, false), Is.EqualTo(MajorEventRequestResult.Accepted));
            Assert.That(f.Added.Count, Is.EqualTo(2));
        }

        // AC3: Loading 中に CancelCleanedUp で終端しても、Add の task が終わるまで Idle に戻らない。終わった後の要求は Add 1。
        [Test]
        public void TerminalDuringLoading_ReturnsToIdleOnlyAfterAddEnds()
        {
            var f = CreateFixture();
            f.Driver.Request(EventId, false);

            f.EmitTerminal(EventId, SceneTerminalKind.CancelCleanedUp);
            Assert.That(f.Driver.State, Is.Not.EqualTo(MajorEventDriverState.Idle));
            Assert.That(f.Driver.Request(EventId, false), Is.Not.EqualTo(MajorEventRequestResult.Accepted));

            f.CompleteAdd(stable: false);
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Idle));
            Assert.That(f.Driver.Request(EventId, false), Is.EqualTo(MajorEventRequestResult.Accepted));
            Assert.That(f.Added.Count, Is.EqualTo(2));
        }

        // AC3: 完了の通知が Add の await の戻りより前（Loading）に届いても Ending になり、unload が 1 回発行される。
        [Test]
        public void CompletionDuringLoading_EndsAndUnloadsOnce()
        {
            var f = CreateFixture();
            f.Driver.Request(EventId, false);

            f.NotifyCompletion();
            f.NotifyCompletion();
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Ending));
            Assert.That(f.Unloaded, Is.EqualTo(new[] { EventId }));

            f.CompleteAdd(stable: true);
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Ending), "Add の戻りで Live に戻さない");
            f.EmitTerminal(EventId, SceneTerminalKind.Removed);
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Idle));
        }

        // AC3: A が Loading・Live の間の別 identity B は Rejected で Add 0。A の終端の後の B は Add 1。
        [Test]
        public void OtherIdentity_WhileActive_IsRejected_ThenAcceptedAfterTerminal()
        {
            var f = CreateFixture();
            f.Driver.Request(EventId, false);

            Assert.That(f.Driver.Request(OtherEventId, false), Is.EqualTo(MajorEventRequestResult.Rejected));
            f.CompleteAdd(stable: true);
            Assert.That(f.Driver.Request(OtherEventId, false), Is.EqualTo(MajorEventRequestResult.Rejected));
            Assert.That(f.Added, Is.EqualTo(new[] { EventId }));
            Assert.That(f.Logger.WarningCount, Is.EqualTo(2));

            f.NotifyCompletion();
            f.EmitTerminal(EventId, SceneTerminalKind.Removed);
            Assert.That(f.Driver.Request(OtherEventId, false), Is.EqualTo(MajorEventRequestResult.Accepted));
            Assert.That(f.Added, Is.EqualTo(new[] { EventId, OtherEventId }));
        }

        // 停止の後は完了の通知で unload を発行しない（回収は Framework の親子の unload に任せる）。
        [Test]
        public void Stop_IgnoresLaterCompletion()
        {
            var f = CreateFixture();
            f.Driver.Request(EventId, false);
            f.CompleteAdd(stable: true);

            f.Driver.Stop();
            f.NotifyCompletion();

            Assert.That(f.Unloaded, Is.Empty);
            Assert.That(f.Driver.Request(EventId, false), Is.EqualTo(MajorEventRequestResult.Rejected));
        }

        // 発行の失敗（捕捉なし）: Add の task が成功で終わるまで Ending で、2 本目の Add を出さない。終わったら Error 1 回で閉じる。
        [Test]
        public void IssueWithoutCapture_StaysEndingUntilAddEnds()
        {
            var f = CreateFixture();
            f.CaptureBeforeAwait = false;

            Assert.That(f.Driver.Request(EventId, false), Is.EqualTo(MajorEventRequestResult.Accepted));
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Ending));
            Assert.That(f.Driver.Request(EventId, false), Is.EqualTo(MajorEventRequestResult.Rejected));
            Assert.That(f.Added.Count, Is.EqualTo(1));

            f.CompleteAdd(stable: true);
            Assert.That(f.Driver.State, Is.EqualTo(MajorEventDriverState.Idle));
            Assert.That(f.Logger.ErrorCount, Is.EqualTo(1));
            Assert.That(f.Unloaded, Is.EqualTo(new[] { EventId }), "追跡できない個体を残さない");
        }

        private Fixture CreateFixture()
        {
            var spring = Track(SceneTestHelper.CreateSceneResource("Season_Spring"));
            AddChild(spring, "Spring_Lighting", LoadType.NecessaryAlways);
            AddChild(spring, "Spring_Cell_4_2", LoadType.OnDemand, streamByDistance: true);
            var resources = new Dictionary<string, SceneResource>(StringComparer.Ordinal)
            {
                [EventId] = AddChild(spring, EventId, LoadType.OnDemand),
                [OtherEventId] = AddChild(spring, OtherEventId, LoadType.OnDemand),
            };
            var summer = Track(SceneTestHelper.CreateSceneResource("Season_Summer"));
            AddChild(summer, "Summer_Event_X", LoadType.OnDemand);

            return new Fixture(spring, resources);
        }

        private SceneResource AddChild(SceneResource parent, string identity, LoadType loadType, bool streamByDistance = false)
        {
            var child = Track(SceneTestHelper.CreateSceneResource(
                identity,
                loadType: loadType,
                parent: parent,
                streamByDistance: streamByDistance));
            SceneTestHelper.AddChild(parent, child);
            return child;
        }

        private SceneResource Track(SceneResource resource)
        {
            _assets.Add(resource);
            return resource;
        }

        public enum AdmitCase
        {
            SeasonNotLoaded,
            SeasonReplaced,
            SeasonNotStable,
            BranchBusy,
            Stopped,
            OtherSeasonEvent,
            Unknown,
            Lighting,
            Cell,
        }

        // driver の状態は internal なので、public の test の引数には Idle でない 3 つを写したこの enum を使う。
        public enum ActiveState
        {
            Loading,
            Live,
            Ending,
        }

        private sealed class Fixture
        {
            private readonly Dictionary<string, SceneResource> _resources;
            private readonly FakeTerminalEvents _terminals = new();
            private UniTaskCompletionSource? _pendingAdd;
            private string? _pendingIdentity;

            internal Fixture(SceneResource seasonResource, Dictionary<string, SceneResource> resources)
            {
                _resources = resources;
                Season = new TestSceneBase(seasonResource, Query, new StubController());
                Query.Scenes[seasonResource.Identity] = Season;
                Query.Stable.Add(seasonResource.Identity);
                Driver = new MajorEventDriver(
                    Season,
                    Query,
                    _terminals,
                    Issue,
                    identity =>
                    {
                        Unloaded.Add(identity);
                        return UniTask.CompletedTask;
                    },
                    (scene, onCompleted) => Completions[scene] = onCompleted,
                    Logger);
            }

            internal TestSceneBase Season { get; }
            internal FakeQuery Query { get; } = new();
            internal MajorEventDriver Driver { get; }
            internal FakeVfxLogger Logger { get; } = new();
            internal List<string> Added { get; } = new();
            internal List<string> Unloaded { get; } = new();
            internal Dictionary<SceneBase, Action> Completions { get; } = new();
            internal bool CaptureBeforeAwait { get; set; } = true;

            internal void CompleteAdd(bool stable)
            {
                if (stable)
                {
                    Query.Stable.Add(_pendingIdentity!);
                }

                _pendingAdd!.TrySetResult();
            }

            internal void NotifyCompletion()
            {
                Completions[Query.GetLoadedScene(_pendingIdentity!)!]();
            }

            internal void EmitTerminal(string identity, SceneTerminalKind kind)
            {
                Query.Scenes.Remove(identity);
                Query.Stable.Remove(identity);
                _terminals.Emit(identity, kind);
            }

            private (SceneBase? Captured, UniTask Add) Issue(string identity)
            {
                Added.Add(identity);
                var scene = new TestSceneBase(_resources[identity], Query, new StubController());
                Query.Scenes[identity] = scene;
                _pendingAdd = new UniTaskCompletionSource();
                _pendingIdentity = identity;
                return (CaptureBeforeAwait ? scene : null, _pendingAdd.Task);
            }
        }

        private sealed class FakeQuery : ISceneQuery
        {
            internal Dictionary<string, SceneBase> Scenes { get; } = new(StringComparer.Ordinal);
            internal HashSet<string> Stable { get; } = new(StringComparer.Ordinal);

            public SceneBase? GetLoadedScene(string identity)
                => Scenes.TryGetValue(identity, out var scene) ? scene : null;

            public bool IsSceneLoaded(string identity) => Scenes.ContainsKey(identity);
            public bool IsSceneStable(string identity) => Stable.Contains(identity);
        }

        private sealed class StubController : ISceneController
        {
            public UniTask AddScene(string sceneIdentify, Func<UniTask>? afterOnLoadedTask, CancellationToken ct, SceneContext? context = null, IProgress<SceneLoadProgress>? progress = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, IReadOnlyDictionary<string, string>? telemetryTags = null, int priority = 100, TelemetryLevel telemetryLevel = TelemetryLevel.Summary)
                => UniTask.CompletedTask;

            public UniTask UnloadScene(string sceneIdentify, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, IReadOnlyDictionary<string, string>? telemetryTags = null, TelemetryLevel telemetryLevel = TelemetryLevel.Summary)
                => UniTask.CompletedTask;

            public UniTask SwitchScene(string? fromSceneIdentify, string toSceneIdentify, CancellationToken ct, SceneContext? context = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen, IReadOnlyDictionary<string, string>? telemetryTags = null)
                => UniTask.CompletedTask;

            public UniTask GoBack(CancellationToken ct, SceneContext? context = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen, IReadOnlyDictionary<string, string>? telemetryTags = null)
                => UniTask.CompletedTask;

            public void ClearHistory()
            {
            }
        }
    }
}
