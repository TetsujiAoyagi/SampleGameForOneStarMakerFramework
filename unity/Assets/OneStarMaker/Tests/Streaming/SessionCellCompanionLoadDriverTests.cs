#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.SceneSystem.Helpers;
using SampleGame.InGame.Streaming;
using SampleGame.InGame.World;
using UnityEngine;

namespace OneStarMaker.Tests.Streaming
{
    [TestFixture]
    public sealed class SessionCellCompanionLoadDriverTests
    {
        private readonly List<ScriptableObject> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public async UniTask Reconcile_ParentNotStable_AddsNothing()
        {
            var fixture = CreateFixture(CellCompanionSet.Full, parentStable: false, "Spring_Environment_0_0");

            await fixture.Driver.ReconcileOnceAsync(CancellationToken.None);

            Assert.That(fixture.Controller.AddCount, Is.Zero);
        }

        [Test]
        public async UniTask Reconcile_Planner_LoadsEventsOnlyFromChildren()
        {
            var fixture = CreateFixture(
                CellCompanionSet.Planner,
                parentStable: true,
                "Spring_Environment_0_0",
                "Spring_Events_0_0");

            await fixture.Driver.ReconcileOnceAsync(CancellationToken.None);

            Assert.That(fixture.Controller.Added, Is.EqualTo(new[] { "Spring_Events_0_0" }));
        }

        [TestCase(CellCompanionSet.Full, "Environment,Lighting,VFX,Events")]
        [TestCase(CellCompanionSet.Planner, "Events")]
        [TestCase(CellCompanionSet.Lighting, "Environment,Lighting")]
        [TestCase(CellCompanionSet.Vfx, "Environment,Lighting,VFX")]
        public async UniTask Reconcile_EachProfile_LoadsOnlyItsIncludedRoles(
            CellCompanionSet set,
            string expectedRoles)
        {
            var fixture = CreateFixture(
                set,
                parentStable: true,
                "Spring_Environment_0_0",
                "Spring_Lighting_0_0",
                "Spring_VFX_0_0",
                "Spring_Events_0_0");

            await fixture.Driver.ReconcileOnceAsync(CancellationToken.None);

            var expected = expectedRoles.Split(',');
            Assert.That(fixture.Controller.Added,
                Is.EquivalentTo(Array.ConvertAll(expected, role => $"Spring_{role}_0_0")));
        }

        [Test]
        public async UniTask Reconcile_DuplicateSameRole_AddsNoneForRole()
        {
            var fixture = CreateFixture(
                CellCompanionSet.Full,
                parentStable: true,
                "Spring_Environment_0_0",
                "Qualified_Environment_west_east");

            await fixture.Driver.ReconcileOnceAsync(CancellationToken.None);

            Assert.That(fixture.Controller.AddCount, Is.Zero);
        }

        [Test]
        public async UniTask Reconcile_InFlightDuplicate_OnlyOneAdd()
        {
            var fixture = CreateFixture(CellCompanionSet.Full, true, "Spring_Environment_0_0");
            var gate = new UniTaskCompletionSource();
            fixture.Controller.AddHandler = (id, _) => gate.Task;

            var first = fixture.Driver.ReconcileOnceAsync(CancellationToken.None);
            await fixture.Driver.ReconcileOnceAsync(CancellationToken.None);

            Assert.That(fixture.Controller.AddCount, Is.EqualTo(1));
            fixture.Query.Loaded.Add("Spring_Environment_0_0");
            gate.TrySetResult();
            await first;
        }

        [Test]
        public async UniTask Reconcile_ParentLeavesResidentsDuringAdd_UnloadsChild()
        {
            var fixture = CreateFixture(CellCompanionSet.Full, true, "Spring_Environment_0_0");
            fixture.Controller.AddHandler = (id, _) =>
            {
                fixture.Query.Loaded.Add(id);
                fixture.Residents.Clear();
                return UniTask.CompletedTask;
            };

            await fixture.Driver.ReconcileOnceAsync(CancellationToken.None);

            Assert.That(fixture.Controller.Unloaded, Is.EqualTo(new[] { "Spring_Environment_0_0" }));
        }

        [Test]
        public async UniTask Reconcile_ParentReloadedWithSameIdentity_UnloadsChild()
        {
            var fixture = CreateFixture(CellCompanionSet.Full, true, "Spring_Environment_0_0");
            fixture.Controller.AddHandler = (id, _) =>
            {
                fixture.Query.Loaded.Add(id);
                fixture.Query.Scenes[fixture.ParentIdentity] = new CellScene(
                    fixture.ParentResource,
                    fixture.Query,
                    fixture.Controller);
                return UniTask.CompletedTask;
            };

            await fixture.Driver.ReconcileOnceAsync(CancellationToken.None);

            Assert.That(fixture.Controller.UnloadCount, Is.EqualTo(1));
        }

        [Test]
        public async UniTask Reconcile_CancelledAfterAdd_UnloadsChild()
        {
            var fixture = CreateFixture(CellCompanionSet.Full, true, "Spring_Environment_0_0");
            using var cts = new CancellationTokenSource();
            fixture.Controller.AddHandler = (id, _) =>
            {
                fixture.Query.Loaded.Add(id);
                cts.Cancel();
                return UniTask.CompletedTask;
            };

            await fixture.Driver.ReconcileOnceAsync(cts.Token);

            Assert.That(fixture.Controller.UnloadCount, Is.EqualTo(1));
        }

        private Fixture CreateFixture(CellCompanionSet set, bool parentStable, params string[] childIdentities)
        {
            const string parentIdentity = "Spring_Cell_0_0";
            var parentResource = SceneTestHelper.CreateSceneResource(parentIdentity, streamByDistance: true);
            _assets.Add(parentResource);
            foreach (var identity in childIdentities)
            {
                var child = SceneTestHelper.CreateSceneResource(identity, parent: parentResource);
                SceneTestHelper.AddChild(parentResource, child);
                _assets.Add(child);
            }

            var query = new FakeQuery();
            var controller = new FakeController(query);
            var parentScene = new CellScene(parentResource, query, controller);
            query.Scenes.Add(parentIdentity, parentScene);
            query.Loaded.Add(parentIdentity);
            if (parentStable) query.Stable.Add(parentIdentity);
            var residents = new List<string> { parentIdentity };
            var driver = new SessionCellCompanionLoadDriver(controller, query, () => residents, set, NullLogger.Instance);
            return new Fixture(parentIdentity, parentResource, query, controller, residents, driver);
        }

        private sealed class Fixture
        {
            internal Fixture(string parentIdentity, SceneResource resource, FakeQuery query, FakeController controller, List<string> residents, SessionCellCompanionLoadDriver driver)
            { ParentIdentity = parentIdentity; ParentResource = resource; Query = query; Controller = controller; Residents = residents; Driver = driver; }
            internal string ParentIdentity { get; }
            internal SceneResource ParentResource { get; }
            internal FakeQuery Query { get; }
            internal FakeController Controller { get; }
            internal List<string> Residents { get; }
            internal SessionCellCompanionLoadDriver Driver { get; }
        }

        private sealed class FakeQuery : ISceneQuery
        {
            internal Dictionary<string, SceneBase> Scenes { get; } = new(StringComparer.Ordinal);
            internal HashSet<string> Loaded { get; } = new(StringComparer.Ordinal);
            internal HashSet<string> Stable { get; } = new(StringComparer.Ordinal);
            public SceneBase? GetLoadedScene(string identity) => Scenes.TryGetValue(identity, out var scene) ? scene : null;
            public bool IsSceneLoaded(string identity) => Loaded.Contains(identity);
            public bool IsSceneStable(string identity) => Stable.Contains(identity);
        }

        private sealed class FakeController : ISceneController
        {
            private readonly FakeQuery _query;
            internal FakeController(FakeQuery query) { _query = query; }
            internal Func<string, CancellationToken, UniTask>? AddHandler { get; set; }
            internal List<string> Added { get; } = new();
            internal List<string> Unloaded { get; } = new();
            internal int AddCount => Added.Count;
            internal int UnloadCount => Unloaded.Count;
            public async UniTask AddScene(string id, Func<UniTask>? after, CancellationToken ct, SceneContext? context = null, IProgress<SceneLoadProgress>? progress = null, LoadingDisplayType display = LoadingDisplayType.None, IReadOnlyDictionary<string, string>? tags = null, int priority = 100, TelemetryLevel level = TelemetryLevel.Summary)
            { Added.Add(id); if (AddHandler != null) await AddHandler(id, ct); else _query.Loaded.Add(id); }
            public UniTask UnloadScene(string id, LoadingDisplayType display = LoadingDisplayType.None, IReadOnlyDictionary<string, string>? tags = null, TelemetryLevel level = TelemetryLevel.Summary)
            { Unloaded.Add(id); _query.Loaded.Remove(id); return UniTask.CompletedTask; }
            public UniTask SwitchScene(string? from, string to, CancellationToken ct, SceneContext? context = null, LoadingDisplayType display = LoadingDisplayType.BlackScreen, IReadOnlyDictionary<string, string>? tags = null) => UniTask.CompletedTask;
            public UniTask GoBack(CancellationToken ct, SceneContext? context = null, LoadingDisplayType display = LoadingDisplayType.BlackScreen, IReadOnlyDictionary<string, string>? tags = null) => UniTask.CompletedTask;
            public void ClearHistory() { }
        }
    }
}
