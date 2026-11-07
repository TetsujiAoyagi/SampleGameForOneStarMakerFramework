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
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Runtime.UpdateSystem.Api;
using OneStarMaker.Tests.AssetManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Bootstrap
{
    /// <summary>
    /// initializer の失敗経路のうち、既存の起動テストが通らないものを派生 harness で確かめる。
    /// <list type="bullet">
    /// <item><description>BeforeSceneLoad の途中の hook が投げたとき、作り済みの常駐サービスが破棄され、それまでの hook は作り済みのサービスを accessor で読めていたこと。</description></item>
    /// <item><description>サービスが揃った後（telemetry sink の登録）で投げたとき、VFX を含む全サービスとテスト用の static が戻ること。</description></item>
    /// <item><description>UICommon の root に component が無いとき、load-ui-common で失敗し、ReleaseAll がロードした root を破棄しにいくこと。</description></item>
    /// </list>
    /// config file は使わず、環境変数で Addressables モードを指定する（Content Directory の判定を通さない）。
    /// </summary>
    [TestFixture]
    public sealed class BeforeSceneLoadServiceStagingTests
    {
        private const string Prefix = "BSSTEST_";
        private const string ContentModeKey = Prefix + "CONTENT__RUNTIMEMODE";

        private StagingInitializer? _initializer;

        [SetUp]
        public void SetUp()
        {
            Environment.SetEnvironmentVariable(ContentModeKey, "addressables");
        }

        [TearDown]
        public void TearDown()
        {
            // ReleaseAll を通して PlayerLoop の UpdateSystemHost、ActiveVfxRuntimeHost、AppTelemetry の sink を戻す。
            if (_initializer != null)
                _initializer.RunCleanup();
            _initializer = null;
            Environment.SetEnvironmentVariable(ContentModeKey, null);
        }

        [Test]
        public void HookFailureMidway_ReleasesServicesBuiltBeforeIt()
        {
            _initializer = new StagingInitializer { ThrowFromVfxBackend = true };

            LogAssert.Expect(LogType.Exception, new Regex("staging: CreateVfxBackend failed"));
            _initializer.RunBefore();

            Assert.That(_initializer.VfxHookSawUpdateCoordinator, Is.True, "UpdateSystemHost が VFX の hook より前に見えていない");
            Assert.That(_initializer.VfxHookSawLoggerFactory, Is.True, "LoggerFactory が VFX の hook より前に見えていない");
            Assert.That(_initializer.CurrentUpdateCoordinator, Is.Null);
            Assert.That(_initializer.CurrentLoggerFactory, Is.Null);
            Assert.That(UpdateSystemRuntime.Coordinator, Is.Null, "作り済みの UpdateSystemHost が PlayerLoop に残っている");
        }

        [Test]
        public void HookFailureAfterAllServices_ReleasesEveryServiceAndTheVfxStatic()
        {
            _initializer = new StagingInitializer { ThrowFromTelemetrySink = true };

            LogAssert.Expect(LogType.Exception, new Regex("staging: RegisterDefaultTelemetrySink failed"));
            _initializer.RunBefore();

            Assert.That(_initializer.SinkHookSawLoggerFactory, Is.True);
            Assert.That(_initializer.SinkHookSawVfx, Is.True);
            Assert.That(_initializer.SinkHookSawActiveVfxHost, Is.True);
            Assert.That(_initializer.CurrentVfx, Is.Null);
            Assert.That(_initializer.CurrentLoggerFactory, Is.Null);
            Assert.That(_initializer.CurrentUpdateCoordinator, Is.Null);
            Assert.That(AbstractApplicationInitializer.ActiveVfxRuntimeHost, Is.Null);
            Assert.That(UpdateSystemRuntime.Coordinator, Is.Null);
        }

        [Test]
        public async Task UiCommonRootWithoutComponent_FailsAtLoadUiCommonAndDestroysTheOwnedRoot()
        {
            _initializer = new StagingInitializer();
            _initializer.RunBefore();

            var backend = new FakeAssetBackend();
            var uiRoot = new GameObject("UICommon");
            backend.SetSceneRoots(StagingInitializer.UiCommonAddress, uiRoot);
            _initializer.ReplaceAssetManagement(new Runtime.AssetManagement.AssetManagement(backend));
            try
            {
                LogAssert.Expect(LogType.Error, new Regex("AfterSceneLoad failed at stage 'load-ui-common'"));
                // loader が記録した root を ReleaseAll が Destroy する。EditMode では Unity がこのエラーを出す。
                LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
                _initializer.RunAfter();
                await _initializer.Failed.Task;

                Assert.That(_initializer.FailureStage, Is.EqualTo("load-ui-common"));
                Assert.That(_initializer.FailureMessage,
                    Is.EqualTo("UICommon component not found on prefab: " + StagingInitializer.UiCommonAddress));
                Assert.That(backend.LoadSceneCallCount, Is.EqualTo(1));
                Assert.That(_initializer.CurrentUpdateCoordinator, Is.Null);
            }
            finally
            {
                if (uiRoot != null) UnityEngine.Object.DestroyImmediate(uiRoot);
            }
        }

        private sealed class StagingInitializer : AbstractApplicationInitializer
        {
            internal const string UiCommonAddress = "staging-ui";

            internal bool ThrowFromVfxBackend { get; set; }
            internal bool ThrowFromTelemetrySink { get; set; }

            internal bool VfxHookSawUpdateCoordinator { get; private set; }
            internal bool VfxHookSawLoggerFactory { get; private set; }
            internal bool SinkHookSawLoggerFactory { get; private set; }
            internal bool SinkHookSawVfx { get; private set; }
            internal bool SinkHookSawActiveVfxHost { get; private set; }

            internal UniTaskCompletionSource Failed { get; } = new();
            internal string FailureStage { get; private set; } = string.Empty;
            internal string FailureMessage { get; private set; } = string.Empty;

            internal object? CurrentUpdateCoordinator => UpdateCoordinator;
            internal object? CurrentLoggerFactory => LoggerFactory;
            internal object? CurrentVfx => Vfx;

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

            protected override IVfxBackend? CreateVfxBackend()
            {
                VfxHookSawUpdateCoordinator = UpdateCoordinator != null;
                VfxHookSawLoggerFactory = LoggerFactory != null;
                if (ThrowFromVfxBackend)
                    throw new InvalidOperationException("staging: CreateVfxBackend failed");
                return null;
            }

            protected override void RegisterDefaultTelemetrySink()
            {
                // base は JSON の sink を登録するが、テストでは file を作らないよう呼ばない。
                SinkHookSawLoggerFactory = LoggerFactory != null;
                SinkHookSawVfx = Vfx != null;
                SinkHookSawActiveVfxHost = ActiveVfxRuntimeHost != null;
                if (ThrowFromTelemetrySink)
                    throw new InvalidOperationException("staging: RegisterDefaultTelemetrySink failed");
            }

            protected override void OnAfterSceneLoadInitializationFailed(string stage, Exception exception)
            {
                FailureStage = stage;
                FailureMessage = exception.Message;
                Failed.TrySetResult();
            }

            // AfterSceneLoad は load-ui-common で止まるので、Scene の生成まで進まない。
            protected override ISceneFactory CreateSceneFactory() =>
                throw new NotSupportedException("staging: the scene factory is not reached.");

            protected override ILoadingDisplay CreateLoadingDisplay() =>
                throw new NotSupportedException("staging: the loading display is not reached.");

            protected override string GetUICommonPrefabAddress() => UiCommonAddress;
            protected override string GetSceneResourceMapAddress() => "staging-map";
            protected override string GetConfigFilePath() => string.Empty;
            protected override string GetEnvironmentVariablePrefix() => Prefix;
            protected override Runtime.DebugSocketServices.DebugSocketService? CreateDebugSocketService(
                AppConfig config) => null;
        }
    }
}
