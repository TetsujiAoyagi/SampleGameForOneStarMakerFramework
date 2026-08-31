#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Tests.CameraSystem;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.SceneSystem.Helpers;
using SampleGame.DependOnAll;
using SampleGame.InGame.World;
using SampleGame.OutGame.Scenes;
using RuntimeCameraSystem = OneStarMaker.Runtime.CameraSystem.Core.CameraSystem;
using Cysharp.Threading.Tasks;
using System.Threading;
using OneStarMaker.Foundation.Telemetry;

namespace OneStarMaker.Tests.SampleGame
{
    /// <summary>
    /// SampleGame の scene ファクトリの構築契約を検証する。
    ///
    /// <para>
    /// 依存はすべてコンストラクタ必須引数であり、null は生成時点で弾く。
    /// 未知の scene identity は例外ではなく null を返す。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class GameSceneFactoryTests
    {
        [Test]
        public void Constructor_NullLoggerFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new GameSceneFactory(null!, null!, null!));
        }

        [Test]
        public void Constructor_NullCameraSystem_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new GameSceneFactory(NullLoggerFactory.Instance, null!, new NoopCameraBackgroundApplier()));
        }

        [Test]
        public void Constructor_NullCameraBackgroundApplier_Throws()
        {
            var cameraSystem = new RuntimeCameraSystem(new FakeCameraBackend());

            Assert.Throws<ArgumentNullException>(
                () => new GameSceneFactory(NullLoggerFactory.Instance, cameraSystem, null!));
        }

        [Test]
        public void CreateSceneClass_UnknownIdentity_ReturnsNull()
        {
            var factory = CreateFactory(NullLoggerFactory.Instance);
            var resource = SceneTestHelper.CreateSceneResource("Unknown");

            var scene = factory.CreateSceneClass(
                resource,
                new StubSceneQuery(),
                new StubSceneController());

            Assert.IsNull(scene);
        }

        [Test]
        public void CreateSceneClass_CellIdentity_ReturnsDemoCellScene()
        {
            var factory = CreateFactory(NullLoggerFactory.Instance);
            var resource = SceneTestHelper.CreateSceneResource("Cell_0_0");

            var scene = factory.CreateSceneClass(
                resource,
                new StubSceneQuery(),
                new StubSceneController());

            Assert.That(scene, Is.TypeOf<DemoCellScene>());
        }

        private static GameSceneFactory CreateFactory(ILoggerFactory loggerFactory)
        {
            var cameraSystem = new RuntimeCameraSystem(new FakeCameraBackend());
            return new GameSceneFactory(loggerFactory, cameraSystem, new NoopCameraBackgroundApplier());
        }

        private sealed class NoopCameraBackgroundApplier : ICameraBackgroundApplier
        {
            public void SetClearFlag(ICameraView view, ClearFlag clearFlag, UnityEngine.Color color)
            {
            }
        }

        private sealed class StubSceneQuery : ISceneQuery
        {
            public SceneBase? GetLoadedScene(string identity) => null;

            public bool IsSceneLoaded(string identity) => false;
            public bool IsSceneStable(string identity) => false;
        }

        private sealed class StubSceneController : ISceneController
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

    /// <summary>
    /// SampleGame の scene 間コマンド伝搬を検証する。
    ///
    /// <para>
    /// 子 scene の追加・自身の unload といった遷移要求が、
    /// scene 側から SceneDirector の正しい API に届くことを主張する。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class GameSceneCommandTests
    {
        [Test]
        public void HpGaugeScene_HandleOpenDialogRequested_CallsAddSceneConfirmDialog()
        {
            var sceneController = new StubSceneController();
            var resource = SceneTestHelper.CreateSceneResource("HpGauge");

            var scene = new HpGaugeScene(
                resource,
                new StubSceneQuery(),
                sceneController,
                NullLoggerFactory.Instance);

            InvokeNonPublic(scene, nameof(HpGaugeScene), "HandleOpenDialogRequested");

            Assert.AreEqual(1, sceneController.AddSceneCallCount);
            Assert.AreEqual("ConfirmDialog", sceneController.LastAddedSceneIdentity);
        }

        [Test]
        public void ConfirmDialogScene_HandleDecided_CallsUnloadSelf()
        {
            var sceneController = new StubSceneController();
            var resource = SceneTestHelper.CreateSceneResource("ConfirmDialog");
            var scene = new ConfirmDialogScene(
                resource,
                new StubSceneQuery(),
                sceneController,
                NullLoggerFactory.Instance);

            InvokeNonPublic(scene, nameof(ConfirmDialogScene), "HandleDecided", false);

            Assert.AreEqual(1, sceneController.UnloadSceneCallCount);
            Assert.AreEqual("ConfirmDialog", sceneController.LastUnloadedSceneIdentity);
        }

        private static void InvokeNonPublic(
            object target,
            string typeName,
            string methodName,
            params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, $"{typeName}.{methodName} が見つかりません。");
            method!.Invoke(target, arguments);
        }

        private sealed class StubSceneQuery : ISceneQuery
        {
            public SceneBase? GetLoadedScene(string identity) => null;

            public bool IsSceneLoaded(string identity) => false;
            public bool IsSceneStable(string identity) => false;
        }

        private sealed class StubSceneController : ISceneController
        {
            public int AddSceneCallCount { get; private set; }
            public string? LastAddedSceneIdentity { get; private set; }
            public int UnloadSceneCallCount { get; private set; }
            public string? LastUnloadedSceneIdentity { get; private set; }

            public UniTask AddScene(string sceneIdentify, Func<UniTask>? afterOnLoadedTask, CancellationToken ct, SceneContext? context = null, IProgress<SceneLoadProgress>? progress = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, IReadOnlyDictionary<string, string>? telemetryTags = null, int priority = 100, TelemetryLevel telemetryLevel = TelemetryLevel.Summary)
            {
                AddSceneCallCount++;
                LastAddedSceneIdentity = sceneIdentify;
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
                UnloadSceneCallCount++;
                LastUnloadedSceneIdentity = sceneIdentify;
                return UniTask.CompletedTask;
            }
        }

    }
}
