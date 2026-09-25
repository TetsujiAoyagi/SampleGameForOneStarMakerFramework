#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using SampleGame.InGame.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.SceneSystem
{
    [TestFixture]
    public class CellSceneTests : SceneDirectorTestBase
    {
        private readonly List<GameObject> _createdGameObjects = new();

        [TearDown]
        public void CellTearDown()
        {
            foreach (var go in _createdGameObjects)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _createdGameObjects.Clear();
        }

        [Test]
        public void Constructor_QualifiedOpaqueIdentityAndStreamingFlag_Succeeds()
        {
            var volume = new Bounds(new Vector3(7f, 8f, 9f), new Vector3(10f, 11f, 12f));
            var resource = SceneTestHelper.CreateSceneResource(
                "Qualifier_With_Underscore_Cell_not_an_int_east",
                streamByDistance: true,
                volume: volume);
            CreatedSOs.Add(resource);

            var scene = new CellScene(resource, new NullSceneQuery(), new NullSceneController());

            Assert.That(scene.Bounds, Is.EqualTo(volume));
        }

        [Test]
        public void Constructor_NonStreamingResource_Throws()
        {
            var resource = SceneTestHelper.CreateSceneResource("Cell_3_5", streamByDistance: false);
            CreatedSOs.Add(resource);

            Assert.Throws<ArgumentException>(
                () => new CellScene(resource, new NullSceneQuery(), new NullSceneController()));
        }

        [Test]
        public void Companion_QualifiedUnknownRoleWithStreamingParent_SucceedsWithoutUIView()
        {
            var parent = SceneTestHelper.CreateSceneResource("opaque-parent", streamByDistance: true);
            var child = SceneTestHelper.CreateSceneResource("Qualifier_Unknown_west_east", parent: parent);
            CreatedSOs.Add(parent);
            CreatedSOs.Add(child);

            var scene = new CellCompanionScene(
                child,
                new NullSceneQuery(),
                new NullSceneController(),
                NullLoggerFactory.Instance);

            Assert.That(scene.UIView, Is.Null);
        }

        [Test]
        public void Companion_OrphanOrNonStreamingParent_Throws()
        {
            var orphan = SceneTestHelper.CreateSceneResource("Spring_Events_0_0");
            var nonStreamingParent = SceneTestHelper.CreateSceneResource("parent", streamByDistance: false);
            var child = SceneTestHelper.CreateSceneResource("Spring_Events_0_1", parent: nonStreamingParent);
            CreatedSOs.Add(orphan);
            CreatedSOs.Add(nonStreamingParent);
            CreatedSOs.Add(child);

            Assert.Throws<ArgumentException>(() => new CellCompanionScene(
                orphan, new NullSceneQuery(), new NullSceneController(), NullLoggerFactory.Instance));
            Assert.Throws<ArgumentException>(() => new CellCompanionScene(
                child, new NullSceneQuery(), new NullSceneController(), NullLoggerFactory.Instance));
        }

        [Test]
        public async Task DemoCell_LoadedWithAuthoredRoot_Succeeds()
        {
            var resource = SceneTestHelper.CreateSceneResource("opaque-cell", streamByDistance: true);
            var root = new GameObject(DemoCellScene.AuthoredRootName);
            CreatedSOs.Add(resource);
            _createdGameObjects.Add(root);
            var scene = new DemoCellScene(
                resource, new NullSceneQuery(), new NullSceneController(), NullLoggerFactory.Instance);
            scene.Initialize(new[] { root });

            await scene.ExecuteLoaded(CancellationToken.None);
        }

        [Test]
        public void DemoCell_LoadedWithoutAuthoredRoot_Throws()
        {
            var resource = SceneTestHelper.CreateSceneResource("opaque-cell", streamByDistance: true);
            var root = new GameObject("UnexpectedRoot");
            CreatedSOs.Add(resource);
            _createdGameObjects.Add(root);
            var scene = new DemoCellScene(
                resource, new NullSceneQuery(), new NullSceneController(), NullLoggerFactory.Instance);
            scene.Initialize(new[] { root });

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await scene.ExecuteLoaded(CancellationToken.None));
        }

        [UnityTest]
        public IEnumerator CellScene_Load_DoesNotRegisterUIView() => UniTask.ToCoroutine(async () =>
        {
            var (director, factory) = SetupDirector("opaque-cell", isCell: true);
            director.RootObjectsFactory = _ => new[] { CreateRootWithUIView("CellRoot") };

            await director.AddScene("opaque-cell", null, CancellationToken.None);

            Assert.That(factory.Created["opaque-cell"], Is.TypeOf<CellScene>());
            Assert.That(factory.Created["opaque-cell"].UIView, Is.Null);
            Assert.That(UICommon.GetUIView("opaque-cell"), Is.Null);
        });

        [UnityTest]
        public IEnumerator PlainScene_Load_RegistersUIView_HarnessSanity() => UniTask.ToCoroutine(async () =>
        {
            var (director, factory) = SetupDirector("Plain", isCell: false);
            director.RootObjectsFactory = _ => new[] { CreateRootWithUIView("PlainRoot") };

            await director.AddScene("Plain", null, CancellationToken.None);

            Assert.That(factory.Created["Plain"].UIView, Is.Not.Null);
            Assert.That(UICommon.GetUIView("Plain"), Is.Not.Null);
        });

        private (RootObjectsSceneDirector Director, StructuralSceneFactory Factory) SetupDirector(string identity, bool isCell)
        {
            var resource = SceneTestHelper.CreateSceneResource(identity, streamByDistance: isCell);
            CreatedSOs.Add(resource);
            Map = SceneTestHelper.CreateSceneResourceMap(resource);
            CreatedSOs.Add(Map);
            var factory = new StructuralSceneFactory();
            var director = new RootObjectsSceneDirector(factory, UICommon, Map, AssetManagement);
            Director = director;
            return (director, factory);
        }

        private GameObject CreateRootWithUIView(string name)
        {
            var go = new GameObject(name);
            go.AddComponent<TestUIView>();
            _createdGameObjects.Add(go);
            return go;
        }

        private sealed class TestUIView : UIView { }

        private sealed class StructuralSceneFactory : ISceneFactory
        {
            public Dictionary<string, SceneBase> Created { get; } = new();

            public SceneBase? CreateSceneClass(SceneResource resource, ISceneQuery query, ISceneController controller)
            {
                SceneBase scene = resource.StreamByDistance
                    ? new CellScene(resource, query, controller)
                    : new SceneBase(resource, query, controller);
                Created.Add(resource.Identity, scene);
                return scene;
            }
        }

        private sealed class RootObjectsSceneDirector : TestableSceneDirector
        {
            public Func<string, GameObject[]>? RootObjectsFactory { get; set; }

            public RootObjectsSceneDirector(ISceneFactory factory, UICommon ui, SceneResourceMap map, IAssetManagement assets)
                : base(factory, ui, map, assets) { }

            protected override async UniTask<(bool BackendSceneLoaded, GameObject[] RootObjects)> PerformUnitySceneLoad(
                string identity,
                SceneResource resource,
                int priority)
            {
                await base.PerformUnitySceneLoad(identity, resource, priority);
                return (false, RootObjectsFactory?.Invoke(identity) ?? Array.Empty<GameObject>());
            }
        }

        private sealed class NullSceneQuery : ISceneQuery
        {
            public SceneBase? GetLoadedScene(string identity) => null;
            public bool IsSceneLoaded(string identity) => false;
            public bool IsSceneStable(string identity) => false;
        }

        private sealed class NullSceneController : ISceneController
        {
            public UniTask AddScene(string id, Func<UniTask>? after, CancellationToken ct, SceneContext? context = null, IProgress<SceneLoadProgress>? progress = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, IReadOnlyDictionary<string, string>? tags = null, int priority = 100, TelemetryLevel telemetryLevel = TelemetryLevel.Summary) => UniTask.CompletedTask;
            public UniTask UnloadScene(string id, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, IReadOnlyDictionary<string, string>? tags = null, TelemetryLevel telemetryLevel = TelemetryLevel.Summary) => UniTask.CompletedTask;
            public UniTask SwitchScene(string? from, string to, CancellationToken ct, SceneContext? context = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen, IReadOnlyDictionary<string, string>? tags = null) => UniTask.CompletedTask;
            public UniTask GoBack(CancellationToken ct, SceneContext? context = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen, IReadOnlyDictionary<string, string>? tags = null) => UniTask.CompletedTask;
            public void ClearHistory() { }
        }
    }
}
