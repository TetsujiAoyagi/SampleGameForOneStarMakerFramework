#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.Streaming;
using OneStarMaker.Tests.Rendering.Vfx;
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using SampleGame.InGame.Streaming;
using SampleGame.InGame.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// S-4b: 枝操作の直列化、Add≠Stable、Unload 早期 return、Stop 後の新規発行 0。
    /// 時間はシグナル。具象 SceneDirector は使わない。
    /// </summary>
    [TestFixture]
    public sealed class SessionSeasonControllerTests
    {
        private readonly List<ScriptableObject> _assets = new();
        private readonly List<SessionSeasonController> _seasons = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var season in _seasons)
            {
                season.Dispose();
            }

            _seasons.Clear();
            foreach (var asset in _assets)
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            _assets.Clear();
        }

        [UnityTest]
        public IEnumerator Ensure_DoesNotBlockCaller_UntilStable()
            => UniTask.ToCoroutine(async () =>
        {
            var fixture = CreateFixture();
            fixture.Controller.HoldStableUntilReleased = true;

            var ensure = fixture.Season.EnsureInitialWorldAsync(CancellationToken.None);
            Assert.That(fixture.Season.IsWorldReady, Is.False);
            Assert.That(ensure.Status, Is.EqualTo(UniTaskStatus.Pending));

            fixture.Query.Stable.Add("Season_Spring");
            fixture.Query.Stable.Add("Spring_Lighting");
            fixture.Controller.ReleaseHeldAdds();
            await UniTask.WaitUntil(() => fixture.Query.IsSceneLoaded("Spring_Cell_0_4"));
            fixture.Query.Stable.Add("Spring_Cell_0_4");
            fixture.Controller.ReleaseHeldAdds();
            await ensure;

            Assert.That(fixture.Season.IsWorldReady, Is.True);
            Assert.That(fixture.Season.IsStreamingActive, Is.True);
        });

        [UnityTest]
        public IEnumerator Ensure_AddCompleteIsNotStable()
            => UniTask.ToCoroutine(async () =>
        {
            var fixture = CreateFixture();
            fixture.Controller.HoldStableUntilReleased = true;

            var ensure = fixture.Season.EnsureInitialWorldAsync(CancellationToken.None);
            fixture.Query.Stable.Add("Season_Spring");
            fixture.Query.Stable.Add("Spring_Lighting");
            fixture.Controller.ReleaseHeldAdds();
            await UniTask.WaitUntil(() => fixture.Query.IsSceneLoaded("Spring_Cell_0_4"));
            Assert.That(fixture.Query.IsSceneLoaded("Spring_Cell_0_4"), Is.True);
            Assert.That(fixture.Query.IsSceneStable("Spring_Cell_0_4"), Is.False);
            Assert.That(fixture.Season.IsWorldReady, Is.False);

            fixture.Query.Stable.Add("Spring_Cell_0_4");
            fixture.Controller.ReleaseHeldAdds();
            await ensure;
            Assert.That(fixture.Season.IsWorldReady, Is.True);
        });

        [Test]
        public void RequestSeason_WhileBusy_RejectsOtherSeason()
        {
            var fixture = CreateFixture();
            fixture.Controller.HoldStableUntilReleased = true;
            var first = fixture.Season.EnsureInitialWorldAsync(CancellationToken.None);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await fixture.Season.RequestSeasonAsync("Summer", 0, 4, CancellationToken.None));

            fixture.Season.StopWorldOperations();
            first.Forget();
        }

        [UnityTest]
        public IEnumerator UnloadEarlyReturn_IsNotLifetimeEnd()
            => UniTask.ToCoroutine(async () =>
        {
            var fixture = CreateFixture();
            await fixture.Season.EnsureInitialWorldAsync(CancellationToken.None);

            // UnloadScene は即 return しても、捕捉個体の終端イベントまでは切替を完了しない。
            // 次 Season の Add は旧枝の発行済み Add（Lighting / 源流 Cell）の終端が揃ってから。
            fixture.Controller.UnloadReturnsImmediately = true;
            fixture.Controller.HoldStableUntilReleased = true;
            var switchTask = fixture.Season.RequestSeasonAsync("Summer", 0, 4, CancellationToken.None);
            await UniTask.Yield();
            Assert.That(switchTask.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(fixture.Controller.Unloaded.Contains("Season_Spring"), Is.True);

            fixture.Terminals.Emit("Season_Spring", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Spring_Lighting", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Spring_Cell_0_4", SceneTerminalKind.Removed);

            await UniTask.WaitUntil(() => fixture.Query.IsSceneLoaded("Season_Summer"));
            Assert.That(switchTask.Status, Is.EqualTo(UniTaskStatus.Pending));
            fixture.Query.Stable.Add("Season_Summer");
            fixture.Query.Stable.Add("Summer_Lighting");
            fixture.Controller.ReleaseHeldAdds();

            await UniTask.WaitUntil(() => fixture.Query.IsSceneLoaded("Summer_Cell_0_4"));
            Assert.That(switchTask.Status, Is.EqualTo(UniTaskStatus.Pending));
            fixture.Query.Stable.Add("Summer_Cell_0_4");
            fixture.Controller.ReleaseHeldAdds();
            await switchTask;
        });

        [UnityTest]
        public IEnumerator SeasonSwitch_WithEnvironmentCompanionAndDistanceRemove_CompletesBoundOps()
            => UniTask.ToCoroutine(async () =>
        {
            var fixture = CreateBranchConvergenceFixture();
            await fixture.Season.EnsureInitialWorldAsync(CancellationToken.None);
            await UniTask.WaitUntil(() => fixture.Controller.Added.Contains("Spring_Environment_0_4"));

            fixture.StreamingDriver!.Controller.Tick(new Vector3(125f, 48f, 1125f + 10000f));
            Assert.That(fixture.StreamingBackend!.Removed, Does.Contain("Spring_Cell_0_4"));

            fixture.Controller.UnloadReturnsImmediately = true;
            fixture.Controller.HoldStableUntilReleased = true;
            var switchTask = fixture.Season.RequestSeasonAsync("Summer", 0, 4, CancellationToken.None);
            await UniTask.Yield();
            Assert.That(switchTask.Status, Is.EqualTo(UniTaskStatus.Pending));

            fixture.Terminals.Emit("Season_Spring", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Spring_Lighting", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Spring_Cell_0_4", SceneTerminalKind.Removed);
            await UniTask.Yield();
            Assert.That(
                switchTask.Status,
                Is.EqualTo(UniTaskStatus.Pending),
                "Environment companion の終端前に切替を完了しない");

            fixture.Terminals.Emit("Spring_Environment_0_4", SceneTerminalKind.Removed);
            await CompleteHeldSwitchAsync(fixture, "Season_Summer", "Summer_Lighting", "Summer_Cell_0_4");
            await switchTask;
        });

        [UnityTest]
        public IEnumerator DelayedLightingTerminal_BlocksSameSeasonReentryUntilObservedInstanceEnds()
            => UniTask.ToCoroutine(async () =>
        {
            var fixture = CreateFixture();
            await fixture.Season.EnsureInitialWorldAsync(CancellationToken.None);

            fixture.Controller.UnloadReturnsImmediately = true;
            fixture.Controller.HoldStableUntilReleased = true;
            var toSummer = fixture.Season.RequestSeasonAsync("Summer", 0, 4, CancellationToken.None);
            await UniTask.Yield();
            Assert.That(toSummer.Status, Is.EqualTo(UniTaskStatus.Pending));

            fixture.Terminals.Emit("Season_Spring", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Spring_Cell_0_4", SceneTerminalKind.Removed);
            await UniTask.Yield();
            Assert.That(
                toSummer.Status,
                Is.EqualTo(UniTaskStatus.Pending),
                "Lighting Removed が Season より遅いとき、観測個体の終端まで切替しない");

            fixture.Terminals.Emit("Spring_Lighting", SceneTerminalKind.Removed);
            await CompleteHeldSwitchAsync(fixture, "Season_Summer", "Summer_Lighting", "Summer_Cell_0_4");
            await toSummer;

            var toSpring = fixture.Season.RequestSeasonAsync("Spring", 0, 4, CancellationToken.None);
            await UniTask.Yield();
            fixture.Terminals.Emit("Season_Summer", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Summer_Lighting", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Summer_Cell_0_4", SceneTerminalKind.Removed);
            await CompleteHeldSwitchAsync(fixture, "Season_Spring", "Spring_Lighting", "Spring_Cell_0_4");
            await toSpring;
        });

        // Season 直下の Atmosphere VFX は Lighting と同じ NecessaryAlways の子なので、controller の
        // コードを変えずに観測個体へ登録され、枝の交換はその終端まで待つ（IncrementalAlways にすると観測されない）。
        [UnityTest]
        public IEnumerator SpringAtmosphereVfx_IsObservedInstance_SwitchWaitsForItsTerminal()
            => UniTask.ToCoroutine(async () =>
        {
            var fixture = CreateFixtureCore(includeOriginEnvironment: false, includeSpringAtmosphere: true);
            await fixture.Season.EnsureInitialWorldAsync(CancellationToken.None);
            Assert.That(fixture.Query.IsSceneStable("Spring_AtmosphereVFX"), Is.True,
                "前提: Season の Add が NecessaryAlways の Atmosphere を載せている");

            fixture.Controller.UnloadReturnsImmediately = true;
            fixture.Controller.HoldStableUntilReleased = true;
            var toSummer = fixture.Season.RequestSeasonAsync("Summer", 0, 4, CancellationToken.None);
            await UniTask.Yield();
            fixture.Terminals.Emit("Season_Spring", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Spring_Cell_0_4", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Spring_Lighting", SceneTerminalKind.Removed);
            await UniTask.Yield();
            Assert.That(
                toSummer.Status,
                Is.EqualTo(UniTaskStatus.Pending),
                "Atmosphere の Removed が最後のとき、観測個体の終端まで切替しない");

            fixture.Terminals.Emit("Spring_AtmosphereVFX", SceneTerminalKind.Removed);
            await CompleteHeldSwitchAsync(fixture, "Season_Summer", "Summer_Lighting", "Summer_Cell_0_4");
            await toSummer;
        });

        [UnityTest]
        public IEnumerator StopWorldOperations_RejectsNewIssues_DoesNotDrain()
            => UniTask.ToCoroutine(async () =>
        {
            var fixture = CreateFixture();
            await fixture.Season.EnsureInitialWorldAsync(CancellationToken.None);
            var addCount = fixture.Controller.Added.Count;

            fixture.Season.StopWorldOperations();
            Assert.That(fixture.Season.IsStreamingActive, Is.False);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await fixture.Season.RequestSeasonAsync("Summer", 0, 4, CancellationToken.None));
            Assert.That(fixture.Controller.Added.Count, Is.EqualTo(addCount));
            Assert.That(fixture.Season.Registry.HasIncomplete(), Is.True);
        });

        [UnityTest]
        public IEnumerator EnsureFailure_SetsWorldReadyException()
            => UniTask.ToCoroutine(async () =>
        {
            var fixture = CreateFixture();
            fixture.Controller.FailIdentity = "Season_Spring";

            try
            {
                await fixture.Season.EnsureInitialWorldAsync(CancellationToken.None);
                Assert.Fail("expected exception");
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                await fixture.Season.WaitUntilWorldReady(CancellationToken.None);
                Assert.Fail("expected world ready exception");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.That(fixture.Season.IsWorldReady, Is.False);
        });

        // AC4: Event が Live の間の切替は、Event の終端を待ってから次の Season を Add する。
        [UnityTest]
        public IEnumerator SeasonSwitch_WithLiveMajorEvent_WaitsForEventTerminal()
            => UniTask.ToCoroutine(async () =>
        {
            var fixture = CreateFixtureCore(includeOriginEnvironment: false, includeMajorEvent: true);
            await fixture.Season.EnsureInitialWorldAsync(CancellationToken.None);
            Assert.That(fixture.Season.RequestMajorEvent(MajorEventId), Is.EqualTo(MajorEventRequestResult.Accepted));
            Assert.That(fixture.Query.IsSceneStable(MajorEventId), Is.True);

            fixture.Controller.UnloadReturnsImmediately = true;
            fixture.Controller.HoldStableUntilReleased = true;
            var toSummer = fixture.Season.RequestSeasonAsync("Summer", 0, 4, CancellationToken.None);
            await UniTask.Yield();
            Assert.That(fixture.Season.RequestMajorEvent(MajorEventId), Is.EqualTo(MajorEventRequestResult.Rejected));
            fixture.Terminals.Emit("Season_Spring", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Spring_Lighting", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Spring_Cell_0_4", SceneTerminalKind.Removed);
            await UniTask.Yield();
            Assert.That(fixture.Controller.Added, Does.Not.Contain("Season_Summer"), "Event の終端の前に次の Season を Add しない");

            fixture.Terminals.Emit(MajorEventId, SceneTerminalKind.Removed);
            await CompleteHeldSwitchAsync(fixture, "Season_Summer", "Summer_Lighting", "Summer_Cell_0_4");
            await toSummer;
            Assert.That(fixture.Season.Registry.HasIncompleteAdd(MajorEventId), Is.False);
        });

        // AC5 (i)〜(iii): Event の Add の await 中に切替を始める。(i) キャンセル窓の CancelCleanedUp（Add はその後に戻る）、
        // (ii) 窓の外のロードの後の Removed（Add が戻ってから終端）、(iii) Initializing の Removed（終端の後に Add が戻る）。
        // どれも次の Season の Add は Event の終端の後で、Event の op は完了し、Event の個体は残らない。
        [UnityTest]
        public IEnumerator SeasonSwitch_DuringMajorEventAdd_WaitsForEventTerminal(
            [Values(EventAddPhase.CancelWindow, EventAddPhase.OutsideWindow, EventAddPhase.Initializing)] EventAddPhase phase)
            => UniTask.ToCoroutine(async () =>
        {
            var fixture = CreateFixtureCore(includeOriginEnvironment: false, includeMajorEvent: true);
            await fixture.Season.EnsureInitialWorldAsync(CancellationToken.None);
            fixture.Controller.HoldStableUntilReleased = true;
            Assert.That(fixture.Season.RequestMajorEvent(MajorEventId), Is.EqualTo(MajorEventRequestResult.Accepted));
            Assert.That(fixture.Season.Registry.HasIncompleteAdd(MajorEventId), Is.True);

            fixture.Controller.UnloadReturnsImmediately = true;
            var toSummer = fixture.Season.RequestSeasonAsync("Summer", 0, 4, CancellationToken.None);
            await UniTask.Yield();
            fixture.Terminals.Emit("Season_Spring", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Spring_Lighting", SceneTerminalKind.Removed);
            fixture.Terminals.Emit("Spring_Cell_0_4", SceneTerminalKind.Removed);
            await UniTask.Yield();
            Assert.That(fixture.Controller.Added, Does.Not.Contain("Season_Summer"));

            switch (phase)
            {
                case EventAddPhase.CancelWindow:
                    fixture.Terminals.Emit(MajorEventId, SceneTerminalKind.CancelCleanedUp);
                    fixture.Controller.ReleaseHeldAdds();
                    break;
                case EventAddPhase.OutsideWindow:
                    fixture.Controller.ReleaseHeldAdds();
                    await UniTask.Yield();
                    Assert.That(fixture.Controller.Added, Does.Not.Contain("Season_Summer"), "Add の戻りだけでは切替しない");
                    fixture.Terminals.Emit(MajorEventId, SceneTerminalKind.Removed);
                    break;
                case EventAddPhase.Initializing:
                    fixture.Terminals.Emit(MajorEventId, SceneTerminalKind.Removed);
                    fixture.Controller.ReleaseHeldAdds();
                    break;
            }

            await CompleteHeldSwitchAsync(fixture, "Season_Summer", "Summer_Lighting", "Summer_Cell_0_4");
            await toSummer;
            Assert.That(fixture.Season.Registry.HasIncompleteAdd(MajorEventId), Is.False);
            Assert.That(fixture.Query.GetLoadedScene(MajorEventId), Is.Null);
        });

        // AC5 (iv): await より前に個体を捕捉できない発行。元の Add が例外で終わるまで 2 本目の Add を出さず、
        // 例外は Error 1 回で閉じ、未観測の例外を残さない。終わった後の要求は Add 1。
        [UnityTest]
        public IEnumerator MajorEventIssueWithoutCapture_WaitsForOriginalAddToFail()
            => UniTask.ToCoroutine(async () =>
        {
            var logger = new FakeVfxLogger();
            var fixture = CreateFixtureCore(includeOriginEnvironment: false, includeMajorEvent: true, logger: logger);
            await fixture.Season.EnsureInitialWorldAsync(CancellationToken.None);
            fixture.Controller.HoldStableUntilReleased = true;
            fixture.Controller.DeferQueryRegistration = true;
            fixture.Controller.FailAfterHoldIdentity = MajorEventId;

            Assert.That(fixture.Season.RequestMajorEvent(MajorEventId), Is.EqualTo(MajorEventRequestResult.Accepted));
            Assert.That(fixture.Season.RequestMajorEvent(MajorEventId), Is.EqualTo(MajorEventRequestResult.Rejected));
            Assert.That(CountAdds(fixture, MajorEventId), Is.EqualTo(1));
            Assert.That(logger.ErrorCount, Is.Zero);

            fixture.Controller.ReleaseHeldAdds();
            await UniTask.Yield();
            Assert.That(logger.ErrorCount, Is.EqualTo(1));
            Assert.That(fixture.Season.Registry.HasIncompleteAdd(MajorEventId), Is.False);

            fixture.Controller.HoldStableUntilReleased = false;
            fixture.Controller.DeferQueryRegistration = false;
            fixture.Controller.FailAfterHoldIdentity = null;
            Assert.That(fixture.Season.RequestMajorEvent(MajorEventId), Is.EqualTo(MajorEventRequestResult.Accepted));
            Assert.That(CountAdds(fixture, MajorEventId), Is.EqualTo(2));
        });

        public enum EventAddPhase
        {
            CancelWindow,
            OutsideWindow,
            Initializing,
        }

        private const string MajorEventId = "Spring_Event_MultiCellProof";

        private static int CountAdds(Fixture fixture, string identity)
            => fixture.Controller.Added.FindAll(added => added == identity).Count;

        private static async UniTask CompleteHeldSwitchAsync(
            Fixture fixture,
            string seasonIdentity,
            string lightingIdentity,
            string originIdentity)
        {
            await UniTask.WaitUntil(() => fixture.Query.IsSceneLoaded(seasonIdentity));
            fixture.Query.Stable.Add(seasonIdentity);
            fixture.Query.Stable.Add(lightingIdentity);
            fixture.Controller.ReleaseHeldAdds();
            await UniTask.WaitUntil(() => fixture.Query.IsSceneLoaded(originIdentity));
            fixture.Query.Stable.Add(originIdentity);
            fixture.Controller.ReleaseHeldAdds();
        }

        private Fixture CreateFixture()
        {
            return CreateFixtureCore(includeOriginEnvironment: false);
        }

        private Fixture CreateBranchConvergenceFixture()
        {
            return CreateFixtureCore(includeOriginEnvironment: true);
        }

        private Fixture CreateFixtureCore(
            bool includeOriginEnvironment,
            bool includeSpringAtmosphere = false,
            bool includeMajorEvent = false,
            Microsoft.Extensions.Logging.ILogger? logger = null)
        {
            var originVolume = new Bounds(new Vector3(125f, 48f, 1125f), new Vector3(250f, 96f, 250f));
            var spring = Track(SceneTestHelper.CreateSceneResource("Season_Spring"));
            var springLight = Track(SceneTestHelper.CreateSceneResource(
                "Spring_Lighting",
                loadType: LoadType.NecessaryAlways,
                parent: spring));
            SceneTestHelper.AddChild(spring, springLight);
            SceneResource? springAtmosphere = null;
            if (includeSpringAtmosphere)
            {
                // 実 asset と同じ形: Season 直下の NecessaryAlways で、streaming でない。
                springAtmosphere = Track(SceneTestHelper.CreateSceneResource(
                    "Spring_AtmosphereVFX",
                    loadType: LoadType.NecessaryAlways,
                    parent: spring));
                SceneTestHelper.AddChild(spring, springAtmosphere);
            }

            var springOrigin = Track(SceneTestHelper.CreateSceneResource(
                "Spring_Cell_0_4",
                parent: spring,
                streamByDistance: true,
                volume: originVolume));
            SceneTestHelper.AddChild(spring, springOrigin);
            SceneResource? springEnvironment = null;
            if (includeOriginEnvironment)
            {
                springEnvironment = Track(SceneTestHelper.CreateSceneResource(
                    "Spring_Environment_0_4",
                    parent: springOrigin));
                SceneTestHelper.AddChild(springOrigin, springEnvironment);
            }

            SceneResource? springEvent = null;
            if (includeMajorEvent)
            {
                springEvent = Track(SceneTestHelper.CreateSceneResource(MajorEventId, parent: spring));
                SceneTestHelper.AddChild(spring, springEvent);
            }

            var summer = Track(SceneTestHelper.CreateSceneResource("Season_Summer"));
            var summerLight = Track(SceneTestHelper.CreateSceneResource(
                "Summer_Lighting",
                loadType: LoadType.NecessaryAlways,
                parent: summer));
            SceneTestHelper.AddChild(summer, summerLight);
            var summerOrigin = Track(SceneTestHelper.CreateSceneResource(
                "Summer_Cell_0_4",
                parent: summer,
                streamByDistance: true,
                volume: originVolume));
            SceneTestHelper.AddChild(summer, summerOrigin);

            var resources = new Dictionary<string, SceneResource>(StringComparer.Ordinal)
            {
                [spring.Identity] = spring,
                [springLight.Identity] = springLight,
                [springOrigin.Identity] = springOrigin,
                [summer.Identity] = summer,
                [summerLight.Identity] = summerLight,
                [summerOrigin.Identity] = summerOrigin,
            };
            if (springEnvironment != null)
            {
                resources[springEnvironment.Identity] = springEnvironment;
            }

            if (springAtmosphere != null)
            {
                resources[springAtmosphere.Identity] = springAtmosphere;
            }

            if (springEvent != null)
            {
                resources[springEvent.Identity] = springEvent;
            }

            var query = new FakeQuery();
            var controller = new FakeBranchController(query, resources);
            var volumes = new FakeVolumeQuery();
            volumes.Volumes[springOrigin.Identity] = springOrigin.Volume;
            volumes.Volumes[summerOrigin.Identity] = summerOrigin.Volume;
            var terminals = new FakeTerminalEvents();
            ISceneStreamingBackend backend;
            QueryBackedStreamingBackend? queryBackend = null;
            DriverHolder? driverHolder = null;
            Func<ISceneStreamingBackend, StreamingCandidateSet, SessionWorldStreamingDriver>? driverFactory = null;
            if (includeOriginEnvironment)
            {
                queryBackend = new QueryBackedStreamingBackend(query, controller);
                backend = queryBackend;
                var holder = new DriverHolder();
                driverHolder = holder;
                driverFactory = (issuedBackend, candidates) =>
                {
                    var driver = new SessionWorldStreamingDriver(
                        issuedBackend,
                        candidates,
                        () => null,
                        NullLogger.Instance);
                    holder.Driver = driver;
                    return driver;
                };
            }
            else
            {
                backend = new FakeStreamingBackend();
            }

            var season = new SessionSeasonController(
                controller,
                query,
                volumes,
                backend,
                terminals,
                CellCompanionSet.Full,
                () => null,
                logger ?? NullLogger.Instance,
                streamingDriverFactory: driverFactory);
            _seasons.Add(season);

            return new Fixture(season, query, controller, terminals, driverHolder, queryBackend);
        }

        private SceneResource Track(SceneResource resource)
        {
            _assets.Add(resource);
            return resource;
        }

        private sealed class Fixture
        {
            internal Fixture(
                SessionSeasonController season,
                FakeQuery query,
                FakeBranchController controller,
                FakeTerminalEvents terminals,
                DriverHolder? streamingDriver = null,
                QueryBackedStreamingBackend? streamingBackend = null)
            {
                Season = season;
                Query = query;
                Controller = controller;
                Terminals = terminals;
                StreamingDriverHolder = streamingDriver;
                StreamingBackend = streamingBackend;
            }

            internal SessionSeasonController Season { get; }
            internal FakeQuery Query { get; }
            internal FakeBranchController Controller { get; }
            internal FakeTerminalEvents Terminals { get; }
            internal DriverHolder? StreamingDriverHolder { get; }
            internal SessionWorldStreamingDriver? StreamingDriver => StreamingDriverHolder?.Driver;
            internal QueryBackedStreamingBackend? StreamingBackend { get; }
        }

        private sealed class DriverHolder
        {
            internal SessionWorldStreamingDriver? Driver;
        }

        private sealed class QueryBackedStreamingBackend : ISceneStreamingBackend
        {
            private readonly FakeQuery _query;
            private readonly FakeBranchController _controller;

            internal QueryBackedStreamingBackend(FakeQuery query, FakeBranchController controller)
            {
                _query = query;
                _controller = controller;
            }

            internal List<string> Removed { get; } = new();

            public async UniTask RequestAdd(string cellId, int priority)
            {
                await _controller.AddScene(
                    cellId,
                    afterOnLoadedTask: null,
                    CancellationToken.None);
            }

            public async UniTask RequestRemove(string cellId)
            {
                Removed.Add(cellId);
                await _controller.UnloadScene(cellId);
            }

            public bool IsLoaded(string cellId) => _query.IsSceneStable(cellId);
        }

        private sealed class FakeQuery : ISceneQuery
        {
            internal Dictionary<string, SceneBase> Scenes { get; } = new(StringComparer.Ordinal);
            internal HashSet<string> Loaded { get; } = new(StringComparer.Ordinal);
            internal HashSet<string> Stable { get; } = new(StringComparer.Ordinal);

            public SceneBase? GetLoadedScene(string identity)
                => Scenes.TryGetValue(identity, out var scene) ? scene : null;

            public bool IsSceneLoaded(string identity) => Loaded.Contains(identity);
            public bool IsSceneStable(string identity) => Stable.Contains(identity);
        }

        private sealed class FakeVolumeQuery : ISceneVolumeQuery
        {
            internal Dictionary<string, Bounds> Volumes { get; } = new(StringComparer.Ordinal);

            public bool TryGetSceneVolume(string identity, out Bounds volume)
                => Volumes.TryGetValue(identity, out volume);
        }

        private sealed class FakeBranchController : ISceneController
        {
            private readonly FakeQuery _query;
            private readonly Dictionary<string, SceneResource> _resources;
            private readonly Queue<UniTaskCompletionSource> _held = new();

            internal FakeBranchController(FakeQuery query, Dictionary<string, SceneResource> resources)
            {
                _query = query;
                _resources = resources;
            }

            internal bool HoldStableUntilReleased { get; set; }
            internal bool UnloadReturnsImmediately { get; set; }
            internal string? FailIdentity { get; set; }
            // 個体を Add の最初の await の後に辞書へ入れる（await より前に捕捉できない発行の再現）。
            internal bool DeferQueryRegistration { get; set; }
            internal string? FailAfterHoldIdentity { get; set; }
            internal List<string> Added { get; } = new();
            internal List<string> Unloaded { get; } = new();

            internal void ReleaseHeldAdds()
            {
                if (_held.Count == 0)
                {
                    return;
                }

                _held.Dequeue().TrySetResult();
            }

            public async UniTask AddScene(
                string sceneIdentify,
                Func<UniTask>? afterOnLoadedTask,
                CancellationToken ct,
                SceneContext? context = null,
                IProgress<SceneLoadProgress>? progress = null,
                LoadingDisplayType loadingDisplay = LoadingDisplayType.None,
                IReadOnlyDictionary<string, string>? telemetryTags = null,
                int priority = 100,
                TelemetryLevel telemetryLevel = TelemetryLevel.Summary)
            {
                Added.Add(sceneIdentify);
                if (string.Equals(FailIdentity, sceneIdentify, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"add failed: {sceneIdentify}");
                }

                if (!_resources.TryGetValue(sceneIdentify, out var resource))
                {
                    throw new InvalidOperationException($"unknown {sceneIdentify}");
                }

                var scene = resource.StreamByDistance
                    ? (SceneBase)new CellScene(resource, _query, this)
                    : new TestSceneBase(resource, _query, this);
                var deferRegistration = DeferQueryRegistration;
                if (!deferRegistration)
                {
                    _query.Scenes[sceneIdentify] = scene;
                }

                _query.Loaded.Add(sceneIdentify);

                foreach (var child in resource.Children)
                {
                    if (child.LoadType != LoadType.NecessaryAlways)
                    {
                        continue;
                    }

                    var childScene = new TestSceneBase(child, _query, this);
                    _query.Scenes[child.Identity] = childScene;
                    _query.Loaded.Add(child.Identity);
                    if (!HoldStableUntilReleased)
                    {
                        _query.Stable.Add(child.Identity);
                    }
                }

                if (HoldStableUntilReleased)
                {
                    var gate = new UniTaskCompletionSource();
                    _held.Enqueue(gate);
                    using (ct.Register(() => gate.TrySetCanceled(ct)))
                    {
                        await gate.Task;
                    }

                    if (string.Equals(FailAfterHoldIdentity, sceneIdentify, StringComparison.Ordinal))
                    {
                        _query.Loaded.Remove(sceneIdentify);
                        throw new InvalidOperationException($"add failed after hold: {sceneIdentify}");
                    }

                    if (deferRegistration)
                    {
                        _query.Scenes[sceneIdentify] = scene;
                    }

                    return;
                }

                _query.Stable.Add(sceneIdentify);
            }

            public UniTask UnloadScene(
                string sceneIdentify,
                LoadingDisplayType loadingDisplay = LoadingDisplayType.None,
                IReadOnlyDictionary<string, string>? telemetryTags = null,
                TelemetryLevel telemetryLevel = TelemetryLevel.Summary)
            {
                Unloaded.Add(sceneIdentify);
                // 親 Unload は子孫も query から外す（IsSceneLoaded false）。終端イベントは出さない。
                RemoveLoadedTree(sceneIdentify);
                return UniTask.CompletedTask;
            }

            private void RemoveLoadedTree(string identity)
            {
                if (_resources.TryGetValue(identity, out var resource))
                {
                    var children = resource.Children;
                    for (var i = 0; i < children.Count; i++)
                    {
                        var child = children[i];
                        if (child != null)
                        {
                            RemoveLoadedTree(child.Identity);
                        }
                    }
                }

                _query.Loaded.Remove(identity);
                _query.Stable.Remove(identity);
                _query.Scenes.Remove(identity);
            }

            public UniTask SwitchScene(
                string? fromSceneIdentify,
                string toSceneIdentify,
                CancellationToken ct,
                SceneContext? context = null,
                LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen,
                IReadOnlyDictionary<string, string>? telemetryTags = null)
                => UniTask.CompletedTask;

            public UniTask GoBack(
                CancellationToken ct,
                SceneContext? context = null,
                LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen,
                IReadOnlyDictionary<string, string>? telemetryTags = null)
                => UniTask.CompletedTask;

            public void ClearHistory()
            {
            }
        }
    }
}
