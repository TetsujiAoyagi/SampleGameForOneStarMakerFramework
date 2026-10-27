#nullable enable

using System;
using System.Collections.Generic;
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
    /// VFX の時間の領域を、偽 backend と偽の時間源で見る: 反映点での再生状態の書き写し（変わったときだけ）、
    /// 止まった領域での開始の保留、消え切り待ちの timeout を領域の時間で数えること、異常な delta の扱い、
    /// 所属先の検証。反映点は <see cref="VfxSystem.Tick"/>（host の LateUpdate から呼ばれる）。
    /// 実 VFX の挙動はここでは見ない（SampleGame の Play テスト）。
    /// </summary>
    [TestFixture]
    public sealed class VfxSystemTimeLayerTests
    {
        private const string SceneA = "SceneA";
        private const float Timeout = 1f;

        private FakeVfxBackend _backend = null!;
        private FakeVfxLogger _logger = null!;
        private FakeVfxTimeSource _time = null!;
        private VfxSystem _system = null!;
        private GameObject _source = null!;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeVfxBackend();
            _logger = new FakeVfxLogger();
            _time = new FakeVfxTimeSource();
            _time.SetLayer(UpdateLayerIds.Gameplay);
            _system = new VfxSystem(_backend, _logger, Timeout, _time);
            _source = new GameObject("VfxSource");
        }

        [TearDown]
        public void TearDown()
        {
            _system.Dispose();
            if (_source != null)
            {
                UnityEngine.Object.DestroyImmediate(_source);
            }
        }

        private VfxSpawnRequest Request() => new(_source, Vector3.zero, Quaternion.identity);

        /// <summary>1 フレーム分の反映点。host と同じく delta 引数は時間源が正なので使われない。</summary>
        private void Frame() => _system.Tick(_time.UnscaledDeltaTime);

        private VfxHandle SpawnPlaying(VfxSpawnRequest? request = null)
        {
            var handle = (VfxHandle)_system.Spawn(request ?? Request(), AssetOwner.App);
            Assert.That(handle.Play(), Is.True);
            return handle;
        }

        // ═══════════════════════════════════════════
        //  宣言
        // ═══════════════════════════════════════════

        [Test]
        public void SpawnRequest_DefaultsToGameplay_AndUnscaledIsNull()
        {
            Assert.That(Request().TimeLayerId, Is.EqualTo(UpdateLayerIds.Gameplay));
            Assert.That(default(VfxSpawnRequest).TimeLayerId, Is.EqualTo(UpdateLayerIds.Gameplay), "default でも gameplay");
            var unscaled = Request().WithUnscaledTime();
            Assert.That(unscaled.TimeLayerId, Is.Null);
            Assert.That(unscaled.Source, Is.SameAs(_source), "他の値は保つ");
        }

        // ═══════════════════════════════════════════
        //  AC1 反映点での書き写し
        // ═══════════════════════════════════════════

        [Test]
        public void AC1_WritesOnlyWhenChanged_AndPauseIsReflectedWhileLayerPaused()
        {
            var handle = SpawnPlaying();
            Frame();
            Assert.That(_backend.SetPlaybackCount, Is.EqualTo(0), "初期状態（止まっていない、倍率 1）と同じなら書かない");

            _time.SetPaused(UpdateLayerIds.Gameplay, true);
            Frame();
            Frame();
            Frame();
            Assert.That(_backend.PlaybackCalls, Has.Count.EqualTo(1), "Pause は 1 回だけ書く");
            Assert.That(_backend.PlaybackCalls[0], Is.EqualTo((handle.Id.Value, true, 1f)));

            _time.SetPaused(UpdateLayerIds.Gameplay, false);
            Frame();
            Assert.That(_backend.PlaybackCalls, Has.Count.EqualTo(2), "止まっている Layer の Resume も反映点で書く");
            Assert.That(_backend.PlaybackCalls[1], Is.EqualTo((handle.Id.Value, false, 1f)));
        }

        [Test]
        public void AC1_PauseAndResumeBetweenReflectionPoints_WritesNothing()
        {
            SpawnPlaying();
            Frame();

            _time.SetPaused(UpdateLayerIds.Gameplay, true);
            _time.SetPaused(UpdateLayerIds.Gameplay, false);
            Frame();

            Assert.That(_backend.SetPlaybackCount, Is.EqualTo(0));
        }

        [Test]
        public void AC1_LayerTimeScaleAndGlobalTimeScale_AreComposedIntoRate()
        {
            var handle = SpawnPlaying();

            // Time.timeScale = 0.5 は Time.deltaTime に現れる。Layer の倍率 2 と合わせて 1。
            _time.SetFrame(deltaTime: 0.008f, unscaledDeltaTime: 0.016f);
            _time.SetScale(UpdateLayerIds.Gameplay, 2f);
            Frame();
            Assert.That(_backend.SetPlaybackCount, Is.EqualTo(0), "0.5 × 2 = 1 は書かない");

            _time.SetScale(UpdateLayerIds.Gameplay, 0.5f);
            Frame();
            Assert.That(_backend.PlaybackCalls, Has.Count.EqualTo(1));
            Assert.That(_backend.PlaybackCalls[0].Id, Is.EqualTo(handle.Id.Value));
            Assert.That(_backend.PlaybackCalls[0].Paused, Is.False);
            Assert.That(_backend.PlaybackCalls[0].Rate, Is.EqualTo(0.25f).Within(1e-6f));
        }

        /// <summary>TimeScale == 0 は止まっていない（IsPaused と区別する）。倍率 0 で書き、開始は届く。</summary>
        [Test]
        public void AC1_TimeScaleZero_IsNotPaused_StartIsDeliveredWithRateZero()
        {
            _time.SetScale(UpdateLayerIds.Gameplay, 0f);
            var handle = (VfxHandle)_system.Spawn(Request(), AssetOwner.App);

            Assert.That(handle.Play(), Is.True);

            Assert.That(_backend.OpsOf(handle.Id), Is.EqualTo(new[] { "Create", "SetPlayback", "Play" }));
            Assert.That(_backend.PlaybackCalls[0].Paused, Is.False);
            Assert.That(_backend.PlaybackCalls[0].Rate, Is.EqualTo(0f));
        }

        [Test]
        public void UnscaledRequest_IgnoresLayerPauseAndScale()
        {
            var handle = SpawnPlaying(Request().WithUnscaledTime());
            _time.SetFrame(deltaTime: 0.004f, unscaledDeltaTime: 0.016f);
            _time.SetLayer(UpdateLayerIds.Gameplay, paused: true, scale: 3f);

            Frame();
            Frame();

            Assert.That(handle.TimeLayerId, Is.Null);
            Assert.That(_backend.SetPlaybackCount, Is.EqualTo(0), "unscaled は常に止まらず倍率 1");
        }

        [Test]
        public void NotCreatedLayer_IsTreatedAsRunningAtScaleOne()
        {
            _time = new FakeVfxTimeSource();
            _system.Dispose();
            _system = new VfxSystem(_backend, _logger, Timeout, _time);
            _time.SetFrame(deltaTime: 0.016f);

            var handle = SpawnPlaying();
            Frame();

            Assert.That(handle.IsActive, Is.True);
            Assert.That(_backend.SetPlaybackCount, Is.EqualTo(0));
        }

        // ═══════════════════════════════════════════
        //  AC2 開始の保留（動的）
        // ═══════════════════════════════════════════

        [Test]
        public void AC2_PlayInPausedDomain_IsDeferred_ThenSetPlaybackThenPlayAtFirstRunningReflection()
        {
            _time.SetPaused(UpdateLayerIds.Gameplay, true);
            var handle = (VfxHandle)_system.Spawn(Request(), AssetOwner.App);

            Assert.That(handle.Play(), Is.True, "受理して true");
            Frame();
            Frame();
            Assert.That(_backend.PlayCount, Is.EqualTo(0), "止まっている間は backend Play を呼ばない");
            Assert.That(handle.IsStartPending, Is.True);

            _time.SetPaused(UpdateLayerIds.Gameplay, false);
            var callsBefore = _backend.OpsOf(handle.Id).Count;
            Frame();

            var ops = _backend.OpsOf(handle.Id);
            Assert.That(ops.GetRange(callsBefore, ops.Count - callsBefore), Is.EqualTo(new[] { "SetPlayback", "Play" }));
            Assert.That(handle.State, Is.EqualTo(VfxHandle.HandleState.Playing));

            Frame();
            Assert.That(_backend.PlayCount, Is.EqualTo(1), "開始は 1 回だけ");
        }

        [Test]
        public void AC2_PendingStart_ThenStop_IsReleasedWithoutPlay()
        {
            _time.SetPaused(UpdateLayerIds.Gameplay, true);
            var stopped = _system.Spawn(Request(), AssetOwner.App);
            var finished = _system.Spawn(Request(), AssetOwner.App);
            var disposed = _system.Spawn(Request(), AssetOwner.App);
            stopped.Play();
            finished.Play();
            disposed.Play();

            stopped.Stop();
            finished.StopAllowingParticlesToFinish();
            disposed.Dispose();
            _time.SetPaused(UpdateLayerIds.Gameplay, false);
            Frame();

            Assert.That(_backend.PlayCount, Is.EqualTo(0));
            Assert.That(_backend.DestroyCount, Is.EqualTo(3));
            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(0), "未開始は消え切り待ちに入らない");
        }

        [Test]
        public void AC2_RetriggerWhilePaused_IsDeferredAndDeliveredOnce()
        {
            var handle = SpawnPlaying();
            _time.SetPaused(UpdateLayerIds.Gameplay, true);
            Frame();

            Assert.That(handle.Play(), Is.True);
            Assert.That(handle.Play(), Is.True);
            Assert.That(_backend.PlayCount, Is.EqualTo(1), "止まっている間の再トリガは保留");

            _time.SetPaused(UpdateLayerIds.Gameplay, false);
            Frame();
            Assert.That(_backend.PlayCount, Is.EqualTo(2), "保留した再トリガは 1 回だけ届く");
        }

        // ═══════════════════════════════════════════
        //  AC3 消え切り待ちは領域の時間
        // ═══════════════════════════════════════════

        [Test]
        public void AC3_StoppingTimeout_CountsLayerScaledTime()
        {
            var handle = SpawnPlaying();
            handle.StopAllowingParticlesToFinish();
            _time.SetScale(UpdateLayerIds.Gameplay, 0.5f);
            _time.SetFrame(deltaTime: 0.5f, unscaledDeltaTime: 0.5f);

            Frame();
            Frame();
            Frame();
            Assert.That(handle.IsActive, Is.True, "0.25 × 3 = 0.75 < 1");

            Frame();
            Assert.That(handle.IsActive, Is.False, "0.25 × 4 = 1 で timeout");
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
        }

        /// <summary>Time.deltaTime が maximumDeltaTime で頭打ちになったフレームは、頭打ちの値で進む（unscaled ではない）。</summary>
        [Test]
        public void AC3_CappedDeltaTime_IsUsed_NotUnscaled()
        {
            var handle = SpawnPlaying();
            handle.StopAllowingParticlesToFinish();
            _time.SetFrame(deltaTime: 0.25f, unscaledDeltaTime: 0.9f);

            Frame();
            Frame();
            Frame();
            Assert.That(handle.IsActive, Is.True, "0.25 × 3 = 0.75。unscaled なら 2.7 で timeout している");

            Frame();
            Assert.That(handle.IsActive, Is.False);
        }

        [Test]
        public void AC3_Paused_TimeoutDoesNotAdvance_AppOwnedRemainsUntilDispose()
        {
            var handle = SpawnPlaying();
            handle.StopAllowingParticlesToFinish();
            _time.SetPaused(UpdateLayerIds.Gameplay, true);
            _time.SetFrame(deltaTime: 0.5f);

            for (var i = 0; i < 20; i++)
            {
                Frame();
            }

            Assert.That(handle.IsActive, Is.True, "止まっている間は timeout しない（実時間では数えない）");

            _system.Dispose();
            Assert.That(handle.IsActive, Is.False, "App 所有の出口は Dispose");
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
        }

        [Test]
        public void AC3_Paused_OwnerTermination_ReleasesImmediately()
        {
            var events = new Subject<SceneEvent>();
            var query = new LoadedSceneQuery(SceneA);
            _system.BindSceneLifecycle(events, query);
            var handle = (VfxHandle)_system.Spawn(Request(), AssetOwner.Scene(SceneA));
            handle.Play();
            handle.StopAllowingParticlesToFinish();
            _time.SetPaused(UpdateLayerIds.Gameplay, true);
            Frame();

            events.OnNext(new SceneEvent(SceneEventType.StateChanged, SceneA, SceneState.PreUnloading));

            Assert.That(handle.IsActive, Is.False, "所有者の終端は時間と無関係に即回収");
            Assert.That(_backend.DestroyCountOf(handle.Id), Is.EqualTo(1));
            events.Dispose();
        }

        [Test]
        public void AC3_UnscaledRequest_TimeoutCountsUnscaledEvenWhilePaused()
        {
            var handle = SpawnPlaying(Request().WithUnscaledTime());
            handle.StopAllowingParticlesToFinish();
            _time.SetPaused(UpdateLayerIds.Gameplay, true);
            _time.SetFrame(deltaTime: 0f, unscaledDeltaTime: 0.25f);

            Frame();
            Frame();
            Frame();
            Assert.That(handle.IsActive, Is.True, "0.25 × 3 = 0.75 < 1");
            Frame();
            Assert.That(handle.IsActive, Is.False, "unscaled の領域は実時間で数える");
        }

        /// <summary>
        /// unscaled の領域の時間は <c>min(unscaledDeltaTime, maximumDeltaTime)</c>。上限を超えるフレームでは
        /// 倍率が 1 未満で書かれ、消え切り待ちも上限の分だけ進む。
        /// </summary>
        [Test]
        public void AC3_UnscaledRequest_FrameOverMaximumDeltaTime_IsCapped()
        {
            var handle = SpawnPlaying(Request().WithUnscaledTime());
            handle.StopAllowingParticlesToFinish();
            _time.MaximumDeltaTime = 0.25f;
            _time.SetFrame(deltaTime: 0.125f, unscaledDeltaTime: 0.5f);

            Frame();
            Assert.That(_backend.PlaybackCalls, Has.Count.EqualTo(1));
            Assert.That(_backend.PlaybackCalls[0].Paused, Is.False);
            Assert.That(_backend.PlaybackCalls[0].Rate, Is.EqualTo(0.5f), "0.25 ÷ 0.5");
            Frame();
            Frame();
            Assert.That(handle.IsActive, Is.True, "0.25 × 3 = 0.75。上限が無ければ 1.5 で timeout している");
            Frame();
            Assert.That(handle.IsActive, Is.False);
        }

        // ═══════════════════════════════════════════
        //  AC5 異常な delta
        // ═══════════════════════════════════════════

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void AC5_AbnormalUnscaledDelta_KeepsRate_NoCallNoException(float unscaled)
        {
            var handle = SpawnPlaying();
            _time.SetScale(UpdateLayerIds.Gameplay, 2f);
            Frame();
            Assert.That(_backend.PlaybackCalls, Has.Count.EqualTo(1), "前提: 倍率 2 を書いた");

            _time.SetFrame(deltaTime: 0.016f, unscaledDeltaTime: unscaled);
            Assert.DoesNotThrow(Frame);

            Assert.That(_backend.PlaybackCalls, Has.Count.EqualTo(1), "状態が変わらなければ書かない");
            Assert.That(handle.IsActive, Is.True);
        }

        /// <summary>
        /// unscaled の要求でも、異常な delta のフレームは倍率を保ち、消え切り待ちは進まない
        /// （異常の判定は上限との min より先。<c>min(NaN, M)</c> は M になるため）。
        /// </summary>
        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void AC5_UnscaledRequest_AbnormalDelta_KeepsRate_TimeoutDoesNotAdvance(float unscaled)
        {
            var handle = SpawnPlaying(Request().WithUnscaledTime());
            handle.StopAllowingParticlesToFinish();
            _time.SetFrame(deltaTime: 0.016f, unscaledDeltaTime: unscaled);

            for (var i = 0; i < 20; i++)
            {
                Assert.DoesNotThrow(Frame);
            }

            Assert.That(_backend.SetPlaybackCount, Is.EqualTo(0), "倍率は前回の値（1）のまま");
            Assert.That(handle.IsActive, Is.True, "異常な delta のフレームでは消え切り待ちが進まない");

            _time.SetFrame(deltaTime: 0.016f, unscaledDeltaTime: 0.25f);
            for (var i = 0; i < 4; i++)
            {
                Frame();
            }

            Assert.That(handle.IsActive, Is.False, "正常な delta に戻ると進む");
        }

        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void AC5_AbnormalDeltaWithPauseAndResume_WritesPausedWithPreviousRate(float unscaled)
        {
            SpawnPlaying();
            _time.SetScale(UpdateLayerIds.Gameplay, 2f);
            Frame();

            _time.SetFrame(deltaTime: 0.016f, unscaledDeltaTime: unscaled);
            _time.SetPaused(UpdateLayerIds.Gameplay, true);
            Frame();
            _time.SetPaused(UpdateLayerIds.Gameplay, false);
            Frame();

            Assert.That(_backend.PlaybackCalls, Has.Count.EqualTo(3));
            Assert.That(_backend.PlaybackCalls[1].Paused, Is.True, "Pause は異常な delta でも届く");
            Assert.That(_backend.PlaybackCalls[1].Rate, Is.EqualTo(2f), "倍率は前回の値");
            Assert.That(_backend.PlaybackCalls[2].Paused, Is.False);
            Assert.That(_backend.PlaybackCalls[2].Rate, Is.EqualTo(2f));
        }

        // ═══════════════════════════════════════════
        //  所属先の検証
        // ═══════════════════════════════════════════

        [TestCase(UpdateLayerIds.Vfx)]
        [TestCase(" Vfx ")]
        [TestCase("")]
        [TestCase("   ")]
        public void Adopt_InvalidTimeLayer_ThrowsArgumentException(string layerId)
        {
            var placed = new GameObject("Placed");
            try
            {
                Assert.Throws<ArgumentException>(() => _system.Adopt(placed.transform, AssetOwner.App, layerId));
                Assert.That(_backend.AdoptCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(placed);
            }
        }

        [Test]
        public void Adopt_InvalidTimeLayer_ThrowsEvenWhenUnsupported()
        {
            var backend = new FakeVfxBackend { Supported = false };
            var system = new VfxSystem(backend, new FakeVfxLogger(), Timeout, _time);
            var placed = new GameObject("Placed");
            try
            {
                Assert.Throws<ArgumentException>(() => system.Adopt(placed.transform, AssetOwner.App, UpdateLayerIds.Vfx));
            }
            finally
            {
                system.Dispose();
                UnityEngine.Object.DestroyImmediate(placed);
            }
        }

        /// <summary>IsSceneLoaded だけを使う偽 query。</summary>
        private sealed class LoadedSceneQuery : ISceneQuery
        {
            private readonly HashSet<string> _loaded = new(StringComparer.Ordinal);

            public LoadedSceneQuery(params string[] loaded)
            {
                foreach (var identity in loaded)
                {
                    _loaded.Add(identity);
                }
            }

            public SceneBase? GetLoadedScene(string identity) => null;

            public bool IsSceneLoaded(string identity) => _loaded.Contains(identity);

            public bool IsSceneStable(string identity) => _loaded.Contains(identity);
        }
    }
}
