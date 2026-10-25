#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using R3;
using UnityEngine;

namespace OneStarMaker.Tests.Rendering.Vfx
{
    /// <summary>
    /// UpdateSystem に毎フレーム登録される前提で足した VfxSystem の性質を見る:
    /// instance 0 の Tick が managed heap を確保しないこと、Tick の中の 1 handle の失敗が同じ Tick の残りを止めないこと、
    /// scene event の Subscribe が失敗したら Scene 所有の Spawn を拒否する状態に戻ること。
    /// </summary>
    [TestFixture]
    public sealed class VfxSystemTickAllocationTests
    {
        private const string SceneA = "SceneA";

        private FakeVfxBackend _backend = null!;
        private GameObject _source = null!;
        private VfxSystem? _system;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeVfxBackend();
            _source = new GameObject("VfxSource");
        }

        [TearDown]
        public void TearDown()
        {
            _system?.Dispose();
            _system = null;
            if (_source != null)
            {
                UnityEngine.Object.DestroyImmediate(_source);
            }
        }

        private VfxSpawnRequest Request() => new(_source, Vector3.zero, Quaternion.identity);

        [Test]
        public void Tick_WithNoInstances_AllocatesNothing()
        {
            _system = new VfxSystem(_backend, new FakeVfxLogger());

            // 初回呼び出しの JIT 等を測定から外す。
            _system.Tick(0.016f);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                _system.Tick(0.016f);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0), "instance 0 の Tick は snapshot を確保しない");
        }

        [Test]
        public void Tick_AfterLastInstanceReleased_AllocatesNothing()
        {
            _system = new VfxSystem(_backend, new FakeVfxLogger());
            var handle = _system.Spawn(Request(), AssetOwner.App);
            handle.Stop();
            Assert.That(_system.LiveInstanceCount, Is.EqualTo(0));

            _system.Tick(0.016f);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                _system.Tick(0.016f);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        /// <summary>
        /// 1 つ目の Stopping handle の処理が backend 以外（logger）の例外で抜けても、2 つ目は同じ Tick で回収される。
        /// 1 つ目は backend の HasAliveParticles が失敗し、その失敗の記録で logger が投げる（VfxHandle の外へ漏れる）。
        /// </summary>
        [Test]
        public void Tick_FirstHandleThrowsOutsideBackend_SecondHandleStillProcessedInSameTick()
        {
            var logger = new ThrowOnceLogger();
            _system = new VfxSystem(_backend, logger, gracefulStopTimeoutSeconds: 10f);
            var first = (VfxHandle)_system.Spawn(Request(), AssetOwner.App);
            var second = (VfxHandle)_system.Spawn(Request(), AssetOwner.App);
            first.Play();
            second.Play();
            first.StopAllowingParticlesToFinish();
            second.StopAllowingParticlesToFinish();

            _backend.AliveParticles = false;
            _backend.Fault = (op, id) =>
                op == "HasAliveParticles" && id == first.Id ? new InvalidOperationException("backend fault") : null;
            logger.ThrowOnNextLog = true;

            Assert.DoesNotThrow(() => _system.Tick(0.016f));

            Assert.That(logger.Thrown, Is.True, "前提: 1 つ目の処理が logger の例外で抜けた");
            Assert.That(_backend.DestroyCountOf(second.Id), Is.EqualTo(1), "2 つ目は同じ Tick で回収される");
            Assert.That(second.IsActive, Is.False);
            Assert.That(logger.ErrorCount, Is.GreaterThanOrEqualTo(1), "Tick の失敗は Error で残る");
        }

        [Test]
        public void BindSceneLifecycle_SubscribeThrows_ExceptionReachesCallerAndSceneSpawnIsRejected()
        {
            _system = new VfxSystem(_backend, new FakeVfxLogger());
            var query = new FakeSceneQuery();
            query.Loaded.Add(SceneA);

            Assert.Throws<InvalidOperationException>(() => _system.BindSceneLifecycle(new ThrowingObservable(), query));

            var createBefore = _backend.CreateCount;
            var handle = _system.Spawn(Request(), AssetOwner.Scene(SceneA));
            Assert.That(handle.IsActive, Is.False, "購読が無いまま Scene 所有を受理しない");
            Assert.That(_backend.CreateCount, Is.EqualTo(createBefore));

            // Scene 以外の所有者は影響を受けない。
            var app = _system.Spawn(Request(), AssetOwner.App);
            Assert.That(app.IsActive, Is.True);
        }

        /// <summary>Subscribe で例外を投げる scene event 源。</summary>
        private sealed class ThrowingObservable : Observable<SceneEvent>
        {
            protected override IDisposable SubscribeCore(Observer<SceneEvent> observer)
                => throw new InvalidOperationException("subscribe failed");
        }

        /// <summary>次の 1 回だけ Log で例外を投げる logger。以後の呼び出しは数えるだけ。</summary>
        private sealed class ThrowOnceLogger : ILogger<VfxSystem>
        {
            public bool ThrowOnNextLog { get; set; }

            public bool Thrown { get; private set; }

            public int ErrorCount { get; private set; }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (ThrowOnNextLog)
                {
                    ThrowOnNextLog = false;
                    Thrown = true;
                    throw new InvalidOperationException("logger fault");
                }

                if (logLevel == LogLevel.Error)
                {
                    ErrorCount++;
                }
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        }

        /// <summary>IsSceneLoaded だけを使う偽 query。</summary>
        private sealed class FakeSceneQuery : ISceneQuery
        {
            public HashSet<string> Loaded { get; } = new(StringComparer.Ordinal);

            public SceneBase? GetLoadedScene(string identity) => null;

            public bool IsSceneLoaded(string identity) => Loaded.Contains(identity);

            public bool IsSceneStable(string identity) => Loaded.Contains(identity);
        }
    }
}
