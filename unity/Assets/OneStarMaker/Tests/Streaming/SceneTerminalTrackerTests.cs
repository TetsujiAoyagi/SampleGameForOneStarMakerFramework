#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using SampleGame.InGame.Streaming;
using UnityEngine;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// S-4b: 終端 tracker の世代照合。購読前イベントを後から完了扱いしない。
    /// </summary>
    [TestFixture]
    public sealed class SceneTerminalTrackerTests
    {
        private readonly System.Collections.Generic.List<ScriptableObject> _assets = new();

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

        [Test]
        public async UniTask WaitForInstance_CompletesOnMatchingTerminal()
        {
            var terminals = new FakeTerminalEvents();
            using var tracker = new SceneTerminalTracker(terminals);
            var instance = CreateInstance("Season_Spring");
            tracker.RegisterObservedInstance(instance);

            var wait = tracker.WaitForInstance(instance, CancellationToken.None);
            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Pending));

            terminals.Emit("Season_Spring", SceneTerminalKind.Removed);
            await wait;
        }

        [Test]
        public void EventBeforeRegister_DoesNotCompleteLaterWait()
        {
            var terminals = new FakeTerminalEvents();
            using var tracker = new SceneTerminalTracker(terminals);
            terminals.Emit("Season_Spring", SceneTerminalKind.Removed);

            var instance = CreateInstance("Season_Spring");
            tracker.RegisterObservedInstance(instance);
            var wait = tracker.WaitForInstance(instance, CancellationToken.None);
            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Pending));
        }

        [Test]
        public async UniTask LaterInstance_DoesNotCompleteOnOldTerminal()
        {
            var terminals = new FakeTerminalEvents();
            using var tracker = new SceneTerminalTracker(terminals);
            var first = CreateInstance("Season_Spring");
            tracker.RegisterObservedInstance(first);
            terminals.Emit("Season_Spring", SceneTerminalKind.Removed);
            await tracker.WaitForInstance(first, CancellationToken.None);

            var second = CreateInstance("Season_Spring");
            tracker.RegisterObservedInstance(second);
            var wait = tracker.WaitForInstance(second, CancellationToken.None);
            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Pending));
            terminals.Emit("Season_Spring", SceneTerminalKind.CancelCleanedUp);
            await wait;
        }

        private TestSceneBase CreateInstance(string identity)
        {
            var resource = SceneTestHelper.CreateSceneResource(identity);
            _assets.Add(resource);
            var query = new StubQuery();
            return new TestSceneBase(resource, query, new StubController());
        }

        private sealed class StubQuery : ISceneQuery
        {
            public SceneBase? GetLoadedScene(string identity) => null;
            public bool IsSceneLoaded(string identity) => false;
            public bool IsSceneStable(string identity) => false;
        }

        private sealed class StubController : ISceneController
        {
            public UniTask AddScene(string sceneIdentify, Func<UniTask>? afterOnLoadedTask, CancellationToken ct, SceneContext? context = null, IProgress<SceneLoadProgress>? progress = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, System.Collections.Generic.IReadOnlyDictionary<string, string>? telemetryTags = null, int priority = 100, OneStarMaker.Foundation.Telemetry.TelemetryLevel telemetryLevel = OneStarMaker.Foundation.Telemetry.TelemetryLevel.Summary)
                => UniTask.CompletedTask;

            public UniTask UnloadScene(string sceneIdentify, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, System.Collections.Generic.IReadOnlyDictionary<string, string>? telemetryTags = null, OneStarMaker.Foundation.Telemetry.TelemetryLevel telemetryLevel = OneStarMaker.Foundation.Telemetry.TelemetryLevel.Summary)
                => UniTask.CompletedTask;

            public UniTask SwitchScene(string? fromSceneIdentify, string toSceneIdentify, CancellationToken ct, SceneContext? context = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen, System.Collections.Generic.IReadOnlyDictionary<string, string>? telemetryTags = null)
                => UniTask.CompletedTask;

            public UniTask GoBack(CancellationToken ct, SceneContext? context = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen, System.Collections.Generic.IReadOnlyDictionary<string, string>? telemetryTags = null)
                => UniTask.CompletedTask;

            public void ClearHistory()
            {
            }
        }
    }

    internal sealed class FakeTerminalEvents : ISceneTerminalEvents
    {
        private Action<SceneTerminalEvent>? _handlers;

        public IDisposable Subscribe(Action<SceneTerminalEvent> handler)
        {
            _handlers += handler;
            return new Unsub(() => _handlers -= handler);
        }

        internal void Emit(string identity, SceneTerminalKind kind)
            => _handlers?.Invoke(new SceneTerminalEvent(identity, kind));

        private sealed class Unsub : IDisposable
        {
            private Action? _dispose;
            internal Unsub(Action dispose) => _dispose = dispose;
            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
