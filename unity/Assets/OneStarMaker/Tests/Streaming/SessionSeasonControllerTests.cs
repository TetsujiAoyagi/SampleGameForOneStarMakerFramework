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
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using SampleGame.InGame.Streaming;
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

        private Fixture CreateFixture()
        {
            var spring = Track(SceneTestHelper.CreateSceneResource("Season_Spring"));
            var springLight = Track(SceneTestHelper.CreateSceneResource(
                "Spring_Lighting",
                loadType: LoadType.NecessaryAlways,
                parent: spring));
            SceneTestHelper.AddChild(spring, springLight);
            var springOrigin = Track(SceneTestHelper.CreateSceneResource(
                "Spring_Cell_0_4",
                parent: spring,
                streamByDistance: true,
                volume: new Bounds(new Vector3(125f, 48f, 1125f), new Vector3(250f, 96f, 250f))));
            SceneTestHelper.AddChild(spring, springOrigin);

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
                volume: new Bounds(new Vector3(125f, 48f, 1125f), new Vector3(250f, 96f, 250f))));
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

            var query = new FakeQuery();
            var controller = new FakeBranchController(query, resources);
            var volumes = new FakeVolumeQuery();
            volumes.Volumes[springOrigin.Identity] = springOrigin.Volume;
            volumes.Volumes[summerOrigin.Identity] = summerOrigin.Volume;
            var terminals = new FakeTerminalEvents();
            var backend = new FakeStreamingBackend();
            var season = new SessionSeasonController(
                controller,
                query,
                volumes,
                backend,
                terminals,
                CellCompanionSet.Full,
                () => null,
                NullLogger.Instance);
            _seasons.Add(season);

            return new Fixture(season, query, controller, terminals);
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
                FakeTerminalEvents terminals)
            {
                Season = season;
                Query = query;
                Controller = controller;
                Terminals = terminals;
            }

            internal SessionSeasonController Season { get; }
            internal FakeQuery Query { get; }
            internal FakeBranchController Controller { get; }
            internal FakeTerminalEvents Terminals { get; }
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

                var scene = new TestSceneBase(resource, _query, this);
                _query.Scenes[sceneIdentify] = scene;
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
