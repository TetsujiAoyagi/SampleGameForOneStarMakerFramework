#nullable enable

using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.UpdateSystem.Api;
using OneStarMaker.Tests.Rendering.Vfx;
using SampleGame.InGame.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SampleGame.Tests.World
{
    /// <summary>
    /// trigger 1 つ分の判断（<see cref="LocalVfxEventSession"/>）を、偽の VfxSystem・loader・logger で確かめる。
    /// Unity の physics と Scene は使わない（実 Scene の経路は Play テストが見る）。
    /// </summary>
    [TestFixture]
    public sealed class LocalVfxEventSessionTests
    {
        private const ulong BodyA = 101;
        private const ulong BodyB = 202;

        private static readonly Vector3 TriggerPosition = new(12f, 3f, -7f);
        private static readonly Quaternion TriggerRotation = Quaternion.Euler(0f, 90f, 0f);

        private GameObject _triggerObject = null!;
        private GameObject _source = null!;
        private RecordingVfxSystem _vfx = null!;
        private FakeVfxLogger _logger = null!;

        [SetUp]
        public void SetUp()
        {
            _triggerObject = new GameObject("LocalVfxEventSessionTests.Trigger");
            _source = new GameObject("LocalVfxEventSessionTests.Source");
            _vfx = new RecordingVfxSystem();
            _logger = new FakeVfxLogger();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_triggerObject);
            Object.DestroyImmediate(_source);
        }

        // ═══ AC1: 準備済みで在室 0→1 なら起動する ═══

        [Test]
        public async Task Ready_FirstEnter_SpawnsOnceAtTriggerPoseOwnedByTriggerAndPlays()
        {
            var session = CreateSession();
            await PrepareReadyAsync(session);

            session.Enter(BodyA);

            Assert.That(_vfx.Spawns, Has.Count.EqualTo(1));
            var (request, owner, handle) = _vfx.Spawns[0];
            Assert.That(owner, Is.EqualTo(AssetOwner.Bind(_triggerObject)));
            Assert.That(request.Source, Is.SameAs(_source));
            Assert.That(request.Position, Is.EqualTo(TriggerPosition));
            Assert.That(request.Rotation, Is.EqualTo(TriggerRotation));
            Assert.That(request.HasParent, Is.False, "親なし（回収漏れが残骸として見える）");
            Assert.That(request.TimeLayerId, Is.EqualTo(UpdateLayerIds.Gameplay));
            Assert.That(handle.PlayCount, Is.EqualTo(1));
        }

        // ═══ AC2: 準備前の進入 ═══

        [Test]
        public async Task EnterBeforeReady_SpawnsWhenPreparationCompletes()
        {
            var session = CreateSession();
            var load = new UniTaskCompletionSource<Object?>();
            var prepare = session.PrepareAsync(() => load.Task);

            session.Enter(BodyA);
            Assert.That(_vfx.Spawns, Is.Empty, "準備の前は起動しない");

            load.TrySetResult(_source);
            await prepare;

            Assert.That(_vfx.Spawns, Has.Count.EqualTo(1));
            Assert.That(_vfx.Spawns[0].Handle.PlayCount, Is.EqualTo(1));
        }

        [Test]
        public async Task EnterAndExitBeforeReady_DoesNotSpawnWhenPreparationCompletes()
        {
            var session = CreateSession();
            var load = new UniTaskCompletionSource<Object?>();
            var prepare = session.PrepareAsync(() => load.Task);

            session.Enter(BodyA);
            session.Exit(BodyA);
            load.TrySetResult(_source);
            await prepare;

            Assert.That(_vfx.Spawns, Is.Empty);
        }

        // ═══ AC3: 退出と再入 ═══

        [Test]
        public async Task Exit_LastOccupant_StopsAllowingParticlesToFinish()
        {
            var session = CreateSession();
            await PrepareReadyAsync(session);
            session.Enter(BodyA);

            session.Exit(BodyA);

            var handle = _vfx.Spawns[0].Handle;
            Assert.That(handle.StopAllowingCount, Is.EqualTo(1));
            Assert.That(handle.StopCount, Is.Zero);
        }

        [Test]
        public async Task Reenter_WhileHandleActive_StopsOldBeforeSpawningNew()
        {
            var session = CreateSession();
            await PrepareReadyAsync(session);
            session.Enter(BodyA);
            session.Exit(BodyA);

            session.Enter(BodyA);

            Assert.That(_vfx.Spawns, Has.Count.EqualTo(2));
            Assert.That(_vfx.Spawns[0].Handle.StopCount, Is.EqualTo(1));
            Assert.That(_vfx.Calls, Is.EqualTo(new[] { "Spawn#1", "Play#1", "StopAllowing#1", "Stop#1", "Spawn#2", "Play#2" }));
        }

        [Test]
        public async Task Reenter_AfterOldHandleCollected_DoesNotStopOld_AndSpawnsNew()
        {
            var session = CreateSession();
            await PrepareReadyAsync(session);
            session.Enter(BodyA);
            session.Exit(BodyA);
            _vfx.Spawns[0].Handle.IsActive = false; // 消え切って回収済み

            session.Enter(BodyA);

            Assert.That(_vfx.Spawns, Has.Count.EqualTo(2));
            Assert.That(_vfx.Spawns[0].Handle.StopCount, Is.Zero);
            Assert.That(_vfx.Calls, Is.EqualTo(new[] { "Spawn#1", "Play#1", "StopAllowing#1", "Spawn#2", "Play#2" }));
        }

        [Test]
        public async Task SecondBodyEnteringAndLeaving_KeepsTheInstanceRunning()
        {
            var session = CreateSession();
            await PrepareReadyAsync(session);
            session.Enter(BodyA);

            session.Enter(BodyB);
            session.Exit(BodyB);

            Assert.That(_vfx.Spawns, Has.Count.EqualTo(1));
            Assert.That(_vfx.Spawns[0].Handle.StopAllowingCount, Is.Zero, "まだ A が中にいる");
        }

        [Test]
        public async Task DuplicateEnterOfSameBody_DoesNotSpawnAgain()
        {
            var session = CreateSession();
            await PrepareReadyAsync(session);

            session.Enter(BodyA);
            session.Enter(BodyA);

            Assert.That(_vfx.Spawns, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ExitOfBodyNotInside_DoesNothing()
        {
            var session = CreateSession();
            await PrepareReadyAsync(session);
            session.Enter(BodyA);

            Assert.DoesNotThrow(() => session.Exit(BodyB));

            Assert.That(_vfx.Spawns[0].Handle.StopAllowingCount, Is.Zero);
        }

        // ═══ AC4: 終了 ═══

        [Test]
        public async Task End_StopsActiveHandleOnce_EvenWhenCalledTwice()
        {
            var session = CreateSession();
            await PrepareReadyAsync(session);
            session.Enter(BodyA);

            session.End();
            session.End();

            Assert.That(_vfx.Spawns[0].Handle.StopCount, Is.EqualTo(1));
            Assert.That(session.IsEnded, Is.True);
        }

        [Test]
        public async Task End_WithCollectedHandle_DoesNotStop()
        {
            var session = CreateSession();
            await PrepareReadyAsync(session);
            session.Enter(BodyA);
            _vfx.Spawns[0].Handle.IsActive = false;

            session.End();

            Assert.That(_vfx.Spawns[0].Handle.StopCount, Is.Zero);
        }

        [Test]
        public async Task EnterAfterEnd_DoesNotSpawn()
        {
            var session = CreateSession();
            await PrepareReadyAsync(session);

            session.End();
            session.Enter(BodyA);

            Assert.That(_vfx.Spawns, Is.Empty);
        }

        [Test]
        public async Task PreparationCompletingAfterEnd_DoesNotSpawn()
        {
            var session = CreateSession();
            var load = new UniTaskCompletionSource<Object?>();
            var prepare = session.PrepareAsync(() => load.Task);
            session.Enter(BodyA);

            session.End();
            load.TrySetResult(_source);
            await prepare;

            Assert.That(_vfx.Spawns, Is.Empty);
            Assert.That(_logger.TotalCount, Is.Zero);
        }

        [Test]
        public async Task EnterAfterFailedPreparation_DoesNotSpawnOrThrow()
        {
            var session = CreateSession();
            await session.PrepareAsync(() => throw new InvalidOperationException("load failed"));

            Assert.DoesNotThrow(() => session.Enter(BodyA));

            Assert.That(_vfx.Spawns, Is.Empty);
        }

        // ═══ AC13: 準備の失敗は外へ出さず、Error ログ 1 件で trigger だけを止める ═══

        [Test]
        public async Task LoaderThrows_CompletesWithoutThrowing_LogsOneError_AndNeverSpawns()
        {
            var session = CreateSession();
            session.Enter(BodyA);

            await session.PrepareAsync(() => UniTask.FromException<Object?>(new InvalidOperationException("load failed")));
            session.Exit(BodyA);
            session.Enter(BodyA);

            Assert.That(_vfx.Spawns, Is.Empty);
            Assert.That(_logger.ErrorCount, Is.EqualTo(1));
            Assert.That(_logger.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public async Task LoaderReturnsNull_CompletesWithoutThrowing_LogsOneError_AndNeverSpawns()
        {
            var session = CreateSession();
            session.Enter(BodyA);

            await session.PrepareAsync(() => UniTask.FromResult<Object?>(null));
            session.Exit(BodyA);
            session.Enter(BodyA);

            Assert.That(_vfx.Spawns, Is.Empty);
            Assert.That(_logger.ErrorCount, Is.EqualTo(1));
            Assert.That(_logger.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public void LoaderCanceled_PropagatesCancellation_AndLogsNothing()
        {
            var session = CreateSession();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await session.PrepareAsync(() => UniTask.FromCanceled<Object?>()));

            Assert.That(_logger.TotalCount, Is.Zero);
        }

        [Test]
        public async Task PrepareTwice_RunsLoaderOnlyOnce()
        {
            var session = CreateSession();
            var calls = 0;

            await session.PrepareAsync(() => { calls++; return UniTask.FromResult<Object?>(_source); });
            await session.PrepareAsync(() => { calls++; return UniTask.FromResult<Object?>(_source); });

            Assert.That(calls, Is.EqualTo(1));
        }

        // ─── helpers ───

        private LocalVfxEventSession CreateSession()
            => new(_vfx, AssetOwner.Bind(_triggerObject), TriggerPosition, TriggerRotation, "test-trigger", _logger);

        private UniTask PrepareReadyAsync(LocalVfxEventSession session)
            => session.PrepareAsync(() => UniTask.FromResult<Object?>(_source));
    }
}
