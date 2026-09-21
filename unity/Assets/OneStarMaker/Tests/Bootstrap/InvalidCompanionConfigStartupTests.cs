#nullable enable

using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Foundation.Config;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Tests.AssetManagement;
using SampleGame.InGame.Streaming;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Bootstrap
{
    [TestFixture]
    public sealed class InvalidCompanionConfigStartupTests
    {
        private const string EnvironmentKey = "S4ATEST_WORLD__CELLCOMPANIONSET";
        private const string ContentModeKey = "S4ATEST_CONTENT__RUNTIMEMODE";
        private const string InstalledPathKey = "S4ATEST_CONTENT__INSTALLEDREVISIONPATH";
        private const string ManifestKey = "S4ATEST_CONTENT__MANIFESTSHA256";

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(EnvironmentKey, null);
            Environment.SetEnvironmentVariable(ContentModeKey, null);
            Environment.SetEnvironmentVariable(InstalledPathKey, null);
            Environment.SetEnvironmentVariable(ManifestKey, null);
        }

        [Test]
        public void UnspecifiedContentMode_StopsBeforeAfterSceneLoadWithoutAddressablesFallback()
        {
            var initializer = new TestInitializer();
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("content:runtimeMode is required unless a verified installed revision pair is provided"));
                initializer.RunBefore();

                LogAssert.Expect(LogType.Error, new Regex("BeforeSceneLoad が失敗したため AfterSceneLoad をスキップ"));
                initializer.RunAfter();

                Assert.That(initializer.FactoryEntered, Is.False);
                Assert.That(initializer.HasSceneDirector, Is.False);
                Assert.That(initializer.HasAssetManagement, Is.False);
            }
            finally { initializer.RunCleanup(); }
        }

        [Test]
        public void OneSidedInstalledPair_StopsBeforeAfterSceneLoadWithoutAddressablesFallback()
        {
            Environment.SetEnvironmentVariable(InstalledPathKey, @"C:\osm-missing-revision");
            var initializer = new TestInitializer();
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("Installed revision path and manifest digest must be provided together"));
                initializer.RunBefore();

                LogAssert.Expect(LogType.Error, new Regex("BeforeSceneLoad が失敗したため AfterSceneLoad をスキップ"));
                initializer.RunAfter();

                Assert.That(initializer.FactoryEntered, Is.False);
                Assert.That(initializer.HasSceneDirector, Is.False);
                Assert.That(initializer.HasAssetManagement, Is.False);
            }
            finally { initializer.RunCleanup(); }
        }

        [Test]
        public void InvalidContentMode_StopsBeforeAfterSceneLoadWithoutAddressablesFallback()
        {
            Environment.SetEnvironmentVariable(ContentModeKey, "unknown-mode");
            var initializer = new TestInitializer();
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("content:runtimeMode must be addressables or directory"));
                initializer.RunBefore();

                LogAssert.Expect(LogType.Error, new Regex("BeforeSceneLoad が失敗したため AfterSceneLoad をスキップ"));
                initializer.RunAfter();

                Assert.That(initializer.FactoryEntered, Is.False);
                Assert.That(initializer.HasSceneDirector, Is.False);
                Assert.That(initializer.HasAssetManagement, Is.False);
            }
            finally { initializer.RunCleanup(); }
        }

        [Test]
        public async Task InvalidConfig_FailsBeforeDirectorAndReleasesLoadedAppAssets()
        {
            Environment.SetEnvironmentVariable(EnvironmentKey, "invalid");
            Environment.SetEnvironmentVariable(ContentModeKey, "addressables");
            var initializer = new TestInitializer();
            initializer.RunBefore();

            var backend = new FakeAssetBackend();
            var uiRoot = new GameObject("UICommon");
            uiRoot.AddComponent<UICommon>();
            backend.SetSceneRoots("test-ui", uiRoot);
            initializer.ReplaceAssetManagement(new Runtime.AssetManagement.AssetManagement(backend));
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(
                    "AfterSceneLoad failed at stage 'create-scene-factory'"));
                LogAssert.Expect(LogType.Error, new Regex(
                    "Destroy may not be called from edit mode"));
                initializer.RunAfter();
                await initializer.Failed.Task;

                Assert.That(initializer.FactoryEntered, Is.True);
                Assert.That(initializer.HasSceneDirector, Is.False);
                Assert.That(initializer.FailureStage, Is.EqualTo("create-scene-factory"));
                Assert.That(backend.LoadSceneCallCount, Is.EqualTo(1));
                Assert.That(backend.LoadAssetCallCount, Is.EqualTo(1));
                Assert.That(backend.ReleaseCallCount, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                initializer.RunCleanup();
                if (uiRoot != null) UnityEngine.Object.DestroyImmediate(uiRoot);
            }
        }

        private sealed class TestInitializer : AbstractApplicationInitializer
        {
            internal UniTaskCompletionSource Failed { get; } = new();
            internal bool FactoryEntered { get; private set; }
            internal string FailureStage { get; private set; } = string.Empty;
            internal bool HasSceneDirector => SceneDirector != null;
            internal bool HasAssetManagement => AssetManagement != null;

            internal void RunBefore() => BootstrapBeforeSceneLoad(this);
            internal void RunAfter() => BootstrapAfterSceneLoad(this);
            internal void RunCleanup() => BootstrapSubsystemRegistration(this);

            internal void ReplaceAssetManagement(IAssetManagement replacement)
            {
                AssetManagement?.ReleaseAll();
                typeof(AbstractApplicationInitializer)
                    .GetField("_assetManagement", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(this, replacement);
            }

            protected override ISceneFactory CreateSceneFactory()
            {
                FactoryEntered = true;
                var config = Config ?? throw new InvalidOperationException("Config missing.");
                const string key = "world:cellCompanionSet";
                var exists = config.ContainsKey(key);
                CellCompanionSetParser.Parse(exists ? config.GetString(key) : null, exists);
                throw new AssertionException("Invalid config unexpectedly passed strict parsing.");
            }

            protected override string GetUICommonPrefabAddress() => "test-ui";
            protected override string GetSceneResourceMapAddress() => "test-map";
            protected override ILoadingDisplay CreateLoadingDisplay() => new NoLoadingDisplay();
            protected override string GetConfigFilePath() => string.Empty;
            protected override string GetEnvironmentVariablePrefix() => "S4ATEST_";
            protected override OneStarMaker.Runtime.DebugSocketServices.DebugSocketService? CreateDebugSocketService(AppConfig config) => null;
            protected override void RegisterDefaultTelemetrySink() { }

            protected override void OnAfterSceneLoadInitializationFailed(string stage, Exception exception)
            {
                FailureStage = stage;
                Failed.TrySetResult();
            }
        }

        private sealed class NoLoadingDisplay : ILoadingDisplay
        {
            public UniTask Show(LoadingDisplayType type, System.Threading.CancellationToken ct) => UniTask.CompletedTask;
            public UniTask Hide(System.Threading.CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
