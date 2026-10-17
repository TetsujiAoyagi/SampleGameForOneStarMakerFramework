#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using R3;
using UnityEngine;

namespace OneStarMaker.Tests.Rendering.Vfx
{
    /// <summary>
    /// VfxSystem の受理規則・所有者の終端・再入・非対応・Dispose を、Subject と偽 ISceneQuery で駆動して見る。
    /// SceneDirector は使わない（実 SceneDirector での順序は VfxSystemSceneDirectorTests）。
    /// </summary>
    [TestFixture]
    public sealed class VfxSystemOwnerTests
    {
        private const string SceneA = "SceneA";
        private const string SceneB = "SceneB";

        private FakeVfxBackend _backend = null!;
        private FakeVfxLogger _logger = null!;
        private VfxSystem _system = null!;
        private Subject<SceneEvent> _events = null!;
        private FakeSceneQuery _query = null!;
        private GameObject _source = null!;
        private readonly List<GameObject> _createdObjects = new();
        private readonly List<VfxSystem> _extraSystems = new();

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeVfxBackend();
            _logger = new FakeVfxLogger();
            _system = new VfxSystem(_backend, _logger, gracefulStopTimeoutSeconds: 1f);
            _events = new Subject<SceneEvent>();
            _query = new FakeSceneQuery();
            _query.Loaded.Add(SceneA);
            _query.Loaded.Add(SceneB);
            _source = CreateObject("VfxSource");
        }

        [TearDown]
        public void TearDown()
        {
            _system.Dispose();
            foreach (var system in _extraSystems)
            {
                system.Dispose();
            }

            _extraSystems.Clear();
            _events.Dispose();
            foreach (var go in _createdObjects)
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            _createdObjects.Clear();
        }

        private GameObject CreateObject(string name)
        {
            var go = new GameObject(name);
            _createdObjects.Add(go);
            return go;
        }

        private VfxSpawnRequest Request() => new(_source, Vector3.zero, Quaternion.identity);

        private void BindLifecycle() => _system.BindSceneLifecycle(_events, _query);

        private void Emit(SceneEventType type, string identity, SceneState state)
            => _events.OnNext(new SceneEvent(type, identity, state));

        private void EmitPreUnloading(string identity)
            => Emit(SceneEventType.StateChanged, identity, SceneState.PreUnloading);

        private static ulong GameObjectIdOf(GameObject go) => EntityId.ToULong(go.GetEntityId());

        private VfxHandle SpawnLive(AssetOwner owner)
        {
            var handle = _system.Spawn(Request(), owner);
            Assert.That(handle, Is.InstanceOf<VfxHandle>(), "受理されるべき起動が拒否された");
            return (VfxHandle)handle;
        }

        private void AssertRejected(IVfxHandle handle, int createCountBefore)
        {
            Assert.That(_backend.CreateCount, Is.EqualTo(createCountBefore));
            Assert.That(handle.IsActive, Is.False);
            Assert.DoesNotThrow(() =>
            {
                handle.Play();
                handle.SetFloat("f", 1f);
                handle.StopAllowingParticlesToFinish();
                handle.Stop();
                handle.Dispose();
            });
        }

        // ═══════════════════════════════════════════
        //  AC1 Spawn から即時 Stop
        // ═══════════════════════════════════════════

        [Test]
        public void SpawnPlayStopStopDispose_CreatesPlaysAndDestroysOnce()
        {
            var handle = _system.Spawn(Request(), AssetOwner.App);

            Assert.That(handle.Play(), Is.True);
            handle.Stop();
            handle.Stop();
            handle.Dispose();

            Assert.That(_backend.CreateCount, Is.EqualTo(1));
            Assert.That(_backend.PlayCount, Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(handle.IsActive, Is.False);
            Assert.That(_system.LiveInstanceCount, Is.EqualTo(0));
        }

        // ═══════════════════════════════════════════
        //  AC2 / AC3 Tick と ctor の timeout
        // ═══════════════════════════════════════════

        [Test]
        public void Tick_CompletesGracefulStop_AndTimesOut()
        {
            var a = SpawnLive(AssetOwner.App);
            a.Play();
            a.StopAllowingParticlesToFinish();

            _system.Tick(0.5f);
            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(0));

            _system.Tick(0.5f);
            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));

            _system.Tick(0.5f);
            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(1));
        }

        [Test]
        public void Tick_DoesNotQueryNonStoppingHandles()
        {
            var a = SpawnLive(AssetOwner.App);
            a.Play();

            _system.Tick(0.1f);

            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(0));
            Assert.That(a.IsActive, Is.True);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Ctor_RejectsNonFiniteOrNonPositiveTimeout(float timeout)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new VfxSystem(new FakeVfxBackend(), new FakeVfxLogger(), timeout));
        }

        [Test]
        public void Ctor_AcceptsLargeFiniteTimeout()
        {
            var system = new VfxSystem(new FakeVfxBackend(), new FakeVfxLogger(), float.MaxValue);
            _extraSystems.Add(system);
        }

        [Test]
        public void Ctor_And_BindSceneLifecycle_RejectNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new VfxSystem(null!, new FakeVfxLogger()));
            Assert.Throws<ArgumentNullException>(() => new VfxSystem(new FakeVfxBackend(), null!));
            Assert.Throws<ArgumentNullException>(() => _system.BindSceneLifecycle(null!, _query));
            Assert.Throws<ArgumentNullException>(() => _system.BindSceneLifecycle(_events, null!));
        }

        // ═══════════════════════════════════════════
        //  AC4 所有者の終端と回収中の再入
        // ═══════════════════════════════════════════

        [Test]
        public void Stopping_OwnerSceneTerminates_DestroysOnce_AndTickDoesNotQuery()
        {
            BindLifecycle();
            var a = SpawnLive(AssetOwner.Scene(SceneA));
            a.Play();
            a.StopAllowingParticlesToFinish();

            EmitPreUnloading(SceneA);
            _system.Tick(0.1f);

            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(0));
        }

        [Test]
        public void DoubleTermination_PreUnloadingThenRemoved_DestroysOnce()
        {
            BindLifecycle();
            var a = SpawnLive(AssetOwner.Scene(SceneA));
            a.Play();

            EmitPreUnloading(SceneA);
            Emit(SceneEventType.Removed, SceneA, SceneState.AfterUnloading);

            Assert.That(_backend.DestroyCountOf(a.Id), Is.EqualTo(1));
        }

        [Test]
        public void Reentry_DuringSceneEventRelease_IsSafe()
        {
            BindLifecycle();
            var other = CreateObject("Other");
            var otherOwner = AssetOwner.Bind(other);
            var a1 = SpawnLive(AssetOwner.Scene(SceneA));
            var a2 = SpawnLive(AssetOwner.Scene(SceneA));
            var b = SpawnLive(otherOwner);
            var reentered = InstallReentryOnFirstDestroy(other, AssetOwner.Scene(SceneA));

            Assert.DoesNotThrow(() => EmitPreUnloading(SceneA));

            AssertEachDestroyedOnce(a1, a2, b);
            Assert.That(reentered.CreateCountDelta, Is.EqualTo(0));
            Assert.That(reentered.Handle!.IsActive, Is.False);
            Assert.That(_logger.ErrorCount, Is.EqualTo(0));
        }

        [Test]
        public void Reentry_DuringDispose_IsSafe()
        {
            BindLifecycle();
            var other = CreateObject("Other");
            var a1 = SpawnLive(AssetOwner.Scene(SceneA));
            var a2 = SpawnLive(AssetOwner.Scene(SceneA));
            var b = SpawnLive(AssetOwner.Bind(other));
            var reentered = InstallReentryOnFirstDestroy(other, AssetOwner.Scene(SceneA));

            Assert.DoesNotThrow(() => _system.Dispose());

            AssertEachDestroyedOnce(a1, a2, b);
            Assert.That(reentered.CreateCountDelta, Is.EqualTo(0));
            Assert.That(reentered.Handle!.IsActive, Is.False);
        }

        /// <summary>
        /// Tick は所有者の終端ではないので、生きている Scene への再入 Spawn は受理される（§2.3 の受理規則）。
        /// 破棄済みの Bind 所有者への再入 Spawn は拒否される。どちらも Tick の列挙を壊さない。
        /// </summary>
        [Test]
        public void Reentry_DuringTickRelease_IsSafe()
        {
            BindLifecycle();
            var other = CreateObject("Other");
            var otherOwner = AssetOwner.Bind(other);
            var a = SpawnLive(AssetOwner.Scene(SceneA));
            var b = SpawnLive(otherOwner);
            a.Play();
            a.StopAllowingParticlesToFinish();
            _backend.AliveParticles = false;

            IVfxHandle? reenteredBind = null;
            IVfxHandle? reenteredScene = null;
            var createBefore = 0;
            var createAfterBind = 0;
            _backend.OnDestroy = id =>
            {
                if (id != a.Id)
                {
                    return;
                }

                _backend.OnDestroy = null;
                var otherId = GameObjectIdOf(other);
                UnityEngine.Object.DestroyImmediate(other);
                _system.NotifyOwnerGameObjectDestroyed(otherId);
                createBefore = _backend.CreateCount;
                reenteredBind = _system.Spawn(Request(), otherOwner);
                createAfterBind = _backend.CreateCount;
                reenteredScene = _system.Spawn(Request(), AssetOwner.Scene(SceneA));
            };

            Assert.DoesNotThrow(() => _system.Tick(0.1f));

            AssertEachDestroyedOnce(a, b);
            Assert.That(createAfterBind - createBefore, Is.EqualTo(0), "破棄済み Bind 所有者への再入 Spawn は Create しない");
            Assert.That(reenteredBind!.IsActive, Is.False);
            Assert.That(reenteredScene!.IsActive, Is.True, "Tick 中でも生きている Scene の所有は受理する");
            Assert.That(((VfxHandle)reenteredScene).State, Is.EqualTo(VfxHandle.HandleState.Created));
        }

        private sealed class ReentryRecord
        {
            public int CreateCountDelta;
            public IVfxHandle? Handle;
        }

        /// <summary>最初の Destroy の中で、別の Bind 所有者の破棄通知と、同じ Scene 所有の Spawn を再入させる。</summary>
        private ReentryRecord InstallReentryOnFirstDestroy(GameObject otherBound, AssetOwner sceneOwner)
        {
            var record = new ReentryRecord();
            var otherId = GameObjectIdOf(otherBound);
            _backend.OnDestroy = _ =>
            {
                _backend.OnDestroy = null;
                _system.NotifyOwnerGameObjectDestroyed(otherId);
                var before = _backend.CreateCount;
                record.Handle = _system.Spawn(Request(), sceneOwner);
                record.CreateCountDelta = _backend.CreateCount - before;
            };
            return record;
        }

        private void AssertEachDestroyedOnce(params VfxHandle[] handles)
        {
            foreach (var handle in handles)
            {
                Assert.That(_backend.DestroyCountOf(handle.Id), Is.EqualTo(1), $"id={handle.Id}");
                Assert.That(handle.IsActive, Is.False);
            }
        }

        // ═══════════════════════════════════════════
        //  AC7 不正・終端済みの所有者と入力
        // ═══════════════════════════════════════════

        [Test]
        public void DefaultOwner_IsRejected()
        {
            var handle = _system.Spawn(Request(), default);

            AssertRejected(handle, 0);
            Assert.That(_system.LiveInstanceCount, Is.EqualTo(0));
        }

        [Test]
        public void TerminatingScene_IsRejected_EvenWhileQueryStillReportsLoaded()
        {
            BindLifecycle();
            EmitPreUnloading(SceneA);

            Assert.That(_query.IsSceneLoaded(SceneA), Is.True);
            AssertRejected(_system.Spawn(Request(), AssetOwner.Scene(SceneA)), 0);
        }

        [Test]
        public void SpawnReenteredFromPreUnloadingDestroy_IsRejected()
        {
            BindLifecycle();
            var a = SpawnLive(AssetOwner.Scene(SceneA));
            var createBefore = _backend.CreateCount;
            IVfxHandle? reentered = null;
            _backend.OnDestroy = _ =>
            {
                _backend.OnDestroy = null;
                reentered = _system.Spawn(Request(), AssetOwner.Scene(SceneA));
            };

            EmitPreUnloading(SceneA);

            Assert.That(_backend.CreateCount, Is.EqualTo(createBefore));
            Assert.That(reentered!.IsActive, Is.False);
            Assert.That(a.IsActive, Is.False);
        }

        [Test]
        public void AfterRemoved_StillRejected_UntilPreLoading_AndOldHandleDoesNotRevive()
        {
            BindLifecycle();
            var old = SpawnLive(AssetOwner.Scene(SceneA));
            EmitPreUnloading(SceneA);
            Emit(SceneEventType.Removed, SceneA, SceneState.AfterUnloading);

            AssertRejected(_system.Spawn(Request(), AssetOwner.Scene(SceneA)), 1);

            Emit(SceneEventType.StateChanged, SceneA, SceneState.PreLoading);
            var reloaded = _system.Spawn(Request(), AssetOwner.Scene(SceneA));

            Assert.That(_backend.CreateCount, Is.EqualTo(2));
            Assert.That(reloaded.IsActive, Is.True);
            Assert.That(old.IsActive, Is.False);
        }

        [Test]
        public void TerminationOfOneScene_DoesNotAffectAnother()
        {
            BindLifecycle();
            var a = SpawnLive(AssetOwner.Scene(SceneA));
            var b = SpawnLive(AssetOwner.Scene(SceneB));

            EmitPreUnloading(SceneA);

            Assert.That(a.IsActive, Is.False);
            Assert.That(b.IsActive, Is.True);
            Assert.That(_system.Spawn(Request(), AssetOwner.Scene(SceneB)).IsActive, Is.True);
        }

        [Test]
        public void DestroyedBindObject_IsRejected()
        {
            var go = CreateObject("Bound");
            var owner = AssetOwner.Bind(go);
            UnityEngine.Object.DestroyImmediate(go);

            AssertRejected(_system.Spawn(Request(), owner), 0);
        }

        [Test]
        public void BindOwnerWithoutObject_IsRejected()
        {
            var go = CreateObject("Bound");
            var owner = AssetOwner.FromGameObjectId(GameObjectIdOf(go));

            AssertRejected(_system.Spawn(Request(), owner), 0);
        }

        [Test]
        public void UnloadedScene_IsRejected()
        {
            BindLifecycle();

            AssertRejected(_system.Spawn(Request(), AssetOwner.Scene("NotLoaded")), 0);
        }

        [Test]
        public void SceneOwner_WithoutLifecycle_IsRejected()
        {
            AssertRejected(_system.Spawn(Request(), AssetOwner.Scene(SceneA)), 0);
        }

        [Test]
        public void SceneOwner_AfterCompleted_IsRejected()
        {
            BindLifecycle();
            _events.OnCompleted();

            AssertRejected(_system.Spawn(Request(), AssetOwner.Scene(SceneA)), 0);
            Assert.That(_system.Spawn(Request(), AssetOwner.App).IsActive, Is.True, "Scene 以外の所有は影響を受けない");
        }

        [Test]
        public void NullOrDestroyedSource_IsRejected()
        {
            AssertRejected(_system.Spawn(new VfxSpawnRequest(null!, Vector3.zero, Quaternion.identity), AssetOwner.App), 0);
            AssertRejected(_system.Spawn(default, AssetOwner.App), 0);

            var destroyedSource = CreateObject("DestroyedSource");
            UnityEngine.Object.DestroyImmediate(destroyedSource);
            AssertRejected(
                _system.Spawn(new VfxSpawnRequest(destroyedSource, Vector3.zero, Quaternion.identity), AssetOwner.App), 0);
        }

        [Test]
        public void DestroyedParent_IsRejected_AndLiveParentIsAccepted()
        {
            var parentGo = CreateObject("Parent");
            var parent = parentGo.transform;
            Assert.That(_system.Spawn(new VfxSpawnRequest(_source, Vector3.zero, Quaternion.identity, parent), AssetOwner.App).IsActive,
                Is.True);

            UnityEngine.Object.DestroyImmediate(parentGo);

            AssertRejected(
                _system.Spawn(new VfxSpawnRequest(_source, Vector3.zero, Quaternion.identity, parent), AssetOwner.App), 1);
        }

        [Test]
        public void AfterDispose_IsRejected()
        {
            _system.Dispose();

            AssertRejected(_system.Spawn(Request(), AssetOwner.App), 0);
        }

        // ═══════════════════════════════════════════
        //  AC8 Bind 所有者
        // ═══════════════════════════════════════════

        [Test]
        public void BindOwner_DestroyNotification_ReleasesSynchronously()
        {
            var go = CreateObject("Bound");
            var a = SpawnLive(AssetOwner.Bind(go));

            _system.NotifyOwnerGameObjectDestroyed(GameObjectIdOf(go));

            Assert.That(_backend.DestroyCountOf(a.Id), Is.EqualTo(1));
            Assert.That(a.IsActive, Is.False);
        }

        [Test]
        public void BindOwner_DestroyedWithoutNotification_ReleasedOnNextTick()
        {
            var go = CreateObject("Bound");
            var a = SpawnLive(AssetOwner.Bind(go));

            UnityEngine.Object.DestroyImmediate(go);
            Assert.That(_backend.DestroyCount, Is.EqualTo(0), "Tick 前の未回収は仕様内");

            _system.Tick(0f);

            Assert.That(_backend.DestroyCountOf(a.Id), Is.EqualTo(1));
        }

        [Test]
        public void BindOwner_NotifierIsAttachedOncePerSystem_AndReachesEachSystem()
        {
            var go = CreateObject("Bound");
            var otherBackend = new FakeVfxBackend();
            var otherSystem = new VfxSystem(otherBackend, new FakeVfxLogger());
            _extraSystems.Add(otherSystem);

            SpawnLive(AssetOwner.Bind(go));
            SpawnLive(AssetOwner.Bind(go));
            otherSystem.Spawn(Request(), AssetOwner.Bind(go));

            var notifiers = go.GetComponents<VfxOwnerDestroyNotifier>();
            Assert.That(notifiers, Has.Length.EqualTo(2), "system ごとに1つ");

            // EditMode では OnDestroy が発火しないので、notifier の OnDestroy を直接呼んで経路を確かめる。
            var onDestroy = typeof(VfxOwnerDestroyNotifier).GetMethod(
                "OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var notifier in notifiers)
            {
                onDestroy.Invoke(notifier, null);
            }

            Assert.That(_backend.DestroyCount, Is.EqualTo(2));
            Assert.That(otherBackend.DestroyCount, Is.EqualTo(1));
        }

        // ═══════════════════════════════════════════
        //  AC9 Spawn 側の失敗
        // ═══════════════════════════════════════════

        [Test]
        public void CreateThrows_ReturnsNullHandle_WithOneError_AndOthersKeepWorking()
        {
            var survivor = SpawnLive(AssetOwner.App);
            _backend.Fault = (op, _) => op == "Create" ? new InvalidOperationException(op) : null;

            IVfxHandle handle = null!;
            Assert.DoesNotThrow(() => handle = _system.Spawn(Request(), AssetOwner.App));
            _backend.Fault = null;

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_backend.DestroyCount, Is.EqualTo(0));
            Assert.That(_logger.ErrorCount, Is.EqualTo(1));
            Assert.That(survivor.Play(), Is.True);
        }

        [Test]
        public void CreateReturnsInvalidId_ReturnsNullHandle_WithOneError()
        {
            _backend.NextIdOverride = () => VfxInstanceId.Invalid;

            var handle = _system.Spawn(Request(), AssetOwner.App);

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_backend.DestroyCount, Is.EqualTo(0));
            Assert.That(_logger.ErrorCount, Is.EqualTo(1));
            Assert.That(_system.LiveInstanceCount, Is.EqualTo(0));
        }

        [Test]
        public void CreateReturnsLiveId_DoesNotDestroyTheLiveInstance()
        {
            var survivor = SpawnLive(AssetOwner.App);
            _backend.NextIdOverride = () => survivor.Id;

            var handle = _system.Spawn(Request(), AssetOwner.App);

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_backend.DestroyCount, Is.EqualTo(0));
            Assert.That(_logger.ErrorCount, Is.EqualTo(1));
            Assert.That(survivor.Play(), Is.True);
            Assert.That(_system.LiveInstanceCount, Is.EqualTo(1));
        }

        [Test]
        public void RegistrationThrows_DestroysReceivedIdOnce()
        {
            var go = CreateObject("Bound");
            _system.RegistrationFaultForTest = _ => throw new InvalidOperationException("registration");

            IVfxHandle handle = null!;
            Assert.DoesNotThrow(() => handle = _system.Spawn(Request(), AssetOwner.Bind(go)));

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_backend.CreateCount, Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_system.LiveInstanceCount, Is.EqualTo(0));
        }

        [Test]
        public void OwnerTerminatesInsideCreate_DestroysReceivedIdOnce()
        {
            BindLifecycle();
            _backend.OnCreate = _ => EmitPreUnloading(SceneA);

            var handle = _system.Spawn(Request(), AssetOwner.Scene(SceneA));

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_system.LiveInstanceCount, Is.EqualTo(0));
        }

        [Test]
        public void DisposeInsideCreate_DestroysReceivedIdOnce()
        {
            _backend.OnCreate = _ => _system.Dispose();

            var handle = _system.Spawn(Request(), AssetOwner.App);

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_system.LiveInstanceCount, Is.EqualTo(0));
        }

        [Test]
        public void HandleBackendFailure_IsLoggedOncePerHandle()
        {
            var a = SpawnLive(AssetOwner.App);
            _backend.Fault = (op, _) => op is "Play" or "Destroy" ? new InvalidOperationException(op) : null;

            Assert.DoesNotThrow(() => a.Play());
            a.Play();

            Assert.That(_logger.ErrorCount, Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_system.LiveInstanceCount, Is.EqualTo(0));
        }

        // ═══════════════════════════════════════════
        //  AC11 非対応
        // ═══════════════════════════════════════════

        [Test]
        public void Unsupported_QueriesOnce_LogsOneWarning_AndNeverCallsBackendAgain()
        {
            var backend = new FakeVfxBackend { Supported = false };
            var logger = new FakeVfxLogger();
            var system = new VfxSystem(backend, logger);
            _extraSystems.Add(system);
            system.BindSceneLifecycle(_events, _query);
            var go = CreateObject("Bound");

            for (var i = 0; i < 3; i++)
            {
                var handle = system.Spawn(Request(), AssetOwner.App);
                Assert.That(handle.IsActive, Is.False);
                Assert.That(handle.Play(), Is.False);
                system.Spawn(Request(), AssetOwner.Scene(SceneA));
                system.Spawn(Request(), AssetOwner.Bind(go));
            }

            system.Tick(0.1f);
            EmitPreUnloading(SceneA);
            system.Dispose();

            Assert.That(backend.QueryCapabilityCount, Is.EqualTo(1));
            Assert.That(backend.TotalCallCount, Is.EqualTo(1));
            Assert.That(logger.WarningCount, Is.EqualTo(1));
            Assert.That(logger.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public void CapabilityQueryThrows_TreatedAsUnsupported_WithOneError()
        {
            var backend = new FakeVfxBackend { CapabilityException = new InvalidOperationException("capability") };
            var logger = new FakeVfxLogger();
            VfxSystem? system = null;

            Assert.DoesNotThrow(() => system = new VfxSystem(backend, logger));
            _extraSystems.Add(system!);
            system!.Spawn(Request(), AssetOwner.App);
            system.Spawn(Request(), AssetOwner.App);

            Assert.That(backend.QueryCapabilityCount, Is.EqualTo(1));
            Assert.That(backend.TotalCallCount, Is.EqualTo(1));
            Assert.That(logger.ErrorCount, Is.EqualTo(1));
            Assert.That(logger.TotalCount, Is.EqualTo(1));
        }

        // ═══════════════════════════════════════════
        //  AC13 全回収と購読の解除
        // ═══════════════════════════════════════════

        [Test]
        public void Dispose_ReleasesEveryOwnerKind()
        {
            BindLifecycle();
            var go = CreateObject("Bound");
            var app = SpawnLive(AssetOwner.App);
            var manual = SpawnLive(AssetOwner.Manual);
            var scene = SpawnLive(AssetOwner.Scene(SceneA));
            var bound = SpawnLive(AssetOwner.Bind(go));
            bound.Play();
            bound.StopAllowingParticlesToFinish();

            _system.Dispose();

            AssertEachDestroyedOnce(app, manual, scene, bound);
            Assert.That(_system.LiveInstanceCount, Is.EqualTo(0));
        }

        [Test]
        public void Subscription_IsCountedOnBind_Rebind_AndDispose()
        {
            var first = new CountingSceneEventObservable();
            var second = new CountingSceneEventObservable();

            _system.BindSceneLifecycle(first, _query);
            Assert.That((first.SubscribeCount, first.UnsubscribeCount), Is.EqualTo((1, 0)));

            _system.BindSceneLifecycle(second, _query);
            Assert.That((first.SubscribeCount, first.UnsubscribeCount), Is.EqualTo((1, 1)));
            Assert.That((second.SubscribeCount, second.UnsubscribeCount), Is.EqualTo((1, 0)));

            _system.Dispose();
            Assert.That((second.SubscribeCount, second.UnsubscribeCount), Is.EqualTo((1, 1)));

            _system.Dispose();
            Assert.That(second.UnsubscribeCount, Is.EqualTo(1));
        }

        [Test]
        public void AfterDispose_EventsTickAndBindDoNotCallBackend()
        {
            var events = new CountingSceneEventObservable();
            _system.BindSceneLifecycle(events, _query);
            var a = SpawnLive(AssetOwner.Scene(SceneA));
            var go = CreateObject("Bound");
            var b = SpawnLive(AssetOwner.Bind(go));

            _system.Dispose();
            var callsAfterDispose = _backend.TotalCallCount;

            Assert.DoesNotThrow(() =>
            {
                events.Emit(new SceneEvent(SceneEventType.StateChanged, SceneA, SceneState.PreUnloading));
                _system.Tick(0.1f);
                _system.NotifyOwnerGameObjectDestroyed(GameObjectIdOf(go));
                _system.BindSceneLifecycle(events, _query);
                _system.Spawn(Request(), AssetOwner.App);
            });

            Assert.That(_backend.TotalCallCount, Is.EqualTo(callsAfterDispose));
            Assert.That(events.SubscribeCount, Is.EqualTo(1), "Dispose 後の BindSceneLifecycle は購読しない");
            Assert.That(a.IsActive, Is.False);
            Assert.That(b.IsActive, Is.False);
        }

        [Test]
        public void OnCompleted_ReleasesSceneOwned_AndRejectsUntilRebind()
        {
            BindLifecycle();
            var scene = SpawnLive(AssetOwner.Scene(SceneA));
            var app = SpawnLive(AssetOwner.App);

            _events.OnCompleted();

            Assert.That(_backend.DestroyCountOf(scene.Id), Is.EqualTo(1));
            Assert.That(app.IsActive, Is.True);
            AssertRejected(_system.Spawn(Request(), AssetOwner.Scene(SceneA)), 2);

            var next = new Subject<SceneEvent>();
            _system.BindSceneLifecycle(next, _query);
            Assert.That(_system.Spawn(Request(), AssetOwner.Scene(SceneA)).IsActive, Is.True);
            next.Dispose();
        }

        [Test]
        public void Rebind_ReleasesSceneOwned_IgnoresOldSource_AndHandlesNewEventsOnce()
        {
            var oldEvents = new CountingSceneEventObservable();
            var newEvents = new CountingSceneEventObservable();
            _system.BindSceneLifecycle(oldEvents, _query);
            var before = SpawnLive(AssetOwner.Scene(SceneA));
            var app = SpawnLive(AssetOwner.App);
            EmitTo(oldEvents, SceneEventType.StateChanged, SceneB, SceneState.PreUnloading);

            _system.BindSceneLifecycle(newEvents, _query);

            Assert.That(_backend.DestroyCountOf(before.Id), Is.EqualTo(1), "差し替え時に既存の Scene 所有を回収する");
            Assert.That(app.IsActive, Is.True);
            Assert.That(_system.Spawn(Request(), AssetOwner.Scene(SceneB)).IsActive, Is.True, "差し替えで終端中の記録を消す");

            var after = SpawnLive(AssetOwner.Scene(SceneA));
            EmitTo(oldEvents, SceneEventType.StateChanged, SceneA, SceneState.PreUnloading);
            Assert.That(after.IsActive, Is.True, "旧 Observable のイベントは影響しない");

            var destroyBefore = _backend.DestroyCount;
            EmitTo(newEvents, SceneEventType.StateChanged, SceneA, SceneState.PreUnloading);
            Assert.That(_backend.DestroyCountOf(after.Id), Is.EqualTo(1));
            Assert.That(_backend.DestroyCount - destroyBefore, Is.EqualTo(1), "新しい Observable のイベントは1回だけ処理される");
            Assert.That((newEvents.SubscribeCount, newEvents.UnsubscribeCount), Is.EqualTo((1, 0)));
        }

        private static void EmitTo(CountingSceneEventObservable events, SceneEventType type, string identity, SceneState state)
            => events.Emit(new SceneEvent(type, identity, state));

        /// <summary>IsSceneLoaded だけを使う偽 query。終端中でも true を返し続けられる。</summary>
        private sealed class FakeSceneQuery : ISceneQuery
        {
            public HashSet<string> Loaded { get; } = new(StringComparer.Ordinal);

            public SceneBase? GetLoadedScene(string identity) => null;

            public bool IsSceneLoaded(string identity) => Loaded.Contains(identity);

            public bool IsSceneStable(string identity) => Loaded.Contains(identity);
        }
    }
}
