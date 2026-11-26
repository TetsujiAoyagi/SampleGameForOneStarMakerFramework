#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Foundation.Config;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.DebugSocketServices;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Tests.AssetManagement;
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using UnityEngine;

namespace OneStarMaker.Tests.Bootstrap
{
    /// <summary>
    /// Framework のデバッグコマンドの構成（<see cref="FrameworkDebugCommands.Compose"/>）と、initializer の配線の 1 行を固定する（AC8）。
    /// あわせて、ReleaseAll が SceneDirector の参照を null に戻すこと（AC9）を固定する。<c>osm.scenes</c> は Dispose 済みの
    /// SceneDirector を扱わず、この null 化に依存して「旧 Play の Scene を出さない」を保つためである。
    /// </summary>
    [TestFixture]
    public sealed class FrameworkDebugCommandsTests
    {
        private readonly List<UnityEngine.Object> _created = new();
        private ProbeInitializer? _initializer;

        [TearDown]
        public void TearDown()
        {
            // ReleaseAll を通して initializer が持つ物を戻す（SceneDirector の Dispose を含む）。
            _initializer?.RunCleanup();
            _initializer = null;

            foreach (var obj in _created)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            _created.Clear();
        }

        // ═══════════════════════════════════════════
        //  Compose
        // ═══════════════════════════════════════════

        [Test]
        public void Compose_OutsideTheEditor_ReturnsTheGameDispatcherItself()
        {
            var game = new CountingDispatcher();

            var composed = FrameworkDebugCommands.Compose(false, game, () => null);

            Assert.That(composed, Is.SameAs(game), "Player では Game の dispatcher を同じ参照のまま渡す");
        }

        [Test]
        public void Compose_InTheEditor_ResolvesOsmScenesAndPassesOtherNamesToTheGame()
        {
            var game = new CountingDispatcher();

            var composed = FrameworkDebugCommands.Compose(true, game, () => null);

            var scenes = composed.DispatchAsync(
                new DebugCommandEnvelopeV1 { CommandType = "osm.scenes", RequestId = "s" },
                CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(scenes.Success, Is.False);
            Assert.That(scenes.Message, Is.EqualTo("unavailable: scene-director-not-created"),
                "osm.scenes を Framework が解決する（SceneDirector が無いので読めない）");
            Assert.That(game.CallCount, Is.EqualTo(0));

            var other = composed.DispatchAsync(
                new DebugCommandEnvelopeV1 { CommandType = "debug.set-active", RequestId = "o" },
                CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(game.CallCount, Is.EqualTo(1), "他の名前は Game へ渡る");
            Assert.That(other.Message, Is.EqualTo(CountingDispatcher.Message));
        }

        // ═══════════════════════════════════════════
        //  initializer の配線
        // ═══════════════════════════════════════════

        [Test]
        public void CreateDebugSocketService_Enabled_CallsTheGameHookOnceAndWrapsItForTheEditor()
        {
            _initializer = new ProbeInitializer();

            var service = _initializer.CallCreateDebugSocketService(CreateConfig(enabled: true));
            try
            {
                Assert.That(service, Is.Not.Null);
                Assert.That(_initializer.CreateDispatcherCallCount, Is.EqualTo(1), "CreateDebugCommandDispatcher は 1 回だけ呼ばれる");

                // EditMode のテストは Editor で動くので、DebugSocketService の中の鎖は main thread の decorator → 振り分け → Game になる。
                var mainThread = GetPrivateField(service!, typeof(DebugSocketService), "_dispatcher");
                var inner = GetPrivateField(mainThread, mainThread.GetType(), "_inner");
                Assert.That(inner, Is.InstanceOf<FrameworkFirstDebugCommandDispatcher>());
                var game = GetPrivateField(inner, typeof(FrameworkFirstDebugCommandDispatcher), "_game");
                Assert.That(game, Is.SameAs(_initializer.GameDispatcher));
            }
            finally
            {
                // 実の DebugSocketService を作ったので明示して破棄する（Dispose は StopAsync().Forget() を呼ぶ。未起動なので即座に返る）。
                service?.Dispose();
            }
        }

        [Test]
        public void CreateDebugSocketService_Disabled_ReturnsNullWithoutCallingTheGameHook()
        {
            _initializer = new ProbeInitializer();

            var service = _initializer.CallCreateDebugSocketService(CreateConfig(enabled: false));

            Assert.That(service, Is.Null);
            Assert.That(_initializer.CreateDispatcherCallCount, Is.EqualTo(0));
        }

        // ═══════════════════════════════════════════
        //  AC9: ReleaseAll が SceneDirector を null に戻す
        // ═══════════════════════════════════════════

        [Test]
        public void ReleaseAll_ClearsTheSceneDirector()
        {
            _initializer = new ProbeInitializer();
            var director = CreateSceneDirector();
            _initializer.InjectSceneDirector(director);
            Assert.That(_initializer.CurrentSceneDirector, Is.SameAs(director), "前提: SceneDirector がある");

            _initializer.RunCleanup();

            Assert.That(_initializer.CurrentSceneDirector, Is.Null,
                "ReleaseAll の後、SceneDirector の参照は null（osm.scenes は Dispose 済みを読まず unavailable になる）");
        }

        // ─── helpers ───

        private TestableSceneDirector CreateSceneDirector()
        {
            var uiCommonGo = new GameObject("UICommon_FrameworkDebugCommandsTests");
            _created.Add(uiCommonGo);
            var uiCommon = uiCommonGo.AddComponent<UICommon>();
            var resource = SceneTestHelper.CreateSceneResource("TestScene", LoadType.OnDemand);
            _created.Add(resource);
            var map = SceneTestHelper.CreateSceneResourceMap(resource);
            _created.Add(map);
            return new TestableSceneDirector(
                new FakeSceneFactory(),
                uiCommon,
                map,
                new Runtime.AssetManagement.AssetManagement(new FakeAssetBackend()));
        }

        private static AppConfig CreateConfig(bool enabled)
            => new(new IConfigProvider[]
            {
                new InMemoryConfigProvider(new Dictionary<string, string>
                {
                    ["debugSocket:enabled"] = enabled ? "true" : "false",
                    // 起動は CreateDebugSocketService の外（initializer の後段）なので、ここでは listener を開かない。
                    ["debugSocket:autoStart"] = "false",
                }),
            });

        private static object GetPrivateField(object target, Type declaringType, string name)
            => declaringType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

        private sealed class InMemoryConfigProvider : IConfigProvider
        {
            private readonly Dictionary<string, string> _values;

            public InMemoryConfigProvider(Dictionary<string, string> values)
            {
                _values = values;
            }

            public void Load(Dictionary<string, string> store)
            {
                foreach (var pair in _values)
                {
                    store[pair.Key] = pair.Value;
                }
            }
        }

        private sealed class CountingDispatcher : IDebugCommandDispatcher
        {
            internal const string Message = "from-game";

            public int CallCount { get; private set; }

            public UniTask<DebugCommandResultEnvelopeV1> DispatchAsync(
                DebugCommandEnvelopeV1 command,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return UniTask.FromResult(new DebugCommandResultEnvelopeV1 { RequestId = command.RequestId, Message = Message });
            }
        }

        /// <summary>initializer の protected の hook と ReleaseAll の経路を、公開 API を足さずにテストから呼ぶための派生。</summary>
        private sealed class ProbeInitializer : AbstractApplicationInitializer
        {
            internal int CreateDispatcherCallCount { get; private set; }

            internal CountingDispatcher GameDispatcher { get; } = new();

            internal SceneDirector? CurrentSceneDirector => SceneDirector;

            internal DebugSocketService? CallCreateDebugSocketService(AppConfig config) => CreateDebugSocketService(config);

            // ReleaseAll は private なので、同じ経路を通る protected static の入口から呼ぶ（既存の Bootstrap の test と同じ）。
            internal void RunCleanup() => BootstrapSubsystemRegistration(this);

            // AfterSceneLoad を通さずに SceneDirector を持たせる（既存の test が _assetManagement を差し替えるのと同じ方法）。
            internal void InjectSceneDirector(SceneDirector director)
                => typeof(AbstractApplicationInitializer)
                    .GetField("_sceneDirector", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(this, director);

            protected override IDebugCommandDispatcher CreateDebugCommandDispatcher()
            {
                CreateDispatcherCallCount++;
                return GameDispatcher;
            }

            protected override ISceneFactory CreateSceneFactory() =>
                throw new NotSupportedException("probe: the scene factory is not used.");

            protected override ILoadingDisplay CreateLoadingDisplay() =>
                throw new NotSupportedException("probe: the loading display is not used.");

            protected override string GetUICommonPrefabAddress() => "probe-ui";

            protected override string GetSceneResourceMapAddress() => "probe-map";
        }
    }
}
