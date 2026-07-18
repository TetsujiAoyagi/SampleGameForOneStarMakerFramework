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
using SampleGame.OutGame;
using SampleGame.OutGame.Scenes;
using SampleGame.OutGame.Title;
using RuntimeCameraSystem = OneStarMaker.Runtime.CameraSystem.Core.CameraSystem;

namespace OneStarMaker.Tests.SampleGame
{
    [TestFixture]
    public sealed class GameSceneFactoryTests
    {
        private StubSceneQuery _sceneQuery = null!;

        [SetUp]
        public void SetUp()
        {
            _sceneQuery = new StubSceneQuery();
        }

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
        public void CreateSceneClass_WithNullLoggerFactoryInstance_DoesNotThrow()
        {
            var factory = CreateFactory(NullLoggerFactory.Instance);
            var resource = SceneTestHelper.CreateSceneResource("Title");

            Assert.DoesNotThrow(() => factory.CreateSceneClass(resource, _sceneQuery));
        }

        [TestCase("Title", typeof(TitleScene))]
        [TestCase("OutGame", typeof(OutGameScene))]
        [TestCase("HpGauge", typeof(HpGaugeScene))]
        [TestCase("ConfirmDialog", typeof(ConfirmDialogScene))]
        public void CreateSceneClass_KnownIdentity_ReturnsExpectedSceneType(string identity, Type expectedType)
        {
            var factory = CreateFactory(NullLoggerFactory.Instance);
            var resource = SceneTestHelper.CreateSceneResource(identity);

            var scene = factory.CreateSceneClass(resource, _sceneQuery);

            Assert.NotNull(scene);
            Assert.IsInstanceOf(expectedType, scene);
        }

        [Test]
        public void CreateSceneClass_UnknownIdentity_ReturnsNull()
        {
            var factory = CreateFactory(NullLoggerFactory.Instance);
            var resource = SceneTestHelper.CreateSceneResource("Unknown");

            var scene = factory.CreateSceneClass(resource, _sceneQuery);

            Assert.IsNull(scene);
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
        }
    }

    [TestFixture]
    public sealed class GameSceneLoggingTests
    {
        [Test]
        public void TitleScene_Constructor_UsesTypedLoggerCategory()
        {
            var capturingFactory = new CategoryCapturingLoggerFactory();
            var resource = SceneTestHelper.CreateSceneResource("Title");

            _ = new TitleScene(resource, new StubSceneQuery(), capturingFactory);

            Assert.AreEqual(typeof(TitleScene).FullName, capturingFactory.LastCategory);
        }

        [Test]
        public void TitleScene_OnInitialize_WritesInformationLog()
        {
            using var capturingFactory = new CapturingLoggerFactory();
            var resource = SceneTestHelper.CreateSceneResource("Title");
            var scene = new TitleScene(resource, new StubSceneQuery(), capturingFactory);

            InvokeOnInitialize(scene);

            Assert.AreEqual(1, capturingFactory.Entries.Count);
            Assert.AreEqual(typeof(TitleScene).FullName, capturingFactory.Entries[0].Category);
            Assert.AreEqual(LogLevel.Information, capturingFactory.Entries[0].Level);
            StringAssert.Contains("Initialized", capturingFactory.Entries[0].Message);
        }

        [Test]
        public void HpGaugeScene_HandleOpenDialogRequested_WithoutSceneDirector_WritesErrorLog()
        {
            using var capturingFactory = new CapturingLoggerFactory();
            var resource = SceneTestHelper.CreateSceneResource("HpGauge");
            var scene = new HpGaugeScene(resource, new StubSceneQuery(), capturingFactory);

            InvokeNonPublic(scene, nameof(HpGaugeScene), "HandleOpenDialogRequested");

            Assert.AreEqual(1, capturingFactory.Entries.Count);
            Assert.AreEqual(typeof(HpGaugeScene).FullName, capturingFactory.Entries[0].Category);
            Assert.AreEqual(LogLevel.Error, capturingFactory.Entries[0].Level);
            StringAssert.Contains("SceneDirector", capturingFactory.Entries[0].Message);
        }

        private static void InvokeOnInitialize(SceneBase scene)
        {
            var method = scene.GetType().GetMethod(
                "OnInitialize",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method!.Invoke(scene, null);
        }

        private static void InvokeNonPublic(object target, string typeName, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, $"{typeName}.{methodName} が見つかりません。");
            method!.Invoke(target, null);
        }

        private sealed class StubSceneQuery : ISceneQuery
        {
            public SceneBase? GetLoadedScene(string identity) => null;

            public bool IsSceneLoaded(string identity) => false;
        }

        private sealed class CategoryCapturingLoggerFactory : ILoggerFactory
        {
            public string? LastCategory { get; private set; }

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public ILogger CreateLogger(string categoryName)
            {
                LastCategory = categoryName;
                return NullLogger.Instance;
            }

            public void Dispose()
            {
            }
        }

        private sealed class CapturingLoggerFactory : ILoggerFactory, IDisposable
        {
            private readonly CapturingLoggerProvider _provider = new();

            public IReadOnlyList<CapturedLogEntry> Entries => _provider.Entries;

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public ILogger CreateLogger(string categoryName)
                => _provider.CreateLogger(categoryName);

            public void Dispose()
            {
                _provider.Dispose();
            }
        }

        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            public List<CapturedLogEntry> Entries { get; } = new();

            public ILogger CreateLogger(string categoryName)
                => new CapturingLogger(categoryName, Entries);

            public void Dispose()
            {
            }
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly string _category;
            private readonly List<CapturedLogEntry> _entries;

            public CapturingLogger(string category, List<CapturedLogEntry> entries)
            {
                _category = category;
                _entries = entries;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull
                => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _entries.Add(new CapturedLogEntry(_category, logLevel, formatter(state, exception)));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }

        private readonly struct CapturedLogEntry
        {
            public string Category { get; }
            public LogLevel Level { get; }
            public string Message { get; }

            public CapturedLogEntry(string category, LogLevel level, string message)
            {
                Category = category;
                Level = level;
                Message = message;
            }
        }
    }
}
