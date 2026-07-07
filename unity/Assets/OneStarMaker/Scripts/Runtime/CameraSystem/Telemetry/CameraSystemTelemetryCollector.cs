#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Telemetry
{
    /// <summary>
    /// CameraSystem の現在状態を <see cref="CameraSystemTelemetrySnapshot"/> へ抽出する収集器。
    /// 内部バッファを再利用して毎回の割当を抑えつつ、出力は独立した配列コピーとして返す。
    /// </summary>
    public sealed class CameraSystemTelemetryCollector
    {
        private readonly List<CameraViewTelemetrySummary> _viewBuffer = new();

        /// <summary>
        /// 全 View の要約を収集し、ブレンド中 View 数を数えてスナップショットを組み立てる。
        /// バッファは毎回クリアして再利用する。
        /// </summary>
        public CameraSystemTelemetrySnapshot Capture(OneStarMaker.Runtime.CameraSystem.Core.CameraSystem system)
        {
            _viewBuffer.Clear();
            system.CollectViewTelemetrySummaries(_viewBuffer);

            var blendingCount = 0;
            for (var i = 0; i < _viewBuffer.Count; i++)
            {
                if (_viewBuffer[i].IsBlending)
                {
                    blendingCount++;
                }
            }

            return new CameraSystemTelemetrySnapshot
            {
                TotalViewCount = system.TotalViewCount,
                AdditionalViewCount = system.AdditionalViewCount,
                BlendingViewCount = blendingCount,
                ViewSummaries = _viewBuffer.ToArray(),
            };
        }
    }
}
