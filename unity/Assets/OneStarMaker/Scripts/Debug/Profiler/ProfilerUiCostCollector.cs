#nullable enable

using System;
using Unity.Profiling;

namespace OneStarMaker.Debug
{
    /// <summary>
    /// UI コスト snapshot の供給元。
    ///
    /// <para>
    /// <see cref="ProfilerTelemetryEmitter"/> は <see cref="ProfilerRecorder"/> の実体ではなく
    /// このインタフェース越しに値を読む。Recorder は Development Build / Editor でしか有効にならず、
    /// 実体を直接持つとテストから閾値超過の入力を作れなくなるため。
    /// </para>
    /// </summary>
    public interface IProfilerUiCostSource
    {
        /// <summary>現フレームの UI コストを取得する。</summary>
        ProfilerUiCostSnapshot Capture();
    }

    /// <summary>
    /// UI コスト監視用の ProfilerRecorder を束ねる collector。
    ///
    /// <para>
    /// recorder の開始/停止責務を View から切り離し、
    /// 表示ロジックは軽量 snapshot の解釈だけに留める。
    /// </para>
    /// </summary>
    public sealed class ProfilerUiCostCollector : IProfilerUiCostSource, IDisposable
    {
        private ProfilerRecorder _canvasRebuildRecorder;
        private ProfilerRecorder _batchCountRecorder;

        public ProfilerUiCostCollector()
        {
            _canvasRebuildRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "UI.Canvas.RebuildBatchedCount");
            _batchCountRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "UI.Canvas.BatchCount");
        }

        public ProfilerUiCostSnapshot Capture()
        {
            return new ProfilerUiCostSnapshot(
                isAvailable: _canvasRebuildRecorder.Valid || _batchCountRecorder.Valid,
                canvasRebuildCount: _canvasRebuildRecorder.Valid ? _canvasRebuildRecorder.LastValue : 0,
                batchCount: _batchCountRecorder.Valid ? _batchCountRecorder.LastValue : 0);
        }

        public void Dispose()
        {
            _canvasRebuildRecorder.Dispose();
            _batchCountRecorder.Dispose();
        }
    }

    /// <summary>
    /// UI コスト collector から取得する軽量 snapshot。
    /// 値型にして毎フレーム取得でもヒープ確保を増やさない。
    /// </summary>
    public readonly struct ProfilerUiCostSnapshot
    {
        public readonly bool IsAvailable;
        public readonly long CanvasRebuildCount;
        public readonly long BatchCount;

        public ProfilerUiCostSnapshot(bool isAvailable, long canvasRebuildCount, long batchCount)
        {
            IsAvailable = isAvailable;
            CanvasRebuildCount = canvasRebuildCount;
            BatchCount = batchCount;
        }
    }
}
