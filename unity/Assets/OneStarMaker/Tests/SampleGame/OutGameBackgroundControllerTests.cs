#nullable enable

using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using SampleGame.OutGame;
using SampleGame.OutGame.Background;
using SampleGame.OutGame.Title;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneStarMaker.Tests.SampleGame
{
    [TestFixture]
    public sealed class OutGameBackgroundControllerTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Request_WithoutSurface_RetainsCurrentDefinition()
        {
            var definition = CreateDefinition();
            var controller = new OutGameBackgroundController();

            controller.Request(definition);

            Assert.That(controller.Current, Is.SameAs(definition));
        }

        [Test]
        public void Attach_AfterRequest_AppliesCurrentDefinitionOnce()
        {
            var definition = CreateDefinition();
            var controller = new OutGameBackgroundController();
            var surface = new RecordingSurface();

            controller.Request(definition);
            controller.Attach(surface);

            Assert.That(surface.AppliedDefinitions, Is.EqualTo(new[] { definition }));
        }

        [Test]
        public void Request_SameDefinitionTwice_DoesNotRedraw()
        {
            var definition = CreateDefinition();
            var controller = new OutGameBackgroundController();
            var surface = new RecordingSurface();
            controller.Attach(surface);

            controller.Request(definition);
            controller.Request(definition);

            Assert.That(surface.AppliedDefinitions, Is.EqualTo(new[] { definition }));
        }

        [Test]
        public void Attach_DifferentSurfaceWithoutDetach_Throws()
        {
            var definition = CreateDefinition();
            var controller = new OutGameBackgroundController();
            var first = new RecordingSurface();
            var second = new RecordingSurface();

            controller.Attach(first);
            controller.Request(definition);

            Assert.That(first.AppliedDefinitions, Is.EqualTo(new[] { definition }));
            Assert.Throws<InvalidOperationException>(() => controller.Attach(second));
            Assert.That(second.AppliedDefinitions, Is.Empty);
        }

        [Test]
        public void Attach_DifferentSurfaceAfterDetach_ReappliesCurrentDefinition()
        {
            var definition = CreateDefinition();
            var controller = new OutGameBackgroundController();
            var first = new RecordingSurface();
            var second = new RecordingSurface();

            controller.Attach(first);
            controller.Request(definition);
            controller.Detach(first);
            controller.Attach(second);

            Assert.That(second.AppliedDefinitions, Is.EqualTo(new[] { definition }));
        }

        [Test]
        public void Request_DefinitionWithoutTexture_Throws()
        {
            var definition = ScriptableObject.CreateInstance<OutGameBackgroundDefinition>();
            _createdObjects.Add(definition);
            var controller = new OutGameBackgroundController();

            Assert.Throws<ArgumentException>(() => controller.Request(definition));
        }

        [Test]
        public void BackgroundUxml_DeclaresInputIgnoringSurface()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/SampleGame/OutGame/Background/OutGameBackground.uxml");

            Assert.That(visualTree, Is.Not.Null);

            var root = visualTree!.CloneTree();
            var surface = root.Q<VisualElement>("outgame-background-surface");

            Assert.That(surface, Is.Not.Null);
            Assert.That(surface!.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(surface.ClassListContains("outgame-background-surface"), Is.True);
        }

        [Test]
        public void BackgroundView_AppliesDefinitionToDeclaredSurface()
        {
            var gameObject = new GameObject("BackgroundView");
            _createdObjects.Add(gameObject);
            var view = gameObject.AddComponent<OutGameBackgroundView>();
            var root = CreateBackgroundRoot(out var surface);
            var definition = CreateDefinition();

            InvokeOnRootCreated(view, root);
            view.Apply(definition);

            Assert.That(view.GetUILayer(), Is.EqualTo(UIView.UILayer.Background));
            Assert.That(surface.style.backgroundImage.value.texture, Is.SameAs(definition.Texture));
            Assert.That(surface.style.unityBackgroundImageTintColor.value, Is.EqualTo(definition.Tint));
        }

        [Test]
        public void BackgroundView_RequestBeforeConnectAndRootCreation_AppliesWhenRootIsCreated()
        {
            var gameObject = new GameObject("BackgroundView");
            _createdObjects.Add(gameObject);
            var view = gameObject.AddComponent<OutGameBackgroundView>();
            var root = CreateBackgroundRoot(out var surface);
            var definition = CreateDefinition();
            var controller = new OutGameBackgroundController();

            controller.Request(definition);
            view.Connect(controller);
            InvokeOnRootCreated(view, root);

            Assert.That(surface.style.backgroundImage.value.texture, Is.SameAs(definition.Texture));
        }

        [Test]
        public void BackgroundView_OnViewDestroy_DetachesFromController()
        {
            var gameObject = new GameObject("BackgroundView");
            _createdObjects.Add(gameObject);
            var view = gameObject.AddComponent<OutGameBackgroundView>();
            var root = CreateBackgroundRoot(out var surface);
            var first = CreateDefinition();
            var second = CreateDefinition();
            var controller = new OutGameBackgroundController();

            InvokeOnRootCreated(view, root);
            view.Connect(controller);
            controller.Request(first);
            InvokeOnViewDestroy(view);
            controller.Request(second);

            Assert.That(surface.style.backgroundImage.value.texture, Is.SameAs(first.Texture));
        }

        [Test]
        public void TitleScene_RequestParentBackground_ForwardsDefinitionToParentRequests()
        {
            var parent = ScriptableObject.CreateInstance<SceneResource>();
            var title = ScriptableObject.CreateInstance<SceneResource>();
            _createdObjects.Add(parent);
            _createdObjects.Add(title);
            SetSceneIdentity(parent, "OutGame");
            SetSceneIdentity(title, "Title");
            typeof(SceneResource)
                .GetField("_parent", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(title, parent);

            var parentScene = new BackgroundRequestsScene(parent);
            var scene = new TitleScene(
                title,
                new RecordingSceneQuery(parent.Identity, parentScene),
                new NullSceneController(),
                Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            var definition = CreateDefinition();

            var method = typeof(TitleScene).GetMethod(
                "RequestParentBackground",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(scene, new object[] { definition });

            Assert.That(parentScene.Current, Is.SameAs(definition));
        }

        private OutGameBackgroundDefinition CreateDefinition()
        {
            var definition = ScriptableObject.CreateInstance<OutGameBackgroundDefinition>();
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            typeof(OutGameBackgroundDefinition)
                .GetField("_texture", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(definition, texture);

            _createdObjects.Add(definition);
            _createdObjects.Add(texture);
            return definition;
        }

        private static VisualElement CreateBackgroundRoot(out VisualElement surface)
        {
            var root = new VisualElement();
            surface = new VisualElement
            {
                name = "outgame-background-surface",
                pickingMode = PickingMode.Ignore,
            };
            surface.AddToClassList("outgame-background-surface");
            root.Add(surface);
            return root;
        }

        private static void InvokeOnRootCreated(OutGameBackgroundView view, VisualElement root)
        {
            var method = typeof(OutGameBackgroundView).GetMethod(
                "OnRootCreated",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(view, new object[] { root });
        }

        private static void InvokeOnViewDestroy(OutGameBackgroundView view)
        {
            var method = typeof(OutGameBackgroundView).GetMethod(
                "OnViewDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(view, null);
        }

        private static void SetSceneIdentity(SceneResource resource, string identity)
        {
            typeof(SceneResource)
                .GetField("_identity", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(resource, identity);
        }

        private sealed class RecordingSurface : IOutGameBackgroundSurface
        {
            public List<OutGameBackgroundDefinition> AppliedDefinitions { get; } = new();

            public void Apply(OutGameBackgroundDefinition definition)
            {
                AppliedDefinitions.Add(definition);
            }
        }

        private sealed class BackgroundRequestsScene : SceneBase, IOutGameBackgroundRequests
        {
            public BackgroundRequestsScene(SceneResource resource)
                : base(resource, new NullSceneQuery(), new NullSceneController())
            {
            }

            public OutGameBackgroundDefinition? Current { get; private set; }

            public void Request(OutGameBackgroundDefinition definition)
            {
                Current = definition;
            }
        }

        private sealed class RecordingSceneQuery : ISceneQuery
        {
            private readonly string _identity;
            private readonly SceneBase _scene;

            public RecordingSceneQuery(string identity, SceneBase scene)
            {
                _identity = identity;
                _scene = scene;
            }

            public SceneBase? GetLoadedScene(string identity)
            {
                return identity == _identity ? _scene : null;
            }

            public bool IsSceneLoaded(string identity) => identity == _identity;
        }

        private sealed class NullSceneQuery : ISceneQuery
        {
            public SceneBase? GetLoadedScene(string identity) => null;

            public bool IsSceneLoaded(string identity) => false;
        }

        private sealed class NullSceneController : ISceneController
        {
            public UniTask AddScene(string sceneIdentify, Func<UniTask>? afterOnLoadedTask, CancellationToken ct, SceneContext? context = null, IProgress<SceneLoadProgress>? progress = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, IReadOnlyDictionary<string, string>? telemetryTags = null, int priority = 100, TelemetryLevel telemetryLevel = TelemetryLevel.Summary)
            {
                return UniTask.CompletedTask;
            }

            public void ClearHistory()
            {
            }

            public UniTask GoBack(CancellationToken ct, SceneContext? context = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen, IReadOnlyDictionary<string, string>? telemetryTags = null)
            {
                return UniTask.CompletedTask;
            }

            public UniTask SwitchScene(string? fromSceneIdentify, string toSceneIdentify, CancellationToken ct, SceneContext? context = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen, IReadOnlyDictionary<string, string>? telemetryTags = null)
            {
                return UniTask.CompletedTask;
            }

            public UniTask UnloadScene(string sceneIdentify, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, IReadOnlyDictionary<string, string>? telemetryTags = null, TelemetryLevel telemetryLevel = TelemetryLevel.Summary)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
