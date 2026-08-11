#nullable enable

using System;
using Cysharp.Text;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Foundation.UpdateSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Debug
{
    /// <summary>
    /// profiler テレメトリを毎フレーム送出する常駐 Element。
    ///
    /// <para>
    /// 送出は元々 <see cref="DebugProfilerView"/> の <c>Update()</c> にあったが、
    /// あの View は uGUI Canvas が無いため一度も生成されず、結果として
    /// ProfilerSummary / GcSpike / UiCost が Unity から一度も出ていなかった。
    /// MonoBehaviour を持たず UpdateSystem の Element として常駐する形は
    /// <c>CameraSystemUpdateElement</c> の前例をなぞっている。
    /// </para>
    /// </summary>
    public sealed class ProfilerTelemetryEmitter : IUpdateElement, IDisposable
    {
        /// <summary>Profiler 専用 Layer。Runtime の UpdateLayerIds には足さない（Runtime は Debug を知らない）。</summary>
        public const string LayerId = "Profiler";

        /// <summary>Camera=50 / Streaming=60 より後。フレームの計測は全部が終わってから取る。</summary>
        public const int LayerOrder = 90;

        private readonly FrameTimeSampler _sampler;
        private readonly IProfilerUiCostSource _uiCostSource;

        private int _lastGcCount;
        private bool _isActive = true;

        public ProfilerTelemetryEmitter(FrameTimeSampler sampler, IProfilerUiCostSource uiCostSource)
        {
            _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            _uiCostSource = uiCostSource ?? throw new ArgumentNullException(nameof(uiCostSource));

            // GC ベースライン。初回フレームで起動時の GC 回数を丸ごと差分として拾わないようにする。
            _lastGcCount = GC.CollectionCount(0);
        }

        /// <summary>登録直後の初期化は不要。サンプラと collector は構築時に揃っている。</summary>
        public void OnElementStart()
        {
        }

        /// <summary>
        /// 計測は LateUpdate 側で行う。Update フェーズでは何もしない。
        /// </summary>
        public void OnElementUpdate(in UpdateFrameContext context)
        {
        }

        /// <summary>
        /// フレームの計測値を取り、閾値判定に通してからレコードを送出する。
        /// 判定そのものは <see cref="ProfilerTelemetryPolicy"/>、
        /// レコード組み立ては <see cref="ProfilerTelemetryRecordFactory"/> が持つ。
        /// </summary>
        public void OnElementLateUpdate(in UpdateFrameContext context)
        {
            if (!_isActive)
            {
                return;
            }

            _sampler.Sample();

            int gcCount = GC.CollectionCount(0);
            int gcDelta = gcCount - _lastGcCount;
            _lastGcCount = gcCount;

            var uiCost = _uiCostSource.Capture();

            var input = new ProfilerFrameInput(
                summaryUpdated: _sampler.SummaryUpdated,
                gcGen0Delta: gcDelta,
                uiCostAvailable: uiCost.IsAvailable,
                canvasRebuildCount: uiCost.CanvasRebuildCount,
                batchCount: uiCost.BatchCount);

            var emission = ProfilerTelemetryPolicy.Decide(
                in input,
                AppTelemetry.Thresholds,
                AppTelemetry.IsEnabled);

            if (emission == ProfilerTelemetryEmission.None)
            {
                return;
            }

            var now = DateTime.UtcNow.Ticks;
            int unityFrame = Time.frameCount;

            if ((emission & ProfilerTelemetryEmission.Summary) != 0)
            {
                EmitSummary(now);
            }

            if ((emission & ProfilerTelemetryEmission.GcSpike) != 0)
            {
                EmitGcSpike(gcDelta, unityFrame, now);
            }

            if ((emission & ProfilerTelemetryEmission.UiCost) != 0)
            {
                EmitUiCost(in uiCost, unityFrame, now);
            }
        }

        /// <summary>
        /// Unregister は UpdateSystem の構造変更フェーズまで遅延する。
        /// 所有者は Unregister より先にここを呼び、同フレーム内の再実行を止める。
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
        }

        public void Dispose()
        {
            _isActive = false;

            if (_uiCostSource is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private void EmitSummary(long utcTicks)
        {
            float avgFps = _sampler.CpuAvgMs > 0f ? 1000f / _sampler.CpuAvgMs : 0f;

            AppTelemetry.WriteRecord(ProfilerTelemetryRecordFactory.CreateSummary(
                fps: avgFps,
                cpuAvgMs: _sampler.CpuAvgMs,
                gpuAvgMs: _sampler.GpuAvgMs,
                gpuAvailable: _sampler.IsGpuTimingAvailable,
                utcTicks: utcTicks));

            // サマリは 1 秒ごとの立ち上がりでのみ出す。読み取り側が明示的に落とす契約。
            _sampler.SummaryUpdated = false;
        }

        private void EmitGcSpike(int gcDelta, int unityFrame, long utcTicks)
        {
            AppTelemetry.WriteRecord(
                ProfilerTelemetryRecordFactory.CreateGcSpike(gcDelta, unityFrame, utcTicks));

            var sceneName = SceneManager.GetActiveScene().name;
            var message = ZString.Format(
                "[\u26a0 GC] {0} collections @ frame {1} ({2})",
                gcDelta, unityFrame, sceneName);

            // 文言は移設前の DebugProfilerView と同一。View は AlertStream の購読側に回る。
            AppTelemetry.NotifyBottleneck(message);
        }

        private void EmitUiCost(in ProfilerUiCostSnapshot uiCost, int unityFrame, long utcTicks)
        {
            AppTelemetry.WriteRecord(
                ProfilerTelemetryRecordFactory.CreateUiCost(unityFrame, utcTicks));

            var message = ZString.Format(
                "[\u26a0 UI] {0} rebuilds, {1} batches",
                uiCost.CanvasRebuildCount, uiCost.BatchCount);

            AppTelemetry.NotifyBottleneck(message);
        }
    }
}
