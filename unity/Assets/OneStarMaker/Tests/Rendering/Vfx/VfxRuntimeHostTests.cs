#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem.World;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UpdateSystem.Api;
using R3;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace OneStarMaker.Tests.Rendering.Vfx
{
    /// <summary>
    /// Framework の VFX composition（<see cref="VfxRuntimeHost"/>）を、偽 backend・実 UpdateCoordinator・Subject で見る:
    /// 未構成の診断、Tick 要素の登録と LateUpdate の反映点、Gameplay Layer の作成と時間の値の出所、
    /// 解放の順序、生成の途中の失敗での後始末、
    /// Scene lifecycle への接続の失敗の扱い。initializer の中の呼び出し位置はここでは見ない（diff で確認する）。
    /// </summary>
    [TestFixture]
    public sealed class VfxRuntimeHostTests
    {
        private const string SceneA = "SceneA";

        private UpdateCoordinator _coordinator = null!;
        private RecordingLoggerFactory _loggers = null!;
        private GameObject _source = null!;
        private VfxRuntimeHost? _host;

        [SetUp]
        public void SetUp()
        {
            _coordinator = new UpdateCoordinator();
            _loggers = new RecordingLoggerFactory();
            _source = new GameObject("VfxSource");
        }

        [TearDown]
        public void TearDown()
        {
            _host?.Dispose();
            _host = null;
            if (_source != null)
            {
                UnityEngine.Object.DestroyImmediate(_source);
            }
        }

        private VfxSpawnRequest Request() => new(_source, Vector3.zero, Quaternion.identity);

        private RecordingLogger SystemLogger => _loggers.For(typeof(VfxSystem));

        private RecordingLogger HostLogger => _loggers.For(typeof(VfxRuntimeHost));

        // ═══════════════════════════════════════════
        //  未構成（backend が null）
        // ═══════════════════════════════════════════

        [Test]
        public void Create_NullBackend_LogsUnconfiguredReasonOnce_AndSpawnIsNoOp()
        {
            _host = VfxRuntimeHost.Create(null, _loggers, _coordinator);

            Assert.That(SystemLogger.CountOf(LogLevel.Warning), Is.EqualTo(1));
            Assert.That(SystemLogger.TotalCount, Is.EqualTo(1));
            Assert.That(SystemLogger.Messages[0], Does.Contain(UnconfiguredVfxBackend.Reason));

            var handle = _host.System.Spawn(Request(), AssetOwner.App);
            Assert.That(handle.IsActive, Is.False);
            Assert.That(handle.Play(), Is.False);
            Assert.That(SystemLogger.TotalCount, Is.EqualTo(1), "Spawn はログを増やさない");
        }

        [Test]
        public void Create_UnsupportedBackend_ReasonDiffersFromUnconfigured()
        {
            var backend = new FakeVfxBackend { Supported = false, UnsupportedReason = "fake-unsupported" };
            _host = VfxRuntimeHost.Create(backend, _loggers, _coordinator);

            Assert.That(SystemLogger.CountOf(LogLevel.Warning), Is.EqualTo(1));
            Assert.That(SystemLogger.Messages[0], Does.Contain("fake-unsupported"));
            Assert.That(SystemLogger.Messages[0], Does.Not.Contain(nameof(UnconfiguredVfxBackend)));
        }

        // ═══════════════════════════════════════════
        //  Tick 要素
        // ═══════════════════════════════════════════

        [Test]
        public void Create_RegistersAndActivatesTickElementOnVfxLayer()
        {
            _host = VfxRuntimeHost.Create(new FakeVfxBackend(), _loggers, _coordinator);

            Assert.That(_coordinator.TryGetHandle(_host, out _), Is.True);
            Assert.That(_host.IsTickElementActive, Is.True, "Scene 安定待ちを経由せず即時 active");
            Assert.That(
                _coordinator.GetOrCreateLayer(UpdateLayerIds.Vfx, UpdateLayerIds.VfxLayerOrder).LayerId,
                Is.EqualTo(UpdateLayerIds.Vfx),
                "layer は Vfx の order で作られている（別の order なら例外になる）");
        }

        [Test]
        public void Create_CreatesGameplayLayerWithDefaultOrder()
        {
            _host = VfxRuntimeHost.Create(new FakeVfxBackend(), _loggers, _coordinator);

            Assert.That(_coordinator.TryGetLayer(UpdateLayerIds.Gameplay, out var layer), Is.True);
            Assert.That(layer!.LayerOrder, Is.EqualTo(UpdateLayerIds.GameplayLayerOrder));
            Assert.That(UpdateLayerIds.GameplayLayerOrder, Is.LessThan(UpdateLayerIds.VfxLayerOrder), "反映点は Gameplay の後");
        }

        /// <summary>反映点は LateUpdate。Update だけでは消え切り待ちも再生状態も進まない。</summary>
        [Test]
        public void ReflectionPoint_IsLateUpdate_NotUpdate()
        {
            var backend = new FakeVfxBackend { AliveParticles = true };
            _host = VfxRuntimeHost.Create(backend, _loggers, _coordinator, gracefulStopTimeoutSeconds: 1f);
            _host.FrameTimeForTest = (0.6f, 0.6f);
            var handle = _host.System.Spawn(Request(), AssetOwner.App);
            handle.Play();
            handle.StopAllowingParticlesToFinish();

            _coordinator.RunUpdate(deltaTime: 0.6f, unscaledDeltaTime: 0.6f);
            Assert.That(backend.HasAliveParticlesCount, Is.EqualTo(0), "Update では反映しない");

            _coordinator.RunLateUpdate(deltaTime: 0.6f, unscaledDeltaTime: 0.6f);
            Assert.That(backend.HasAliveParticlesCount, Is.EqualTo(1), "LateUpdate で反映した");
            _coordinator.RunLateUpdate(deltaTime: 0.6f, unscaledDeltaTime: 0.6f);
            Assert.That(handle.IsActive, Is.False, "領域の時間の累積で timeout した");
            Assert.That(backend.DestroyCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Gameplay Layer が止まっていても Vfx Layer の反映点は呼ばれ、Pause を backend へ書く。
        /// 止まっている間は消え切り待ちの timeout が進まず、Resume の後に進む（実時間では数えない）。
        /// </summary>
        [Test]
        public void GameplayPaused_ReflectsPause_AndTimeoutDoesNotAdvanceUntilResume()
        {
            var backend = new FakeVfxBackend { AliveParticles = true };
            _host = VfxRuntimeHost.Create(backend, _loggers, _coordinator, gracefulStopTimeoutSeconds: 1f);
            _host.FrameTimeForTest = (0.6f, 0.6f);
            var gameplay = _coordinator.GetOrCreateLayer(UpdateLayerIds.Gameplay, UpdateLayerIds.GameplayLayerOrder);
            var handle = _host.System.Spawn(Request(), AssetOwner.App);
            handle.Play();
            handle.StopAllowingParticlesToFinish();

            gameplay.IsPaused = true;
            for (var i = 0; i < 5; i++)
            {
                _coordinator.RunLateUpdate(deltaTime: 0.6f, unscaledDeltaTime: 0.6f);
            }

            Assert.That(handle.IsActive, Is.True, "止まっている間は timeout しない");
            Assert.That(backend.PlaybackCalls, Has.Count.EqualTo(1), "Pause は 1 回だけ書く");
            Assert.That(backend.PlaybackCalls[0].Paused, Is.True);

            gameplay.IsPaused = false;
            _coordinator.RunLateUpdate(deltaTime: 0.6f, unscaledDeltaTime: 0.6f);
            Assert.That(backend.PlaybackCalls, Has.Count.EqualTo(2));
            Assert.That(backend.PlaybackCalls[1].Paused, Is.False, "Resume が同じ反映点で書かれる");
            Assert.That(handle.IsActive, Is.True, "Resume の後の 0.6 秒では timeout しない");

            _coordinator.RunLateUpdate(deltaTime: 0.6f, unscaledDeltaTime: 0.6f);
            Assert.That(handle.IsActive, Is.False, "Resume の後の累積で timeout した");
        }

        /// <summary>時間の値は Layer の状態を Layer を作らずに読む（未作成の Layer は「止まっていない、倍率 1」）。</summary>
        [Test]
        public void TimeSource_UnknownLayer_IsNotCreated()
        {
            _host = VfxRuntimeHost.Create(new FakeVfxBackend(), _loggers, _coordinator);
            var source = (IVfxTimeSource)_host;

            Assert.That(source.TryGetLayerState("NotCreated", out var paused, out var scale), Is.False);
            Assert.That(paused, Is.False);
            Assert.That(scale, Is.EqualTo(1f));
            Assert.That(_coordinator.TryGetLayer("NotCreated", out _), Is.False, "読むだけで作らない");
        }

        // ═══════════════════════════════════════════
        //  Dispose の順序
        // ═══════════════════════════════════════════

        [Test]
        public void Dispose_DeactivatesUnregistersDisposesSystemThenBackend_AndSecondDisposeIsNoOp()
        {
            var backend = new DisposableBackend();
            var host = VfxRuntimeHost.Create(backend, _loggers, _coordinator);
            _host = host;
            var handle = host.System.Spawn(Request(), AssetOwner.App);
            Assert.That(handle.IsActive, Is.True);

            var order = new List<string>();
            var deactivatedAtDestroy = false;
            var unregisteredAtDestroy = false;
            backend.Inner.OnDestroy = destroyedId =>
            {
                deactivatedAtDestroy = !host.IsTickElementActive;
                // 登録解除の要求は構造変更フェーズで適用される。ここで適用して、要求済みかを確かめる。
                _coordinator.ApplyStructuralChanges();
                unregisteredAtDestroy = !_coordinator.TryGetHandle(host, out _);
                order.Add("VfxSystem.Dispose -> backend.Destroy");
            };
            backend.OnDispose = () => order.Add("backend.Dispose");

            host.Dispose();
            host.Dispose();

            Assert.That(order, Is.EqualTo(new[] { "VfxSystem.Dispose -> backend.Destroy", "backend.Dispose" }));
            Assert.That(deactivatedAtDestroy, Is.True, "要素の無効化は VfxSystem の Dispose より前");
            Assert.That(unregisteredAtDestroy, Is.True, "登録解除は VfxSystem の Dispose より前");
            Assert.That(backend.DisposeCount, Is.EqualTo(1), "2 回目の Dispose は何もしない");
            Assert.That(handle.IsActive, Is.False);
        }

        // ═══════════════════════════════════════════
        //  生成の途中の失敗
        // ═══════════════════════════════════════════

        /// <summary>VfxSystem の生成より前（logger の生成）で失敗: 例外が届き、backend は Dispose される。</summary>
        [Test]
        public void Create_LoggerFactoryThrows_DisposesBackendAndRethrows()
        {
            var backend = new DisposableBackend();

            Assert.Throws<InvalidOperationException>(
                () => VfxRuntimeHost.Create(backend, new ThrowingLoggerFactory(), _coordinator));

            Assert.That(backend.DisposeCount, Is.EqualTo(1));
            Assert.That(backend.Inner.QueryCapabilityCount, Is.EqualTo(0), "前提: VfxSystem の生成前に失敗した");
        }

        /// <summary>
        /// VfxSystem の生成より後（Tick 要素の登録）で失敗: layer Vfx が別の order で既にあると RegisterElement が投げる。
        /// 例外が届き、backend は Dispose され、要素は登録されていない。
        /// </summary>
        [Test]
        public void Create_RegisterElementThrowsOnLayerOrderConflict_DisposesBackendAndRethrows()
        {
            _coordinator.GetOrCreateLayer(UpdateLayerIds.Vfx, UpdateLayerIds.VfxLayerOrder + 1);
            var backend = new DisposableBackend();

            Assert.Throws<InvalidOperationException>(() => VfxRuntimeHost.Create(backend, _loggers, _coordinator));

            Assert.That(backend.Inner.QueryCapabilityCount, Is.EqualTo(1), "前提: VfxSystem を作った後に失敗した");
            Assert.That(backend.DisposeCount, Is.EqualTo(1));
            Assert.That(backend.Inner.TotalCallCount, Is.EqualTo(1), "backend は能力判定以外に呼ばれていない");
        }

        // ═══════════════════════════════════════════
        //  Scene lifecycle への接続
        // ═══════════════════════════════════════════

        [Test]
        public void BindSceneLifecycle_Succeeds_SceneOwnedSpawnAccepted()
        {
            _host = VfxRuntimeHost.Create(new FakeVfxBackend(), _loggers, _coordinator);
            var query = new FakeSceneQuery();
            query.Loaded.Add(SceneA);
            using var events = new Subject<SceneEvent>();

            _host.BindSceneLifecycle(events, query);

            Assert.That(_host.IsSceneLifecycleBound, Is.True);
            Assert.That(_host.System.Spawn(Request(), AssetOwner.Scene(SceneA)).IsActive, Is.True);
        }

        [Test]
        public void BindSceneLifecycle_SubscribeThrows_LogsErrorOnceAndDoesNotThrow()
        {
            _host = VfxRuntimeHost.Create(new FakeVfxBackend(), _loggers, _coordinator);
            var query = new FakeSceneQuery();
            query.Loaded.Add(SceneA);

            Assert.DoesNotThrow(() => _host.BindSceneLifecycle(new ThrowingObservable(), query));

            Assert.That(HostLogger.CountOf(LogLevel.Error), Is.EqualTo(1));
            Assert.That(_host.IsSceneLifecycleBound, Is.False);
            Assert.That(_host.System.Spawn(Request(), AssetOwner.Scene(SceneA)).IsActive, Is.False,
                "接続に失敗したら Scene 所有の Spawn は拒否される");
            Assert.That(_host.System.Spawn(Request(), AssetOwner.App).IsActive, Is.True, "App 所有は使い続けられる");
        }

        // ═══════════════════════════════════════════
        //  偽物
        // ═══════════════════════════════════════════

        /// <summary>Dispose を数える backend。他の操作は <see cref="FakeVfxBackend"/> へ委ねる。</summary>
        private sealed class DisposableBackend : IVfxBackend, IDisposable
        {
            public FakeVfxBackend Inner { get; } = new();

            public Action? OnDispose { get; set; }

            public int DisposeCount { get; private set; }

            public bool QueryCapability(out string? reason) => Inner.QueryCapability(out reason);

            public VfxInstanceId Create(in VfxSpawnRequest request) => Inner.Create(request);

            public bool Play(VfxInstanceId id) => Inner.Play(id);

            public void SetPlayback(VfxInstanceId id, bool paused, float rate) => Inner.SetPlayback(id, paused, rate);

            public VfxInstanceId Adopt(UnityEngine.Object placed) => Inner.Adopt(placed);

            public void Release(VfxInstanceId id) => Inner.Release(id);

            public void StopEmitting(VfxInstanceId id) => Inner.StopEmitting(id);

            public bool HasAliveParticles(VfxInstanceId id) => Inner.HasAliveParticles(id);

            public bool SetFloat(VfxInstanceId id, string name, float value) => Inner.SetFloat(id, name, value);

            public bool SetInt(VfxInstanceId id, string name, int value) => Inner.SetInt(id, name, value);

            public bool SetBool(VfxInstanceId id, string name, bool value) => Inner.SetBool(id, name, value);

            public bool SetVector3(VfxInstanceId id, string name, Vector3 value) => Inner.SetVector3(id, name, value);

            public bool SetVector4(VfxInstanceId id, string name, Vector4 value) => Inner.SetVector4(id, name, value);

            public void Destroy(VfxInstanceId id) => Inner.Destroy(id);

            public void Dispose()
            {
                DisposeCount++;
                OnDispose?.Invoke();
            }
        }

        /// <summary>category（型の完全名）ごとに記録する logger を返す factory。</summary>
        private sealed class RecordingLoggerFactory : ILoggerFactory
        {
            private readonly Dictionary<string, RecordingLogger> _loggers = new(StringComparer.Ordinal);

            public RecordingLogger For(Type type) => Get(type.FullName!);

            public ILogger CreateLogger(string categoryName) => Get(categoryName);

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public void Dispose()
            {
            }

            private RecordingLogger Get(string category)
            {
                if (!_loggers.TryGetValue(category, out var logger))
                {
                    logger = new RecordingLogger();
                    _loggers.Add(category, logger);
                }

                return logger;
            }
        }

        private sealed class RecordingLogger : ILogger
        {
            private readonly Dictionary<LogLevel, int> _counts = new();

            public List<string> Messages { get; } = new();

            public int TotalCount => Messages.Count;

            public int CountOf(LogLevel level) => _counts.TryGetValue(level, out var count) ? count : 0;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _counts[logLevel] = CountOf(logLevel) + 1;
                Messages.Add(logLevel + ": " + formatter(state, exception));
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        }

        private sealed class ThrowingLoggerFactory : ILoggerFactory
        {
            public ILogger CreateLogger(string categoryName) => throw new InvalidOperationException("logger factory fault");

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class ThrowingObservable : Observable<SceneEvent>
        {
            protected override IDisposable SubscribeCore(Observer<SceneEvent> observer)
                => throw new InvalidOperationException("subscribe failed");
        }

        private sealed class FakeSceneQuery : ISceneQuery
        {
            public HashSet<string> Loaded { get; } = new(StringComparer.Ordinal);

            public SceneBase? GetLoadedScene(string identity) => null;

            public bool IsSceneLoaded(string identity) => Loaded.Contains(identity);

            public bool IsSceneStable(string identity) => Loaded.Contains(identity);
        }
    }
}
