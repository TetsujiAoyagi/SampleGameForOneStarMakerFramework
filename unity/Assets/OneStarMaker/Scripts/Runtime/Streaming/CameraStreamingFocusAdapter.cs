#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.CameraSystem;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.Streaming
{
    /// <summary>
    /// CameraFocusProvider と WorldStreamingController を 1 Tick に束ねる薄い adapter（CAM-09 / 正典 §9）。
    /// テストでは <see cref="Tick"/> を直接呼び、本番では UpdateSystem 等から駆動する。
    /// </summary>
    public sealed class CameraStreamingFocusAdapter
    {
        private readonly CameraFocusProvider _focusProvider;
        private readonly WorldStreamingController _controller;
        private IReadOnlyList<CameraFocusSource> _sources;

        /// <summary>
        /// adapter を構築する。
        /// </summary>
        /// <param name="controller">注視点を受け取るストリーミング Controller。</param>
        /// <param name="sources">Tick 毎に読む focus 供給元。省略時は空（no-op）。</param>
        /// <param name="focusProvider">注視点抽出器。省略時は既定実装。</param>
        public CameraStreamingFocusAdapter(
            WorldStreamingController controller,
            IReadOnlyList<CameraFocusSource>? sources = null,
            CameraFocusProvider? focusProvider = null)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _focusProvider = focusProvider ?? new CameraFocusProvider();
            _sources = sources ?? Array.Empty<CameraFocusSource>();
        }

        /// <summary>
        /// Tick 対象 View 群。実行中の View 追加/Release に追随するため差し替え可能。
        /// </summary>
        public IReadOnlyList<CameraFocusSource> Sources
        {
            get => _sources;
            set => _sources = value ?? Array.Empty<CameraFocusSource>();
        }

        /// <summary>直前 Tick で Controller へ渡した focus 件数。CAM-10 テレメトリの観測用。</summary>
        public int LastForwardedFocusCount { get; private set; }

        /// <summary>
        /// focus 収集 → Controller.Tick を 1 回実行する。
        /// 包含対象が 0 件のときは no-op（RT のみ等の一時状態を許容する）。
        /// </summary>
        public void Tick()
        {
            var focuses = _focusProvider.CollectFocusPositions(_sources);
            LastForwardedFocusCount = focuses.Count;

            // WorldStreamingController は focus 1 件以上を要求する。全 View 除外時はストリーミングを止める。
            if (focuses.Count == 0)
            {
                return;
            }

            _controller.Tick(focuses);
        }
    }
}
