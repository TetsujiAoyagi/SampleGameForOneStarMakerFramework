#nullable enable

using System;
using System.Reflection;
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

namespace OneStarMaker.Tests.Bootstrap
{
    [TestFixture]
    public sealed class InvalidCompanionConfigStartupTests
    {
        private const string EnvironmentKey = "S4ATEST_WORLD__CELLCOMPANIONSET";

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(EnvironmentKey, null);
        }

        [Test]
        public async UniTask InvalidConfig_FailsBeforeDirectorAndReleasesLoadedAppAssets()
        {
            Environment.SetEnvironmentVariable(EnvironmentKey, "invalid");
            var initializer = new TestInitializer();
            initializer.RunBefore();

            var backend = new FakeAssetBackend();
            var uiRoot = new GameObject("UICommon");
            uiRoot.AddComponent<UICommon>();
            backend.SetSceneRoots("test-ui", uiRoot);
            initializer.ReplaceAssetManagement(new Runtime.AssetManagement.AssetManagement(backend));
            try
            {
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
