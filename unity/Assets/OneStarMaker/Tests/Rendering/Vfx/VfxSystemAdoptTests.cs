#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UpdateSystem.Api;
using R3;
using UnityEngine;

namespace OneStarMaker.Tests.Rendering.Vfx
{
    /// <summary>
    /// Scene に配置済みの VFX の登録（<see cref="VfxSystem.Adopt"/>）を、偽 backend・偽の時間源・Subject で見る:
    /// 登録だけで開始しないこと、止まっていない最初の反映点での自動開始、止まった領域での開始の保留、
    /// 回収が Destroy ではなく Release で GameObject を壊さないこと、二重登録の拒否、非対応環境での停止。
    /// </summary>
    [TestFixture]
    public sealed class VfxSystemAdoptTests
    {
        private const string SceneA = "SceneA";

        private FakeVfxBackend _backend = null!;
        private FakeVfxLogger _logger = null!;
        private FakeVfxTimeSource _time = null!;
        private VfxSystem _system = null!;
        private Subject<SceneEvent> _events = null!;
        private LoadedSceneQuery _query = null!;
        private readonly List<GameObject> _createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeVfxBackend();
            _logger = new FakeVfxLogger();
            _time = new FakeVfxTimeSource();
            _time.SetLayer(UpdateLayerIds.Gameplay);
            _system = new VfxSystem(_backend, _logger, 1f, _time);
            _events = new Subject<SceneEvent>();
            _query = new LoadedSceneQuery();
            _query.Loaded.Add(SceneA);
            _system.BindSceneLifecycle(_events, _query);
        }

        [TearDown]
        public void TearDown()
        {
            _system.Dispose();
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

        /// <summary>配置済みの VFX の component の代わり（Framework は型を知らないので何の component でもよい）。</summary>
        private Component CreatePlaced(string name = "PlacedVfx")
        {
            var go = new GameObject(name);
            _createdObjects.Add(go);
            return go.transform;
        }

        private void Frame() => _system.Tick(_time.UnscaledDeltaTime);

        private VfxHandle AdoptLive(Component placed, string? timeLayerId = UpdateLayerIds.Gameplay)
        {
            var handle = _system.Adopt(placed, AssetOwner.Scene(SceneA), timeLayerId);
            Assert.That(handle, Is.InstanceOf<VfxHandle>(), "受理されるべき登録が拒否された");
            return (VfxHandle)handle;
        }

        // ═══════════════════════════════════════════
        //  AC2・E4 開始（配置済み）
        // ═══════════════════════════════════════════

        [Test]
        public void Adopt_RegistersOnly_ThenStartsAtFirstReflectionPoint()
        {
            var placed = CreatePlaced();
            var handle = AdoptLive(placed);

            Assert.That(_backend.AdoptCount, Is.EqualTo(1));
            Assert.That(_backend.AdoptedObjects[0], Is.SameAs(placed));
            Assert.That(_backend.PlayCount, Is.EqualTo(0), "Adopt は登録だけで開始しない");
            Assert.That(handle.IsAdopted, Is.True);

            Frame();
            Assert.That(_backend.PlayCount, Is.EqualTo(1), "止まっていない最初の反映点で自動開始する");
            Assert.That(handle.State, Is.EqualTo(VfxHandle.HandleState.Playing));

            Frame();
            Assert.That(_backend.PlayCount, Is.EqualTo(1), "自動開始は 1 回だけ");
        }

        [Test]
        public void AC2_AdoptInPausedDomain_StartsAfterResume_SetPlaybackThenPlay()
        {
            _time.SetPaused(UpdateLayerIds.Gameplay, true);
            var handle = AdoptLive(CreatePlaced());

            Frame();
            Frame();
            Assert.That(_backend.PlayCount, Is.EqualTo(0), "止まっている間は開始しない");

            _time.SetPaused(UpdateLayerIds.Gameplay, false);
            var before = _backend.OpsOf(handle.Id).Count;
            Frame();

            var ops = _backend.OpsOf(handle.Id);
            Assert.That(ops.GetRange(before, ops.Count - before), Is.EqualTo(new[] { "SetPlayback", "Play" }));
        }

        [Test]
        public void AC2_AdoptPending_ThenStop_ReleasesWithoutPlay()
        {
            _time.SetPaused(UpdateLayerIds.Gameplay, true);
            var stopped = AdoptLive(CreatePlaced("A"));
            var finished = AdoptLive(CreatePlaced("B"));

            stopped.Stop();
            finished.StopAllowingParticlesToFinish();
            _time.SetPaused(UpdateLayerIds.Gameplay, false);
            Frame();

            Assert.That(_backend.PlayCount, Is.EqualTo(0));
            Assert.That(_backend.ReleaseCount, Is.EqualTo(2));
            Assert.That(_backend.DestroyCount, Is.EqualTo(0));
        }

        [Test]
        public void ExplicitPlayBeforeFirstReflection_DoesNotStartTwice()
        {
            var handle = AdoptLive(CreatePlaced());

            Assert.That(handle.Play(), Is.True, "自動開始の保留中は受理して反映点に任せる");
            Assert.That(_backend.PlayCount, Is.EqualTo(0));
            Frame();
            Assert.That(_backend.PlayCount, Is.EqualTo(1));
        }

        // ═══════════════════════════════════════════
        //  AC4 回収（Release）と二重登録
        // ═══════════════════════════════════════════

        [Test]
        public void AC4_OwnerPreUnloading_ReleasesOnce_NotDestroy_GameObjectAlive()
        {
            var placed = CreatePlaced();
            var handle = AdoptLive(placed);
            Frame();
            var goAliveAtRelease = false;
            _backend.OnRelease = _ => goAliveAtRelease = placed != null && placed.gameObject != null;

            _events.OnNext(new SceneEvent(SceneEventType.StateChanged, SceneA, SceneState.PreUnloading));

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_backend.CountOf("Release", handle.Id), Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(0), "配置済みは Destroy しない");
            Assert.That(goAliveAtRelease, Is.True, "回収の時点で go は破棄されていない");
            Assert.That(placed != null, Is.True, "回収は GameObject を壊さない");
        }

        [Test]
        public void AC4_HandleStop_ReleasesOnce_AndNeverPlaysAgain()
        {
            var handle = AdoptLive(CreatePlaced());
            Frame();

            handle.Stop();
            var callsAfterRelease = _backend.CallCountOf(handle.Id);
            Assert.That(handle.Play(), Is.False);
            Frame();
            Frame();

            Assert.That(_backend.CountOf("Release", handle.Id), Is.EqualTo(1));
            Assert.That(_backend.CallCountOf(handle.Id), Is.EqualTo(callsAfterRelease), "回収の後は backend を呼ばない");
        }

        [Test]
        public void AC4_SecondAdoptOfSameComponent_IsRejectedWithoutBackendCall()
        {
            var placed = CreatePlaced();
            var first = AdoptLive(placed);
            var callsBefore = _backend.TotalCallCount;

            var second = _system.Adopt(placed, AssetOwner.Scene(SceneA));

            Assert.That(second.IsActive, Is.False, "何もしない handle");
            Assert.That(_backend.TotalCallCount, Is.EqualTo(callsBefore), "backend を呼ばない");
            Assert.That(first.IsActive, Is.True, "1 件目は影響を受けない");
            Assert.DoesNotThrow(() =>
            {
                second.Play();
                second.Stop();
            });
            Assert.That(first.IsActive, Is.True);
        }

        [Test]
        public void AdoptAfterRelease_IsAcceptedAsNewRegistration()
        {
            var placed = CreatePlaced();
            AdoptLive(placed).Stop();

            var again = _system.Adopt(placed, AssetOwner.Scene(SceneA));

            Assert.That(again.IsActive, Is.True, "回収した component は、呼び出し側が明示すれば新しく登録できる");
            Assert.That(_backend.AdoptCount, Is.EqualTo(2));
        }

        [Test]
        public void Adopt_RejectedOwnersAndDestroyedTarget_ReturnNoOp()
        {
            var placed = CreatePlaced();
            var destroyed = CreatePlaced("Destroyed");
            UnityEngine.Object.DestroyImmediate(destroyed.gameObject);

            Assert.That(_system.Adopt(destroyed, AssetOwner.Scene(SceneA)).IsActive, Is.False, "破棄済みの対象");
            Assert.That(_system.Adopt(placed, AssetOwner.Scene("NotLoaded")).IsActive, Is.False, "未ロードの Scene");
            Assert.That(_system.Adopt(placed, default).IsActive, Is.False, "default(AssetOwner)");
            _events.OnNext(new SceneEvent(SceneEventType.StateChanged, SceneA, SceneState.PreUnloading));
            Assert.That(_system.Adopt(placed, AssetOwner.Scene(SceneA)).IsActive, Is.False, "終端中の Scene");
            Assert.That(_backend.AdoptCount, Is.EqualTo(0));
        }

        [Test]
        public void Adopt_BackendThrowsOrReturnsInvalid_ReturnsNoOpAndLogs()
        {
            _backend.Fault = (op, _) => op == "Adopt" ? new InvalidOperationException("adopt failed") : null;
            Assert.That(_system.Adopt(CreatePlaced("A"), AssetOwner.Scene(SceneA)).IsActive, Is.False);

            _backend.Fault = null;
            _backend.NextIdOverride = () => VfxInstanceId.Invalid;
            Assert.That(_system.Adopt(CreatePlaced("B"), AssetOwner.Scene(SceneA)).IsActive, Is.False);

            Assert.That(_logger.ErrorCount, Is.EqualTo(2));
            Assert.That(_backend.ReleaseCount, Is.EqualTo(0), "登録できなかったものは回収しない");
        }

        [Test]
        public void Dispose_ReleasesAdoptedWithRelease()
        {
            var handle = AdoptLive(CreatePlaced());
            Frame();

            _system.Dispose();

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_backend.CountOf("Release", handle.Id), Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(0));
        }

        // ═══════════════════════════════════════════
        //  AC9 非対応環境（登録側）
        // ═══════════════════════════════════════════

        [Test]
        public void AC9_Unsupported_AdoptAndSpawn_NoBackendCallNoExceptionNoExtraLog()
        {
            var backend = new FakeVfxBackend { Supported = false };
            var logger = new FakeVfxLogger();
            var system = new VfxSystem(backend, logger, 1f, _time);
            try
            {
                system.BindSceneLifecycle(_events, _query);
                Assert.That(logger.TotalCount, Is.EqualTo(1), "前提: 起動時の診断 1 件");

                IVfxHandle adopted = null!;
                IVfxHandle spawned = null!;
                Assert.DoesNotThrow(() =>
                {
                    adopted = system.Adopt(CreatePlaced(), AssetOwner.Scene(SceneA));
                    spawned = system.Spawn(
                        new VfxSpawnRequest(CreatePlaced("Source"), Vector3.zero, Quaternion.identity),
                        AssetOwner.Scene(SceneA));
                    adopted.Play();
                    spawned.Play();
                    system.Tick(0.016f);
                    adopted.Stop();
                    spawned.Stop();
                });

                Assert.That(adopted.IsActive, Is.False);
                Assert.That(spawned.IsActive, Is.False);
                Assert.That(backend.TotalCallCount, Is.EqualTo(1), "QueryCapability の 1 回だけ");
                Assert.That(logger.TotalCount, Is.EqualTo(1), "追加の診断ログは無い");
                Assert.That(logger.CountOf(LogLevel.Warning), Is.EqualTo(1));
            }
            finally
            {
                system.Dispose();
            }
        }

        /// <summary>IsSceneLoaded だけを使う偽 query。</summary>
        private sealed class LoadedSceneQuery : ISceneQuery
        {
            public HashSet<string> Loaded { get; } = new(StringComparer.Ordinal);

            public SceneBase? GetLoadedScene(string identity) => null;

            public bool IsSceneLoaded(string identity) => Loaded.Contains(identity);

            public bool IsSceneStable(string identity) => Loaded.Contains(identity);
        }
    }
}
