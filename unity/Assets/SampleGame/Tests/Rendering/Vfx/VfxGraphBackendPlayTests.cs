#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.UpdateSystem.Api;
using OneStarMaker.Tests.Rendering.Vfx;
using SampleGame.Common.Rendering.Vfx;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.TestTools;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// 実 backend の契約（発射しない Create・拒否・id・Destroy の耐性・Set*）、非対応での停止、配置済み VFX の登録・開始・回収、
    /// 停止と回収（生きた coordinator から Tick される host を通す）、時間の領域への追従（Pause・倍率・負荷スパイク）、
    /// 配置先、Framework の composition の存在を Play 中に見る。
    /// 描画は <see cref="VfxGraphRenderPlayTests"/>。
    /// </summary>
    [TestFixture]
    public sealed class VfxGraphBackendPlayTests : VfxPlayTestFixture
    {
        // ═══════════════════════════════════════════
        //  Framework の composition（起動時に作られている）
        // ═══════════════════════════════════════════

        /// <summary>接続（AfterSceneLoad の SceneDirector 生成の直後）までの待ちは共通の SetUp が行う。</summary>
        [Test]
        public void Startup_FrameworkHostExists_TickActive_AndBoundToSceneLifecycle()
        {
            var host = AbstractApplicationInitializer.ActiveVfxRuntimeHost!;
            Assert.That(host.IsTickElementActive, Is.True);
            Assert.That(host.IsSceneLifecycleBound, Is.True);
        }

        // ═══════════════════════════════════════════
        //  非対応・未判定（graphics 不要）
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Unsupported_WarnsOnceFromCtor_SpawnsNothing_AndPlacedVfxStaysDisabled()
        {
            var logger = new FakeVfxLogger();
            var backend = Track(new VfxGraphBackend(UnsupportedFacts));
            var before = CountVisualEffects();

            // ctor の Warning から数える。
            var system = Track(new VfxSystem(backend, logger));
            Assert.That(logger.WarningCount, Is.EqualTo(1));
            Assert.That(logger.TotalCount, Is.EqualTo(1));

            for (var i = 0; i < 10; i++)
            {
                IVfxHandle? handle = null;
                Assert.DoesNotThrow(() => handle = system.Spawn(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity), AssetOwner.App));
                Assert.That(handle!.IsActive, Is.False);
                Assert.That(handle.Play(), Is.False);
            }

            Assert.That(logger.TotalCount, Is.EqualTo(1), "Spawn の間にログは増えない");
            Assert.That(CountVisualEffects() - before, Is.EqualTo(0));

            var placed = InstantiatePlaced();
            var adopted = system.Adopt(placed, AssetOwner.App);
            Assert.That(adopted.IsActive, Is.False, "非対応では登録しない");
            yield return null;
            yield return null;
            Assert.That(placed.enabled, Is.False);
            Assert.That(logger.TotalCount, Is.EqualTo(1), "登録の間にログは増えない");
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>marker は自分では有効化しない（Awake は何もしない）。登録しない限り無効のまま。</summary>
        [UnityTest]
        public IEnumerator PlacedVfx_NotAdopted_StaysDisabled()
        {
            AssumeGraphicsSupported();
            CreateHost(RealFacts);

            var placed = InstantiatePlaced();
            yield return null;
            yield return null;

            Assert.That(placed.enabled, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// 登録は有効化しない。最初の反映点（LateUpdate）で有効になり、初期イベントで粒子が出る。
        /// 回収は無効化だけで GameObject を壊さず、回収の後に再び有効にならない。
        /// </summary>
        [UnityTest]
        public IEnumerator Supported_AdoptedPlacedVfx_EnabledAtReflection_ReleaseDisablesWithoutDestroy()
        {
            AssumeGraphicsSupported();
            var host = CreateHost(RealFacts);
            var placed = InstantiatePlaced();

            var handle = host.System.Adopt(placed, AssetOwner.App);
            Assert.That(handle.IsActive, Is.True);
            Assert.That(placed.enabled, Is.False, "登録だけでは有効にしない");

            yield return null;
            Assert.That(placed.enabled, Is.True, "最初の反映点で有効になる");
            var alive = false;
            for (var frame = 0; frame < ParticleFrameLimit && !alive; frame++)
            {
                yield return null;
                alive = placed.HasAnySystemAwake() || placed.aliveParticleCount > 0;
            }

            Assert.That(alive, Is.True, "有効化の初期イベントで粒子が出る");

            handle.Stop();
            Assert.That(placed.enabled, Is.False, "回収は無効化");
            yield return null;
            yield return null;
            Assert.That(placed != null, Is.True, "回収は GameObject を壊さない");
            Assert.That(placed.enabled, Is.False, "回収の後に再び有効にならない");
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// 回収の後に同じ component を新しく登録すると、前の登録が止まった領域で書いた pause が残っていても、
        /// 契約の初期状態（止まっていない、倍率 1）から自動再生が進む。順序: 止まった領域で Adopt → 反映点で pause が
        /// 書かれる → 回収（無効化だけ）→ Resume → 同じ component を Adopt。
        /// </summary>
        [UnityTest]
        public IEnumerator Supported_ReadoptAfterReleaseInPausedDomain_StartsFromInitialPlayback()
        {
            AssumeGraphicsSupported();
            var host = CreateHost(RealFacts);
            var placed = InstantiatePlaced(Immortal);
            var layer = GameplayLayer;
            layer.IsPaused = true;

            var first = host.System.Adopt(placed, AssetOwner.App);
            Assert.That(first.IsActive, Is.True);
            yield return null;
            Assert.That(placed.pause, Is.True, "前提: 止まった領域の反映点で pause が書かれた");
            first.Stop();
            Assert.That(placed.enabled, Is.False, "前提: 回収で無効になった");

            layer.IsPaused = false;
            yield return null;
            var second = host.System.Adopt(placed, AssetOwner.App);
            Assert.That(second.IsActive, Is.True, "回収の後の新しい登録は受理される");
            Assert.That(placed.pause, Is.False, "登録で pause が初期状態に戻る");
            Assert.That(placed.playRate, Is.EqualTo(1f), "登録で playRate が初期状態に戻る");

            var alive = false;
            for (var frame = 0; frame < ParticleFrameLimit && !alive; frame++)
            {
                yield return null;
                alive = placed.aliveParticleCount > 0;
            }

            Assert.That(placed.enabled, Is.True, "止まっていない反映点で有効になる");
            Assert.That(alive, Is.True, "再登録の後に自動再生が進む");
            second.Stop();
            LogAssert.NoUnexpectedReceived();
        }

        private VisualEffect InstantiatePlaced(VisualEffectAsset? asset = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlacedPrefabPath);
            Assert.That(prefab != null, Is.True, "fixture の prefab が無い");
            Assert.That(prefab!.GetComponent<VisualEffect>().enabled, Is.False, "前提: prefab の VisualEffect は無効のまま保存されている");
            var instance = Object.Instantiate(prefab);
            Objects.Add(instance);
            var effect = instance.GetComponent<VisualEffect>();
            if (asset != null)
            {
                // 無効のまま差し替えるので、ここでは初期イベントは送られない。
                effect.visualEffectAsset = asset;
            }

            return effect;
        }

        // ═══════════════════════════════════════════
        //  時間の領域（AC6・AC7）
        // ═══════════════════════════════════════════

        // AC6 の止まっている間のフレーム数。reap しない fixture の aliveParticleCount は同じフレームで変わる（実測）ので、
        // 除外するフレームは無い。
        private const int PausedFrames = 30;

        /// <summary>
        /// AC6（動的）: 一定の spawn rate で寿命の無い粒子を出す fixture を再生中に Gameplay Layer を止めると、
        /// 止まっている N フレームの間 aliveParticleCount が増えず、Resume の後に増える。
        /// </summary>
        [UnityTest]
        public IEnumerator AC6_Dynamic_PauseStopsAndResumeRestarts()
        {
            AssumeGraphicsSupported();
            var backend = new VfxGraphBackend();
            var host = CreateHost(backend);
            var handle = host.System.Spawn(new VfxSpawnRequest(Immortal, Vector3.zero, Quaternion.identity), AssetOwner.App);
            var effect = backend.FindForTest(IdOf(handle))!;
            handle.Play();

            yield return AssertPauseStopsAndResumeRestarts(effect, "dynamic");
        }

        /// <summary>AC6（配置済み）: 同じ操作で、登録した配置済み VFX も止まり、再開する。</summary>
        [UnityTest]
        public IEnumerator AC6_Placed_PauseStopsAndResumeRestarts()
        {
            AssumeGraphicsSupported();
            var host = CreateHost(RealFacts);
            var placed = InstantiatePlaced(Immortal);
            Assert.That(host.System.Adopt(placed, AssetOwner.App).IsActive, Is.True);

            yield return AssertPauseStopsAndResumeRestarts(placed, "placed");
        }

        private IEnumerator AssertPauseStopsAndResumeRestarts(VisualEffect effect, string label)
        {
            var started = false;
            for (var frame = 0; frame < ParticleFrameLimit && !started; frame++)
            {
                yield return null;
                started = effect.aliveParticleCount > 0;
            }

            Assert.That(started, Is.True, $"前提: {label} の粒子が出ている");

            // coroutine は Update の後・LateUpdate の前に再開する。ここで止めると、このフレームの反映点（LateUpdate）で
            // pause が書かれ、このフレームの VFX の更新から止まる。
            var layer = GameplayLayer;
            layer.IsPaused = true;
            var pausedCount = effect.aliveParticleCount;
            var counts = new List<int>();
            for (var frame = 0; frame < PausedFrames; frame++)
            {
                yield return null;
                counts.Add(effect.aliveParticleCount);
            }

            TestContext.Out.WriteLine($"{label} pausedAt={pausedCount} duringPause={string.Join(",", counts)}");
            Assert.That(counts, Has.All.EqualTo(pausedCount), $"{label}: 止まっている間に粒子が増えた");

            layer.IsPaused = false;
            var resumed = false;
            var resumeFrames = 0;
            for (; resumeFrames < ParticleFrameLimit && !resumed; resumeFrames++)
            {
                yield return null;
                resumed = effect.aliveParticleCount > pausedCount;
            }

            TestContext.Out.WriteLine($"{label} resumedAfterFrames={resumeFrames} alive={effect.aliveParticleCount}");
            Assert.That(resumed, Is.True, $"{label}: Resume の後に粒子が増えない");
            LogAssert.NoUnexpectedReceived();
        }

        // AC7 で比べるフレーム数と、引き延ばすフレーム。0.1・0.2 秒は VFX Graph の上限の既定（0.05 秒）を超えるが
        // Time.maximumDeltaTime では頭打ちにならない長さ、0.5 秒は Time.maximumDeltaTime を超える負荷スパイク。
        private const int MeasuredFrames = 60;
        private static readonly (int Frame, double Seconds)[] StretchedFrames = { (15, 0.1), (30, 0.2), (45, 0.5) };

        /// <summary>
        /// AC7: Gameplay Layer に置いた test の要素が受け取った DeltaTime と、VFX の spawn system が進んだ時間
        /// （totalTime の増分）が、フレームごとと列の和の両方で一致する。(Time.timeScale, Layer の TimeScale) の 3 組の
        /// それぞれで、VFX Graph の上限の既定を超えるが頭打ちにならないフレームと、Time.maximumDeltaTime を超える
        /// 負荷スパイクのフレームを含む列で比べる。
        /// </summary>
        [UnityTest]
        public IEnumerator AC7_EffectTimeMatchesGameplayTime_IncludingLoadSpike()
        {
            AssumeGraphicsSupported();
            var coordinator = UpdateSystemRuntime.Coordinator!;
            var probe = new GameplayDeltaProbe();
            Assert.That(coordinator.RegisterElement(UpdateLayerIds.Gameplay, probe, UpdateLayerIds.GameplayLayerOrder), Is.True);
            coordinator.ActivatePendingRegistrations();
            try
            {
                var conditions = new[] { (0.5f, 0.5f), (1f, 2f), (2f, 0.5f) };
                foreach (var (timeScale, layerScale) in conditions)
                {
                    yield return MeasureOneCondition(probe, unscaled: false, timeScale, layerScale);
                }
            }
            finally
            {
                coordinator.UnregisterElement(probe);
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// AC7（unscaled）: unscaled の要求は Time.timeScale に依らず、1 フレームに
        /// <c>min(unscaledDeltaTime, Time.maximumDeltaTime)</c> だけ進む。Time.timeScale = 0.5 で、
        /// u ≈ 0.5 秒のフレームの増分が Time.maximumDeltaTime になる。
        /// </summary>
        [UnityTest]
        public IEnumerator AC7_UnscaledEffectTime_IsCappedAtMaximumDeltaTime()
        {
            AssumeGraphicsSupported();
            // unscaled の領域の時間は probe から取らないが、引き延ばしは gameplay の Update で行うので probe を置く。
            var coordinator = UpdateSystemRuntime.Coordinator!;
            var probe = new GameplayDeltaProbe();
            Assert.That(coordinator.RegisterElement(UpdateLayerIds.Gameplay, probe, UpdateLayerIds.GameplayLayerOrder), Is.True);
            coordinator.ActivatePendingRegistrations();
            try
            {
                yield return MeasureOneCondition(probe, unscaled: true, timeScale: 0.5f, layerScale: 1f);
            }
            finally
            {
                coordinator.UnregisterElement(probe);
            }

            LogAssert.NoUnexpectedReceived();
        }

        /// <param name="probe">Gameplay Layer の要素。引き延ばしを行い、gameplay の要求では領域の時間の出所になる。</param>
        /// <param name="unscaled">true なら unscaled の要求で、領域の時間は Unity の Time から求める。</param>
        private IEnumerator MeasureOneCondition(GameplayDeltaProbe probe, bool unscaled, float timeScale, float layerScale)
        {
            var backend = new VfxGraphBackend();
            var host = CreateHost(backend);
            var request = new VfxSpawnRequest(Immortal, Vector3.zero, Quaternion.identity);
            var handle = host.System.Spawn(unscaled ? request.WithUnscaledTime() : request, AssetOwner.App);
            var effect = backend.FindForTest(IdOf(handle))!;
            var maximumDeltaTime = Time.maximumDeltaTime;
            Assert.That(maximumDeltaTime, Is.GreaterThan(0f), "前提: Time.maximumDeltaTime が 0 でない");
            Assert.That(VFXManager.maxDeltaTime, Is.EqualTo(maximumDeltaTime), "前提: backend が VFX Graph の上限を揃えている");
            SetTimeScale(timeScale);
            GameplayLayer.SetTimeScale(layerScale);
            handle.Play();
            var spawnNames = new List<string>();

            // Play の直後は長いフレームが出ることがあるので、最初の反映点から 5 フレームを外して測る。
            for (var frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            effect.GetSpawnSystemNames(spawnNames);
            Assert.That(spawnNames, Is.Not.Empty, "前提: fixture に spawn system がある");
            var spawnName = spawnNames[0];

            // 行の対応付けは coroutine の再開時点に頼らない。coroutine が VFX の更新（PostLateUpdate.VFXUpdate）の前と
            // 後のどちらで再開するかは仮定せず、VFX の更新の直後に差し込んだ記録点で、そのフレームの番号・
            // unscaledDeltaTime・領域の時間・totalTime を取り、同じフレーム番号どうしで比べる。再開時点の前後は
            // 記録して出力する（発見 C 1 巡目で、増分が期待より 1 行早く出た原因を区別するため）。
            //
            // 引き延ばしは coroutine ではなく、次のフレームの gameplay の要素の Update の中（同じフレームの VFX の更新より前。
            // 実際の負荷スパイクと同じ位置）で行う。coroutine は VFX の更新の後に再開する（発見 C 2 巡目の実測）ので、
            // そこで引き延ばすと、VFX Graph が Unity の Time で進んでも、自分の更新どうしの実時間で進んでも、増分が
            // 同じフレームに入って区別できない。Update の中なら、Unity の Time で進むときは引き延ばしたフレームの次の
            // フレームの delta と増分に、自分で測るときは引き延ばしたフレームの増分に入るので、後者は不一致で落ちる。
            var recorder = Track(new VfxUpdateRecorder(() => effect.GetSpawnSystemInfo(spawnName).totalTime,
                unscaled ? null : probe, maximumDeltaTime));
            probe.SpinFrames.Clear();
            var resumesAfterVfxUpdate = 0;
            try
            {
                // 最初の記録（増分の起点）が取れるまで進める。
                yield return null;
                for (var frame = 0; frame < MeasuredFrames; frame++)
                {
                    var stretch = Array.FindIndex(StretchedFrames, s => s.Frame == frame);
                    if (stretch >= 0)
                    {
                        probe.RequestSpin(StretchedFrames[stretch].Seconds);
                    }

                    yield return null;
                    Assert.That(VFXManager.maxDeltaTime, Is.EqualTo(maximumDeltaTime), "測定中に VFX Graph の上限が変わった");
                    if (recorder.LastFrame == Time.frameCount)
                    {
                        resumesAfterVfxUpdate++;
                    }
                }

                // 最後の引き延ばしの次のフレームまで、記録点が取るまで進める。
                yield return null;
                yield return null;
            }
            finally
            {
                probe.RequestSpin(0);
                recorder.Dispose();
            }

            var samples = recorder.Samples;
            var spinFrames = probe.SpinFrames;
            Assert.That(spinFrames, Has.Count.EqualTo(StretchedFrames.Length), "前提: gameplay の Update で全ての引き延ばしが行われた");

            // 引き延ばしはそのフレームの開始より後なので、次のフレームの unscaledDeltaTime に入る。
            var stretchedUnscaled = new List<float>();
            foreach (var spinFrame in spinFrames)
            {
                var next = samples.FindIndex(sample => sample.Frame == spinFrame + 1);
                Assert.That(next, Is.GreaterThanOrEqualTo(0), $"前提: 引き延ばした f{spinFrame} の次のフレームが記録された");
                stretchedUnscaled.Add(samples[next].UnscaledDeltaTime);
            }

            var expectedSum = 0f;
            var effectSum = 0f;
            var rows = new List<string>();
            var mismatches = new List<string>();
            var cappedIncrement = float.NaN;
            for (var i = 1; i < samples.Count; i++)
            {
                var previous = samples[i - 1];
                var current = samples[i];
                var increment = current.TotalTime - previous.TotalTime;
                var expected = current.DomainDelta;
                rows.Add($"f{current.Frame}:{current.UnscaledDeltaTime:F4}/{expected:F4}/{increment:F4}");
                if (current.Frame != previous.Frame + 1)
                {
                    mismatches.Add($"f{current.Frame} 記録点が前のフレーム f{previous.Frame} から連続していない");
                    continue;
                }

                if (float.IsNaN(expected))
                {
                    mismatches.Add($"f{current.Frame} gameplay の要素がこのフレームの Update を受け取っていない");
                    continue;
                }

                if (Math.Abs(increment - expected) > Math.Max(0.005f, expected * 0.02f))
                {
                    mismatches.Add($"f{current.Frame} u={current.UnscaledDeltaTime:F4} expected={expected:F5} effect={increment:F5}");
                }

                if (current.UnscaledDeltaTime > maximumDeltaTime)
                {
                    cappedIncrement = increment;
                }

                expectedSum += expected;
                effectSum += increment;
            }

            // 引き延ばしたフレーム F とその次のフレームの値。増分が F で大きく u が F+1 で大きければ、VFX Graph が
            // Unity の Time ではなく自分の更新どうしの実時間で進んでいる（引き延ばしは F の VFX の更新より前に起きる）。
            var spins = new List<string>();
            foreach (var spinFrame in spinFrames)
            {
                spins.Add($"spin@f{spinFrame}:{DescribeFrame(samples, spinFrame)};{DescribeFrame(samples, spinFrame + 1)}");
            }

            var sumTolerance = expectedSum * 0.01f;
            TestContext.Out.WriteLine(
                $"unscaled={unscaled} timeScale={timeScale} layerScale={layerScale} expected={expectedSum:F5} " +
                $"effect={effectSum:F5} sumTolerance={sumTolerance:F5} maximumDeltaTime={maximumDeltaTime} " +
                $"vfxMaxDeltaTime={VFXManager.maxDeltaTime} resumesAfterVfxUpdate={resumesAfterVfxUpdate}/{MeasuredFrames} " +
                $"stretched(u)={string.Join(",", stretchedUnscaled)} spins={string.Join(" ", spins)} " +
                $"frames(u/expected/effect)={string.Join(",", rows)}");
            Assert.That(samples.Count, Is.GreaterThanOrEqualTo(MeasuredFrames), "前提: 記録点が測定の全フレームで呼ばれた");

            // 前提: 引き延ばしたフレームが狙った区間に入っている（VFX Graph の上限の既定 0.05 < u ≤ maximumDeltaTime が
            // 2 つ、maximumDeltaTime を超えるものが 1 つ）。
            Assert.That(stretchedUnscaled, Has.Count.EqualTo(StretchedFrames.Length));
            Assert.That(stretchedUnscaled[0], Is.GreaterThan(0.05f).And.LessThanOrEqualTo(maximumDeltaTime));
            Assert.That(stretchedUnscaled[1], Is.GreaterThan(0.05f).And.LessThanOrEqualTo(maximumDeltaTime));
            Assert.That(stretchedUnscaled[2], Is.GreaterThan(maximumDeltaTime));

            var label = $"(unscaled {unscaled}, timeScale {timeScale}, layer {layerScale})";
            Assert.That(mismatches, Is.Empty, $"{label} でフレームの VFX の時間が領域の時間と一致しない");
            Assert.That(effectSum, Is.EqualTo(expectedSum).Within(sumTolerance), $"{label} で VFX の時間の和が領域の時間の和と一致しない");
            if (unscaled)
            {
                Assert.That(cappedIncrement, Is.EqualTo(maximumDeltaTime).Within(Math.Max(0.005f, maximumDeltaTime * 0.02f)),
                    "unscaled の VFX が maximumDeltaTime を超えるフレームで maximumDeltaTime だけ進まない");
            }

            handle.Stop();
            SetTimeScale(1f);
            GameplayLayer.SetTimeScale(1f);
        }

        private static string DescribeFrame(List<VfxUpdateRecorder.Sample> samples, int frame)
        {
            var index = samples.FindIndex(s => s.Frame == frame);
            if (index <= 0)
            {
                return $"f{frame}=(未記録)";
            }

            var sample = samples[index];
            return $"f{frame}=u {sample.UnscaledDeltaTime:F4} expected {sample.DomainDelta:F4} effect {sample.TotalTime - samples[index - 1].TotalTime:F4}";
        }

        /// <summary>Gameplay Layer の要素が受け取った DeltaTime と、それを受け取ったフレーム（AC7 の gameplay 側の時間）。</summary>
        private sealed class GameplayDeltaProbe : IUpdateElement
        {
            public float Last { get; private set; }

            public int Frame { get; private set; } = -1;

            public void OnElementStart()
            {
            }

            /// <summary>Update の中で引き延ばしたフレームの番号。</summary>
            public List<int> SpinFrames { get; } = new();

            private double _pendingSpinSeconds;

            /// <summary>次の Update の中で、指定秒だけ main thread を占有する（0 で取り消す）。</summary>
            public void RequestSpin(double seconds) => _pendingSpinSeconds = seconds;

            public void OnElementUpdate(in UpdateFrameContext context)
            {
                Last = context.DeltaTime;
                Frame = Time.frameCount;
                if (_pendingSpinSeconds > 0)
                {
                    var seconds = _pendingSpinSeconds;
                    _pendingSpinSeconds = 0;
                    SpinFrames.Add(Frame);
                    SpinMainThread(seconds);
                }
            }

            public void OnElementLateUpdate(in UpdateFrameContext context)
            {
            }
        }

        /// <summary>
        /// PlayerLoop の <c>PostLateUpdate.VFXUpdate</c> の直後に記録点を差し込み、VFX がそのフレームを更新した後の
        /// totalTime と、同じフレームの unscaledDeltaTime・領域の時間を取る。Dispose で差し込む前の PlayerLoop に戻す
        /// （テストが途中で失敗しても TearDown の Dispose で戻る）。
        /// </summary>
        private sealed class VfxUpdateRecorder : IDisposable
        {
            private readonly Func<float> _readTotalTime;
            private readonly GameplayDeltaProbe? _probe;
            private readonly float _maximumDeltaTime;
            private PlayerLoopSystem? _saved;

            public VfxUpdateRecorder(Func<float> readTotalTime, GameplayDeltaProbe? probe, float maximumDeltaTime)
            {
                _readTotalTime = readTotalTime;
                _probe = probe;
                _maximumDeltaTime = maximumDeltaTime;
                var root = PlayerLoop.GetCurrentPlayerLoop();
                var inserted = false;
                var patched = Insert(root, ref inserted);
                Assert.That(inserted, Is.True, "前提: PlayerLoop に PostLateUpdate.VFXUpdate がある");
                _saved = root;
                PlayerLoop.SetPlayerLoop(patched);
            }

            public List<Sample> Samples { get; } = new();

            /// <summary>記録点が最後に呼ばれたフレーム（呼ばれていなければ -1）。</summary>
            public int LastFrame => Samples.Count > 0 ? Samples[Samples.Count - 1].Frame : -1;

            public void Dispose()
            {
                if (_saved is { } saved)
                {
                    _saved = null;
                    PlayerLoop.SetPlayerLoop(saved);
                }
            }

            private void Record()
            {
                // 領域の時間: gameplay は要素がこのフレームに受け取った DeltaTime（受け取っていなければ NaN）、unscaled は
                // min(unscaledDeltaTime, maximumDeltaTime)（Framework の定義を Unity の Time から独立に求める）。
                var frame = Time.frameCount;
                var domain = _probe == null
                    ? Math.Min(Time.unscaledDeltaTime, _maximumDeltaTime)
                    : _probe.Frame == frame ? _probe.Last : float.NaN;
                Samples.Add(new Sample(frame, Time.unscaledDeltaTime, domain, _readTotalTime()));
            }

            private PlayerLoopSystem Insert(PlayerLoopSystem system, ref bool inserted)
            {
                if (system.subSystemList == null)
                {
                    return system;
                }

                var list = new List<PlayerLoopSystem>(system.subSystemList.Length + 1);
                foreach (var child in system.subSystemList)
                {
                    list.Add(Insert(child, ref inserted));
                    if (system.type == typeof(PostLateUpdate) && child.type == typeof(PostLateUpdate.VFXUpdate))
                    {
                        list.Add(new PlayerLoopSystem { type = typeof(VfxUpdateRecorder), updateDelegate = Record });
                        inserted = true;
                    }
                }

                system.subSystemList = list.ToArray();
                return system;
            }

            public readonly struct Sample
            {
                public Sample(int frame, float unscaledDeltaTime, float domainDelta, float totalTime)
                {
                    Frame = frame;
                    UnscaledDeltaTime = unscaledDeltaTime;
                    DomainDelta = domainDelta;
                    TotalTime = totalTime;
                }

                public int Frame { get; }

                public float UnscaledDeltaTime { get; }

                public float DomainDelta { get; }

                public float TotalTime { get; }
            }
        }

        // ═══════════════════════════════════════════
        //  backend の契約
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Create_DoesNotEmitUntilPlay()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            var id = backend.Create(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity));
            Assert.That(id.IsValid, Is.True);
            var effect = backend.FindForTest(id)!;
            // 計測値（合否には使わない）: initialEventName の読み戻しと、粒子の無い system の awake の状態。
            // 読み戻しが空でも抑止が効いているとは限らない（asset の後に書くと空を返しつつ OnPlay が送られた）ので、
            // 抑止の確認は下の粒子の有無で行う。
            TestContext.Out.WriteLine(
                $"initialEventName='{effect.initialEventName}' hasAnySystemAwakeAtCreate={effect.HasAnySystemAwake()}");

            for (var frame = 0; frame < 120; frame++)
            {
                yield return null;
                Assert.That(backend.HasAliveParticles(id), Is.False,
                    $"Create だけでは発射しない（frame {frame}、aliveParticleCount={effect.aliveParticleCount}）");
            }

            Assert.That(backend.Play(id), Is.True);
            var alive = false;
            for (var frame = 0; frame < ParticleFrameLimit && !alive; frame++)
            {
                yield return null;
                alive = backend.HasAliveParticles(id);
            }

            Assert.That(alive, Is.True, "Play の後、上限フレーム内に粒子が出る");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Create_NonVfxSource_ReturnsInvalidWithoutCreatingObjects()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            var before = CountVisualEffects();
            var notAnAsset = NewObject("NotAVfxAsset");

            var id = backend.Create(new VfxSpawnRequest(notAnAsset, Vector3.zero, Quaternion.identity));

            Assert.That(id.IsValid, Is.False);
            Assert.That(CountVisualEffects(), Is.EqualTo(before));
            Assert.That(backend.RootForTest == null, Is.True, "拒否は GameObject を作る前に起きる");
        }

        [UnityTest]
        public IEnumerator Ids_AreNeverReusedAcross1000CreateDestroy()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            var ids = new HashSet<ulong>();
            for (var i = 0; i < 1000; i++)
            {
                var id = backend.Create(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity));
                Assert.That(ids.Add(id.Value), Is.True, "id の重複: " + id.Value);
                backend.Destroy(id);
            }

            yield return null;
            Assert.That(ids.Count, Is.EqualTo(1000));
        }

        [UnityTest]
        public IEnumerator Destroy_IsSafeForUnknownRepeatedExternallyDestroyedAndLostSource()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            Assert.DoesNotThrow(() => backend.Destroy(new VfxInstanceId(987654321)), "未知の id");

            var twice = backend.Create(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity));
            backend.Destroy(twice);
            Assert.DoesNotThrow(() => backend.Destroy(twice), "Destroy 済みの id");

            var external = backend.Create(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity));
            Object.Destroy(backend.FindForTest(external)!.gameObject);
            yield return null;
            Assert.DoesNotThrow(() => backend.Destroy(external), "外から破棄済みの GameObject");

            var sourceCopy = Object.Instantiate(ShortLived);
            var lostSource = backend.Create(new VfxSpawnRequest(sourceCopy, Vector3.zero, Quaternion.identity));
            Assert.That(lostSource.IsValid, Is.True, "前提: 複製した source から作れた");
            Object.Destroy(sourceCopy);
            yield return null;
            Assert.DoesNotThrow(() => backend.Destroy(lostSource), "source を破棄した後");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Destroy_WithLiveGameObject_DisablesAndDetachesSourceSynchronously()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            var id = backend.Create(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity));
            var effect = backend.FindForTest(id)!;

            backend.Destroy(id);

            Assert.That(effect != null, Is.True, "前提: 破棄はフレーム末なので、まだ存在する");
            Assert.That(effect!.enabled, Is.False);
            Assert.That(effect.visualEffectAsset == null, Is.True, "Destroy から戻った時点で source を参照しない");
        }

        [UnityTest]
        public IEnumerator SetParameters_WriteExposedProperties_AndRejectUnknownMismatchedAndDestroyed()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            var id = backend.Create(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity));
            var effect = backend.FindForTest(id)!;
            Assert.That(effect.HasFloat("FloatProp") && effect.HasInt("IntProp") && effect.HasBool("BoolProp")
                        && effect.HasVector3("Vector3Prop") && effect.HasVector4("Vector4Prop"), Is.True,
                "前提: fixture に Exposed Property が 5 型ある");

            Assert.That(backend.SetFloat(id, "FloatProp", 2.5f), Is.True);
            Assert.That(backend.SetInt(id, "IntProp", 7), Is.True);
            Assert.That(backend.SetBool(id, "BoolProp", true), Is.True);
            Assert.That(backend.SetVector3(id, "Vector3Prop", new Vector3(1f, 2f, 3f)), Is.True);
            Assert.That(backend.SetVector4(id, "Vector4Prop", new Vector4(1f, 2f, 3f, 4f)), Is.True);
            Assert.That(effect.GetFloat("FloatProp"), Is.EqualTo(2.5f));
            Assert.That(effect.GetInt("IntProp"), Is.EqualTo(7));
            Assert.That(effect.GetBool("BoolProp"), Is.True);
            Assert.That(effect.GetVector3("Vector3Prop"), Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(effect.GetVector4("Vector4Prop"), Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));

            Assert.That(backend.SetFloat(id, "NoSuchProp", 1f), Is.False, "未定義の名前");
            Assert.That(backend.SetFloat(id, "IntProp", 1f), Is.False, "型の合わない名前");
            Assert.That(backend.SetVector4(id, "Vector3Prop", Vector4.one), Is.False, "型の合わない名前");

            Object.Destroy(effect.gameObject);
            yield return null;
            Assert.That(backend.SetFloat(id, "FloatProp", 1f), Is.False, "外から破棄済みの instance");
            LogAssert.NoUnexpectedReceived();
        }

        // ═══════════════════════════════════════════
        //  停止と回収（Framework の host を通して生きた coordinator から Tick される）
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator ImmediateStop_DestroysGameObjectByNextFrame()
        {
            AssumeGraphicsSupported();
            var backend = new VfxGraphBackend();
            var host = CreateHost(backend);
            var handle = host.System.Spawn(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity), AssetOwner.App);
            var go = backend.FindForTest(IdOf(handle))!.gameObject;
            handle.Play();
            yield return null;

            handle.Stop();
            yield return null;

            Assert.That(go == null, Is.True);
        }

        [UnityTest]
        public IEnumerator StopAllowingParticlesToFinish_DestroysAfterParticlesDie()
        {
            AssumeGraphicsSupported();
            var backend = new VfxGraphBackend();
            var host = CreateHost(backend);
            var handle = host.System.Spawn(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity), AssetOwner.App);
            var id = IdOf(handle);
            var go = backend.FindForTest(id)!.gameObject;
            handle.Play();

            var sawAlive = false;
            for (var frame = 0; frame < ParticleFrameLimit && !sawAlive; frame++)
            {
                yield return null;
                sawAlive = backend.HasAliveParticles(id);
            }

            Assert.That(sawAlive, Is.True, "前提: 粒子が出ている");
            handle.StopAllowingParticlesToFinish();
            for (var frame = 0; frame < ParticleFrameLimit && go != null; frame++)
            {
                yield return null;
            }

            Assert.That(go == null, Is.True, "粒子が消えた後、上限フレーム内に破棄される");
            Assert.That(handle.IsActive, Is.False);
        }

        /// <summary>粒子が消えない fixture は timeout で破棄される。経過実時間が timeout 以上であること（即 timeout の偽合格を除く）。</summary>
        [UnityTest]
        public IEnumerator StopAllowingParticlesToFinish_ImmortalParticles_DestroyedByTimeout()
        {
            AssumeGraphicsSupported();
            const float timeout = 0.5f;
            var backend = new VfxGraphBackend();
            var host = CreateHost(backend, timeout);
            var handle = host.System.Spawn(new VfxSpawnRequest(Immortal, Vector3.zero, Quaternion.identity), AssetOwner.App);
            var id = IdOf(handle);
            var go = backend.FindForTest(id)!.gameObject;
            handle.Play();

            var sawAlive = false;
            for (var frame = 0; frame < ParticleFrameLimit && !sawAlive; frame++)
            {
                yield return null;
                sawAlive = backend.HasAliveParticles(id);
            }

            Assert.That(sawAlive, Is.True, "前提: 粒子が出ている");
            // 経過はフレームの時計（unscaled time）で測る。消え切り待ちは反映点ごとに領域の時間（Gameplay、
            // Time.timeScale = 1 なので頭打ちのフレーム以外は unscaled delta と同じ）を足す規則で、delta は直前のフレームの
            // 開始時刻からの差なので、Stop の後の最初の反映点には呼び出しより前の時間が入る。呼び出しの瞬間の実時間から
            // 測ると最大 1 フレーム分短く見えるが、フレームの時計なら反映点が足した delta の合計と一致する
            // （頭打ちのフレームでは足す量が少ないので、経過はむしろ長くなる）。最初の反映点で即 timeout する実装は
            // 経過 1 フレーム分しかないので、これでも落ちる。
            var stoppedAt = Time.unscaledTimeAsDouble;
            handle.StopAllowingParticlesToFinish();
            for (var frame = 0; frame < ParticleFrameLimit * 4 && go != null; frame++)
            {
                yield return null;
            }

            var elapsed = Time.unscaledTimeAsDouble - stoppedAt;
            TestContext.Out.WriteLine($"timeout={timeout} elapsedUnscaled={elapsed:F4}");
            Assert.That(go == null, Is.True, "上限フレーム内に timeout で破棄される");
            Assert.That(elapsed, Is.GreaterThanOrEqualTo(timeout * 0.99), "経過実時間が timeout 未満で破棄された");
        }

        [UnityTest]
        public IEnumerator BindOwnerDestroyed_InstanceDestroyedByNextFrame()
        {
            AssumeGraphicsSupported();
            var backend = new VfxGraphBackend();
            var host = CreateHost(backend);
            var owner = NewObject("VfxOwner");
            var handle = host.System.Spawn(new VfxSpawnRequest(ShortLived, Vector3.zero, Quaternion.identity), AssetOwner.Bind(owner));
            var go = backend.FindForTest(IdOf(handle))!.gameObject;
            handle.Play();
            yield return null;

            Object.Destroy(owner);
            yield return null;
            yield return null;

            Assert.That(handle.IsActive, Is.False);
            Assert.That(go == null, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        // ═══════════════════════════════════════════
        //  配置先
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Placement_WorldTransformMatchesRequest_WithAndWithoutParent_AndDisposeCleansUp()
        {
            AssumeGraphicsSupported();
            var backend = Track(new VfxGraphBackend());
            var position = new Vector3(1f, 2f, 3f);
            var rotation = Quaternion.Euler(10f, 20f, 30f);

            var loose = backend.Create(new VfxSpawnRequest(ShortLived, position, rotation));
            var looseGo = backend.FindForTest(loose)!.gameObject;
            var root = backend.RootForTest!;
            Assert.That(looseGo.transform.parent, Is.SameAs(root.transform));
            Assert.That(root.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            AssertWorldTransform(looseGo.transform, position, rotation);

            var parent = NewObject("VfxParent");
            parent.transform.SetPositionAndRotation(new Vector3(-4f, 5f, 6f), Quaternion.Euler(0f, 90f, 45f));
            var child = backend.Create(new VfxSpawnRequest(ShortLived, position, rotation, parent.transform));
            var childGo = backend.FindForTest(child)!.gameObject;
            Assert.That(childGo.transform.parent, Is.SameAs(parent.transform));
            AssertWorldTransform(childGo.transform, position, rotation);

            backend.Dispose();
            yield return null;
            Assert.That(root == null, Is.True, "Dispose で root が破棄される");
            Assert.That(looseGo == null && childGo == null, Is.True, "Dispose で残った instance が破棄される");
        }

        private static void AssertWorldTransform(Transform transform, Vector3 position, Quaternion rotation)
        {
            Assert.That(Vector3.Distance(transform.position, position), Is.LessThan(1e-4f));
            Assert.That(Quaternion.Angle(transform.rotation, rotation), Is.LessThan(0.01f));
        }
    }
}
