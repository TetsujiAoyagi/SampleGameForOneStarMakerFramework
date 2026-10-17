#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using UnityEngine;

namespace OneStarMaker.Tests.Rendering.Vfx
{
    /// <summary>
    /// VfxHandle の状態機械を VfxSystem なしで見る。回収 callback と失敗記録 callback だけを差し込み、
    /// 偽 backend の呼び出し回数で判定する。
    /// </summary>
    [TestFixture]
    public sealed class VfxHandleStateTests
    {
        private const float Timeout = 1f;

        private FakeVfxBackend _backend = null!;
        private List<VfxHandle> _released = null!;
        private List<Exception> _failures = null!;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeVfxBackend();
            _released = new List<VfxHandle>();
            _failures = new List<Exception>();
        }

        private VfxHandle CreateHandle(ulong id = 1)
        {
            return new VfxHandle(
                _backend,
                new VfxInstanceId(id),
                AssetOwner.Manual,
                h => _released.Add(h),
                (_, e) => _failures.Add(e));
        }

        private VfxHandle CreateStoppingHandle()
        {
            var handle = CreateHandle();
            Assert.That(handle.Play(), Is.True);
            handle.StopAllowingParticlesToFinish();
            Assert.That(handle.State, Is.EqualTo(VfxHandle.HandleState.Stopping));
            return handle;
        }

        // ═══════════════════════════════════════════
        //  AC1 即時 Stop と Created・Playing の行
        // ═══════════════════════════════════════════

        [Test]
        public void PlayStopStopDispose_DestroysOnce_ThenNoBackendCalls()
        {
            var handle = CreateHandle();

            Assert.That(handle.Play(), Is.True);
            handle.Stop();
            var callsAfterFirstStop = _backend.TotalCallCount;
            handle.Stop();
            handle.Dispose();

            Assert.That(_backend.PlayCount, Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_backend.TotalCallCount, Is.EqualTo(callsAfterFirstStop));
            Assert.That(handle.IsActive, Is.False);
            Assert.That(_released, Has.Count.EqualTo(1));
        }

        [Test]
        public void Created_StopAllowingParticlesToFinish_DestroysWithoutStopEmitting()
        {
            var handle = CreateHandle();

            handle.StopAllowingParticlesToFinish();

            Assert.That(_backend.StopEmittingCount, Is.EqualTo(0));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(handle.IsActive, Is.False);
        }

        [Test]
        public void PlayReturnsFalse_StaysCreated_ThenGracefulStopDestroysImmediately()
        {
            _backend.PlayResult = false;
            var handle = CreateHandle();

            Assert.That(handle.Play(), Is.False);
            Assert.That(handle.State, Is.EqualTo(VfxHandle.HandleState.Created));

            handle.StopAllowingParticlesToFinish();

            Assert.That(_backend.StopEmittingCount, Is.EqualTo(0));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
        }

        [Test]
        public void Playing_PlayAgain_RetriggersBackend()
        {
            var handle = CreateHandle();

            Assert.That(handle.Play(), Is.True);
            Assert.That(handle.Play(), Is.True);

            Assert.That(_backend.PlayCount, Is.EqualTo(2));
            Assert.That(handle.State, Is.EqualTo(VfxHandle.HandleState.Playing));
        }

        // ═══════════════════════════════════════════
        //  AC2 消え切り待ち Stop
        // ═══════════════════════════════════════════

        [Test]
        public void GracefulStop_DestroysWhenParticlesGone()
        {
            var handle = CreateStoppingHandle();

            handle.AdvanceStopping(0.1f, Timeout);
            Assert.That(_backend.DestroyCount, Is.EqualTo(0));

            _backend.AliveParticles = false;
            handle.AdvanceStopping(0.1f, Timeout);

            Assert.That(_backend.StopEmittingCount, Is.EqualTo(1));
            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(2));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));

            handle.AdvanceStopping(0.1f, Timeout);
            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(2));
        }

        [Test]
        public void Stopping_PlayIsIgnored_AndGracefulStopDoesNotExtend()
        {
            var handle = CreateStoppingHandle();
            var playCount = _backend.PlayCount;

            Assert.That(handle.Play(), Is.False);
            handle.AdvanceStopping(0.6f, Timeout);
            handle.StopAllowingParticlesToFinish();
            handle.AdvanceStopping(0.4f, Timeout);

            Assert.That(_backend.PlayCount, Is.EqualTo(playCount));
            Assert.That(_backend.StopEmittingCount, Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1), "再呼び出しで待ち時間が 0 に戻ると timeout しない");
        }

        // ═══════════════════════════════════════════
        //  AC3 timeout と delta の扱い
        // ═══════════════════════════════════════════

        [Test]
        public void Timeout_ReachedExactly_DestroysWithoutParticleQuery()
        {
            var handle = CreateStoppingHandle();

            handle.AdvanceStopping(0.5f, Timeout);
            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(0));

            handle.AdvanceStopping(0.5f, Timeout);
            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(1), "timeout 到達時は粒子判定を呼ばない");
            Assert.That(_backend.DestroyCount, Is.EqualTo(1), "待ち時間 == timeout で回収する（> ではない）");
        }

        [Test]
        public void ZeroDelta_StillQueriesParticles()
        {
            var handle = CreateStoppingHandle();

            handle.AdvanceStopping(0f, Timeout);

            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(0));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteDelta_DoesNotAdvanceWaiting(float delta)
        {
            var handle = CreateStoppingHandle();

            handle.AdvanceStopping(delta, Timeout);
            Assert.That(_backend.DestroyCount, Is.EqualTo(0), "非有限の delta で即 timeout にしない");

            handle.AdvanceStopping(0.9f, Timeout);
            Assert.That(_backend.DestroyCount, Is.EqualTo(0));

            handle.AdvanceStopping(0.1f, Timeout);
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
        }

        [Test]
        public void NegativeDelta_DoesNotReduceWaiting()
        {
            var handle = CreateStoppingHandle();

            handle.AdvanceStopping(0.9f, Timeout);
            handle.AdvanceStopping(-5f, Timeout);
            handle.AdvanceStopping(0.1f, Timeout);

            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
        }

        // ═══════════════════════════════════════════
        //  AC4 競合行と backend 呼び出し中の再入
        // ═══════════════════════════════════════════

        [Test]
        public void Stopping_Stop_AbortsWaitingAndDestroysOnce()
        {
            var handle = CreateStoppingHandle();

            handle.Stop();
            handle.AdvanceStopping(0.1f, Timeout);

            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(0));
        }

        [Test]
        public void Stopping_Dispose_AbortsWaitingAndDestroysOnce()
        {
            var handle = CreateStoppingHandle();

            handle.Dispose();
            handle.AdvanceStopping(0.1f, Timeout);

            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(0));
        }

        [Test]
        public void Stopping_OwnerTerminationRelease_ThenStop_DestroysOnce()
        {
            var handle = CreateStoppingHandle();

            // 所有者の終端は VfxSystem から Release として届く。二回目の終端と後続操作は何もしない。
            handle.Release();
            handle.Release();
            handle.Stop();
            handle.AdvanceStopping(0.1f, Timeout);

            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_backend.HasAliveParticlesCount, Is.EqualTo(0));
        }

        [Test]
        public void Reentry_StopInsideBackendPlay_StaysReleased()
        {
            var handle = CreateHandle();
            _backend.OnPlay = _ => handle.Stop();

            var played = handle.Play();
            var callsAfterPlay = _backend.TotalCallCount;
            handle.Play();
            handle.StopAllowingParticlesToFinish();

            Assert.That(played, Is.False);
            Assert.That(handle.IsActive, Is.False);
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_backend.TotalCallCount, Is.EqualTo(callsAfterPlay));
        }

        [Test]
        public void Reentry_DisposeInsideStopEmitting_StaysReleased()
        {
            var handle = CreateHandle();
            Assert.That(handle.Play(), Is.True);
            _backend.OnStopEmitting = _ => handle.Dispose();

            Assert.DoesNotThrow(() => handle.StopAllowingParticlesToFinish());
            var callsAfterStop = _backend.TotalCallCount;
            handle.AdvanceStopping(0.1f, Timeout);
            handle.Play();

            Assert.That(handle.IsActive, Is.False);
            Assert.That(handle.State, Is.EqualTo(VfxHandle.HandleState.Released));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_backend.TotalCallCount, Is.EqualTo(callsAfterStop));
        }

        [Test]
        public void Reentry_GracefulStopInsideReplay_DoesNotWritePlayingBack()
        {
            var handle = CreateHandle();
            Assert.That(handle.Play(), Is.True);
            _backend.OnPlay = _ =>
            {
                _backend.OnPlay = null;
                handle.StopAllowingParticlesToFinish();
            };

            var replayed = handle.Play();

            Assert.That(replayed, Is.False);
            // IsActive は Stopping でも true なので、状態は後続の backend 呼び出しで判定する。
            var playCount = _backend.PlayCount;
            Assert.That(handle.Play(), Is.False);
            Assert.That(_backend.PlayCount, Is.EqualTo(playCount), "Stopping からの Play は backend を呼ばない");
            Assert.That(_backend.StopEmittingCount, Is.EqualTo(1));

            _backend.AliveParticles = false;
            handle.AdvanceStopping(0.1f, Timeout);
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
        }

        [Test]
        public void Reentry_GracefulStopInsideStopEmitting_CallsStopEmittingOnce()
        {
            var handle = CreateHandle();
            Assert.That(handle.Play(), Is.True);
            _backend.OnStopEmitting = _ => handle.StopAllowingParticlesToFinish();

            handle.StopAllowingParticlesToFinish();

            Assert.That(_backend.StopEmittingCount, Is.EqualTo(1));
            Assert.That(handle.State, Is.EqualTo(VfxHandle.HandleState.Stopping));
        }

        // ═══════════════════════════════════════════
        //  AC9 backend 例外（handle 側）
        // ═══════════════════════════════════════════

        [TestCase("Play")]
        [TestCase("StopEmitting")]
        [TestCase("HasAliveParticles")]
        [TestCase("Set")]
        public void BackendThrows_ReleasesHandle_WithOneFailureAndOneDestroy(string faultedOp)
        {
            var handle = CreateHandle();
            _backend.Fault = (op, _) => op == faultedOp ? new InvalidOperationException(op) : null;

            Assert.DoesNotThrow(() =>
            {
                handle.Play();
                handle.SetFloat("p", 1f);
                handle.StopAllowingParticlesToFinish();
                handle.AdvanceStopping(0.1f, Timeout);
            });

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_failures, Has.Count.EqualTo(1));
            Assert.That(_failures[0].Message, Is.EqualTo(faultedOp));
        }

        [Test]
        public void DestroyThrows_NoRetry_OneFailure()
        {
            var handle = CreateHandle();
            _backend.Fault = (op, _) => op == "Destroy" ? new InvalidOperationException(op) : null;

            Assert.DoesNotThrow(() => handle.Stop());
            handle.Stop();
            handle.Dispose();

            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(handle.IsActive, Is.False);
            Assert.That(_failures, Has.Count.EqualTo(1));
        }

        [Test]
        public void PlayThrows_ThenDestroyThrows_LogsOnlyFirstFailure()
        {
            var handle = CreateHandle();
            _backend.Fault = (op, _) => op is "Play" or "Destroy" ? new InvalidOperationException(op) : null;

            Assert.DoesNotThrow(() => handle.Play());

            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(_failures, Has.Count.EqualTo(1));
            Assert.That(_failures[0].Message, Is.EqualTo("Play"));
        }

        [Test]
        public void BackendThrows_OtherHandleKeepsWorking()
        {
            var failing = CreateHandle(1);
            var healthy = CreateHandle(2);
            _backend.Fault = (op, id) => id.Value == 1 && op == "HasAliveParticles"
                ? new InvalidOperationException(op)
                : null;
            failing.Play();
            failing.StopAllowingParticlesToFinish();
            healthy.Play();
            healthy.StopAllowingParticlesToFinish();

            failing.AdvanceStopping(0.1f, Timeout);
            _backend.AliveParticles = false;
            healthy.AdvanceStopping(0.1f, Timeout);

            Assert.That(_backend.DestroyCountOf(new VfxInstanceId(1)), Is.EqualTo(1));
            Assert.That(_backend.DestroyCountOf(new VfxInstanceId(2)), Is.EqualTo(1));
            Assert.That(_failures, Has.Count.EqualTo(1));
        }

        // ═══════════════════════════════════════════
        //  AC12 名前付きパラメータ
        // ═══════════════════════════════════════════

        [Test]
        public void SetParameters_PassNameAndValueThrough()
        {
            var handle = CreateHandle();

            Assert.That(handle.SetFloat("f", 1.5f), Is.True);
            Assert.That(handle.SetInt("i", 3), Is.True);
            Assert.That(handle.SetBool("b", true), Is.True);
            Assert.That(handle.SetVector3("v3", new Vector3(1, 2, 3)), Is.True);
            Assert.That(handle.SetVector4("v4", new Vector4(1, 2, 3, 4)), Is.True);

            Assert.That(_backend.SetCalls, Is.EqualTo(new List<(string, ulong, string, object)>
            {
                ("Float", 1UL, "f", 1.5f),
                ("Int", 1UL, "i", 3),
                ("Bool", 1UL, "b", true),
                ("Vector3", 1UL, "v3", new Vector3(1, 2, 3)),
                ("Vector4", 1UL, "v4", new Vector4(1, 2, 3, 4)),
            }));
        }

        [Test]
        public void SetParameter_BackendFalse_IsReturnedAsIs_NotAFailure()
        {
            _backend.SetResult = false;
            var handle = CreateHandle();

            Assert.That(handle.SetFloat("f", 1f), Is.False);
            Assert.That(handle.SetVector4("v", Vector4.one), Is.False);

            Assert.That(handle.IsActive, Is.True);
            Assert.That(_failures, Is.Empty);
            Assert.That(_backend.DestroyCount, Is.EqualTo(0));
        }

        [Test]
        public void SetParameter_OnReleased_DoesNotCallBackend()
        {
            var handle = CreateHandle();
            handle.Stop();

            Assert.That(handle.SetFloat("f", 1f), Is.False);
            Assert.That(handle.SetBool("b", true), Is.False);

            Assert.That(_backend.SetCount, Is.EqualTo(0));
        }

        [TestCase(null)]
        [TestCase("")]
        public void SetParameter_NullOrEmptyName_IsRejectedWithoutLosingInstance(string? name)
        {
            var handle = CreateHandle();

            Assert.That(handle.SetFloat(name!, 1f), Is.False);
            Assert.That(handle.SetInt(name!, 1), Is.False);

            Assert.That(_backend.SetCount, Is.EqualTo(0));
            Assert.That(handle.IsActive, Is.True);
            Assert.That(_backend.DestroyCount, Is.EqualTo(0));
        }

        [Test]
        public void SetParameter_ReleasedDuringBackendCall_ReturnsFalse()
        {
            var handle = CreateHandle();
            _backend.OnSet = _ => handle.Stop();

            // backend が true を返しても、呼び出し中に回収されていたら false を返す。
            Assert.That(handle.SetFloat("f", 1f), Is.False);

            Assert.That(handle.IsActive, Is.False);
            Assert.That(_backend.SetCount, Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
        }
    }
}
