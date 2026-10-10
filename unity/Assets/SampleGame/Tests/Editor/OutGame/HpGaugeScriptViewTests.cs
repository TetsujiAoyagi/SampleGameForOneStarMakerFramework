#nullable enable

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.ScriptSystem;
using OneStarMaker.Runtime.UpdateSystem.Hosting;
using SampleGame.OutGame.HpGauge;
using SampleGame.OutGame.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SampleGame.Tests.Editor.OutGame
{
    [TestFixture]
    public sealed class HpGaugeScriptViewTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string HpUxml = "Assets/SampleGame/OutGame/HpGauge/HpGauge.uxml";
        private const string ContentModeKey = "SAMPLEGAME_CONTENT__RUNTIMEMODE";
        private SceneSetup[]? _sceneSetup;
        private string? _previousContentMode;
        private bool _contentModeChanged;
        private GameObject? _gameObject;
        private HpGaugeView? _view;
        private UpdateSystemHost? _host;

        [TearDown]
        public void TearDown()
        {
            if (EditorApplication.isPlaying) return;
            if (_gameObject != null) UnityEngine.Object.DestroyImmediate(_gameObject);
            _gameObject = null;
            _view = null;
            _host?.Dispose();
            _host = null;
        }

        [UnityTearDown]
        public IEnumerator CleanupPlayMode()
        {
            try
            {
                if (EditorApplication.isPlaying)
                {
                    if (_gameObject != null)
                    {
                        UnityEngine.Object.Destroy(_gameObject);
                        yield return null;
                    }
                    _gameObject = null;
                    _view = null;
                    _host?.Dispose();
                    _host = null;
                    yield return null;
                    yield return new ExitPlayMode();
                }

                if (_sceneSetup != null)
                {
                    if (_sceneSetup.Any(scene => scene.isLoaded && scene.isActive))
                        EditorSceneManager.RestoreSceneManagerSetup(_sceneSetup);
                    else
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    _sceneSetup = null;
                }
            }
            finally
            {
                if (_contentModeChanged)
                {
                    Environment.SetEnvironmentVariable(ContentModeKey, _previousContentMode,
                        EnvironmentVariableTarget.Process);
                    _contentModeChanged = false;
                }
            }
        }

        [Test]
        public void View_UsesNamedUxmlAndOneRunAtATime()
        {
            _host = new UpdateSystemHost();
            CreateView();
            Assert.That(Status(), Is.EqualTo("Idle"));
            Assert.That(_view!.Root.Q<Button>("damage-button"), Is.Not.Null);
            Assert.That(_view.Root.Q<Button>("heal-button"), Is.Not.Null);
            Assert.That(_view.Root.Q<Button>("open-dialog-button"), Is.Not.Null);
            Assert.That(_view.Root.Q<Button>("run-script-button"), Is.Not.Null);
            Assert.That(_view.Root.Q<Button>("stop-script-button"), Is.Not.Null);
            Run();
            var first = Current();
            Assert.That(first, Is.Not.Null);
            Run();
            Assert.That(Current(), Is.SameAs(first));
            Assert.That(Status(), Is.EqualTo("Running"));
            Step();
            var afterDamage = Hp();
            Assert.That(afterDamage, Is.LessThan(100));
            Step();
            Assert.That(Status(), Is.EqualTo("Waiting"));
            Invoke("StopScript");
            Assert.That(first!.State, Is.EqualTo(ScriptCommandRunState.Stopped));
            Assert.That(Current(), Is.Null);
            Assert.That(Status(), Is.EqualTo("Stopped"));
            for (var i = 0; i < 8; i++) Step();
            Assert.That(Hp(), Is.EqualTo(afterDamage));
            Run();
            Assert.That(Current(), Is.Not.SameAs(first));
            Assert.That(Status(), Is.EqualTo("Running"));
            Step();
            Assert.That(Hp(), Is.LessThan(afterDamage));
        }

        [Test]
        public void RegistrationFalseAndThrow_CloseProvisionalRunAndShowFailed()
        {
            CreateView();
            Run(); // No UpdateSystemHost: RegisterElement returns false.
            Assert.That(Current(), Is.Null);
            Assert.That(Status(), Is.EqualTo("Failed"));
            UnityEngine.Object.DestroyImmediate(_gameObject);
            _gameObject = null;
            _view = null;
            _host = new UpdateSystemHost();
            _host.Coordinator.GetOrCreateLayer("HpGaugeScriptDemo", layerOrder: 1);
            CreateView();
            Run(); // Layer order mismatch throws before registration.
            Assert.That(Current(), Is.Null);
            Assert.That(Status(), Is.EqualTo("Failed"));
        }

        [Test]
        public void RegistrationThrow_WithThrowingWarningHandler_StillClosesRunner()
        {
            _host = new UpdateSystemHost();
            _host.Coordinator.GetOrCreateLayer("HpGaugeScriptDemo", layerOrder: 1);
            CreateView();
            var previous = Debug.unityLogger.logHandler;
            var throwing = new ThrowingWarningLogHandler(previous, Current);
            try
            {
                Debug.unityLogger.logHandler = throwing;
                Assert.DoesNotThrow(Run);
            }
            finally { Debug.unityLogger.logHandler = previous; }

            Assert.That(throwing.WarningCalls, Is.GreaterThan(0));
            Assert.That(throwing.CapturedRunner, Is.Not.Null);
            Assert.That(throwing.CapturedRunner!.State, Is.EqualTo(ScriptCommandRunState.Stopped));
            Assert.That(typeof(ScriptCommandRunner).GetField("_host", Hidden)!
                .GetValue(throwing.CapturedRunner), Is.Null);
            Assert.That(Current(), Is.Null);
            Assert.That(Status(), Is.EqualTo("Failed"));
        }

        [Test]
        public void OldNotificationAndDelayedUnregister_CannotMutateNewRun()
        {
            _host = new UpdateSystemHost();
            CreateView();
            Run();
            var old = Current()!;
            Step();
            Invoke("StopScript");
            Run(); // old structural removal may still be pending.
            var next = Current()!;
            Assert.That(next, Is.Not.SameAs(old));
            Invoke("OnScriptStateChanged", old, ScriptCommandRunState.Failed);
            Assert.That(Status(), Is.EqualTo("Running"));
            var hp = Hp();
            Step();
            Assert.That(Hp(), Is.LessThan(hp));
            Assert.That(old.State, Is.EqualTo(ScriptCommandRunState.Stopped));
            Assert.That(((FieldInfo)typeof(ScriptCommandRunner).GetField("_host", Hidden)!).GetValue(old), Is.Null);
        }

        [Test]
        public void TrackAndPreUnload_CloseGateBeforeViewModelDisposal()
        {
            _host = new UpdateSystemHost();
            CreateView();
            Run();
            var runner = Current()!;
            var resource = ScriptableObject.CreateInstance<SceneResource>();
            try
            {
                var scene = new HpGaugeScene(resource, new EmptySceneQuery(), null!, NullLoggerFactory.Instance);
                typeof(SceneBase).GetField("_uiView", Hidden)!.SetValue(scene, _view);
                typeof(HpGaugeScene).GetMethod("OnPreUnLoadedImpl", Hidden)!.Invoke(scene, null);
                Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Stopped));
                Assert.That(Current(), Is.Null);
                Run();
                Assert.That(Current(), Is.Null); // shutdown rejects a new Run.
            }
            finally { UnityEngine.Object.DestroyImmediate(resource); }
            UnityEngine.Object.DestroyImmediate(_gameObject);
            _gameObject = null;
            Assert.That(((FieldInfo)typeof(ScriptCommandRunner).GetField("_host", Hidden)!).GetValue(runner), Is.Null);
        }

        [UnityTest]
        public IEnumerator TrackCleanup_StopsLiveRunnerWhenViewIsDestroyed()
        {
            _sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _previousContentMode = Environment.GetEnvironmentVariable(ContentModeKey,
                EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(ContentModeKey, "addressables", EnvironmentVariableTarget.Process);
            _contentModeChanged = true;
            yield return new EnterPlayMode();

            _host = new UpdateSystemHost();
            CreateView();
            Run();
            var runner = Current()!;
            UnityEngine.Object.Destroy(_gameObject);
            yield return null;
            Assert.That(_gameObject == null, Is.True);
            _gameObject = null;
            _view = null;
            Assert.That(runner.State, Is.EqualTo(ScriptCommandRunState.Stopped));
            Assert.That(typeof(ScriptCommandRunner).GetField("_host", Hidden)!.GetValue(runner), Is.Null);
        }

        [Test]
        public void SceneReferencesGraphAndMap_ContainExactHpPath()
        {
            AssertSceneTree("Assets/SampleGame/OutGame/HpGauge/HpGauge.unity", HpUxml, typeof(HpGaugeView));
            AssertSceneTree("Assets/SampleGame/OutGame/ConfirmDialog/ConfirmDialog.unity",
                "Assets/SampleGame/OutGame/ConfirmDialog/ConfirmDialog.uxml",
                typeof(SampleGame.OutGame.ConfirmDialog.ConfirmDialogView));
            var graph = AssetDatabase.LoadAssetAtPath<SceneGraphEdges>("Assets/SceneGraphData/Graphs/Total.asset");
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>("Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset");
            Assert.That(graph, Is.Not.Null);
            Assert.That(map, Is.Not.Null);
            Assert.That(graph!.Edges.Count(e => e.Parent != null && e.Parent.Identity == "OutGameScene" && e.Child != null && e.Child.Identity == "HpGauge"), Is.EqualTo(1));
            Assert.That(graph.Edges.Count(e => e.Parent != null && e.Parent.Identity == "HpGauge" && e.Child != null && e.Child.Identity == "ConfirmDialog"), Is.EqualTo(1));
            var outGame = map!.GetSceneResource("OutGameScene");
            var hp = map.GetSceneResource("HpGauge");
            var dialog = map.GetSceneResource("ConfirmDialog");
            Assert.That(outGame, Is.Not.Null);
            Assert.That(hp, Is.Not.Null);
            Assert.That(dialog, Is.Not.Null);
            Assert.That(hp!.Parent, Is.SameAs(outGame));
            Assert.That(dialog!.Parent, Is.SameAs(hp));
            Assert.That(outGame!.Children.Count(child => child == hp), Is.EqualTo(1));
            Assert.That(hp.Children.Count(child => child == dialog), Is.EqualTo(1));
            Assert.That(hp.LoadType, Is.EqualTo(OneStarMaker.Runtime.AssetDescriptions.LoadType.OnDemand));
            Assert.That(dialog.LoadType, Is.EqualTo(OneStarMaker.Runtime.AssetDescriptions.LoadType.OnDemand));
            var nodes = AssetDatabase.FindAssets("t:SceneNodeData", new[] { "Assets/SceneGraphData/Nodes" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SceneNodeData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(node => node != null).ToArray()!;
            var graphs = AssetDatabase.FindAssets("t:SceneGraphEdges", new[] { "Assets/SceneGraphData/Graphs" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SceneGraphEdges>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null).ToArray()!;
            Assert.That(map.GenerateHash, Is.EqualTo(SceneResourceGenerator.ComputeCurrentHash(nodes, graphs)));
        }

        private static void AssertSceneTree(string scenePath, string uxmlPath, Type viewType)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var views = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren(viewType, true)).ToArray();
                Assert.That(views, Has.Length.EqualTo(1));
                var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
                Assert.That(new SerializedObject(views[0]).FindProperty("_visualTreeAsset").objectReferenceValue,
                    Is.SameAs(tree));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private void CreateView()
        {
            _gameObject = new GameObject("HpGaugeScriptViewTest");
            _view = _gameObject.AddComponent<HpGaugeView>();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HpUxml);
            Assert.That(tree, Is.Not.Null);
            _view.AssignVisualTreeAssetForEditor(tree!);
            _view.Initialize();
        }

        private void Run() => Invoke("RunScript");
        private void Invoke(string method, params object[] args)
            => typeof(HpGaugeView).GetMethod(method, Hidden)!.Invoke(_view, args);
        private ScriptCommandRunner? Current()
            => (ScriptCommandRunner?)typeof(HpGaugeView).GetField("_currentRunner", Hidden)!.GetValue(_view);
        private string Status() => _view!.Root.Q<Label>("script-status-label")!.text;
        private int Hp()
        {
            var model = (HpGaugeViewModel)typeof(HpGaugeView).GetField("_viewModel", Hidden)!.GetValue(_view)!;
            var hp = typeof(HpGaugeViewModel).GetProperty("Hp")!.GetValue(model)!;
            return (int)hp.GetType().GetProperty("CurrentValue")!.GetValue(hp)!;
        }
        private void Step()
        {
            _host!.Coordinator.ActivatePendingRegistrations();
            _host.Coordinator.RunUpdate(0.1f, 0.1f);
            _host.Coordinator.RunLateUpdate(0.1f, 0.1f);
            _host.Coordinator.ApplyMainThreadChanges();
            _host.Coordinator.ApplyStructuralChanges();
        }
        private sealed class ThrowingWarningLogHandler : ILogHandler
        {
            private readonly ILogHandler _previous;
            private readonly Func<ScriptCommandRunner?> _current;

            public ThrowingWarningLogHandler(ILogHandler previous, Func<ScriptCommandRunner?> current)
            {
                _previous = previous;
                _current = current;
            }

            public int WarningCalls { get; private set; }
            public ScriptCommandRunner? CapturedRunner { get; private set; }

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                if (logType == LogType.Warning)
                {
                    WarningCalls++;
                    CapturedRunner ??= _current();
                    throw new InvalidOperationException("Warning handler failed");
                }
                _previous.LogFormat(logType, context, format, args);
            }

            public void LogException(Exception exception, UnityEngine.Object context)
                => _previous.LogException(exception, context);
        }

        private sealed class EmptySceneQuery : ISceneQuery
        {
            public SceneBase? GetLoadedScene(string identity) => null;
            public bool IsSceneLoaded(string identity) => false;
            public bool IsSceneStable(string identity) => false;
        }
    }
}
