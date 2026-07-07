#nullable enable

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
    /// 1 View の観測要約。スタック深度（レイヤー別）やブレンド状態など、テレメトリで送る不変スナップショット値。
    /// </summary>
    public readonly struct CameraViewTelemetrySummary
    {
        /// <summary>対象 View。</summary>
        public ViewId ViewId { get; init; }

        /// <summary>全レイヤー合計のスタック深度。</summary>
        public int StackDepthTotal { get; init; }

        /// <summary>Gameplay レイヤーのスタック深度。</summary>
        public int GameplayDepth { get; init; }

        /// <summary>Cutscene レイヤーのスタック深度。</summary>
        public int CutsceneDepth { get; init; }

        /// <summary>Debug レイヤーのスタック深度。</summary>
        public int DebugDepth { get; init; }

        /// <summary>アクティブカメラの Id。</summary>
        public string ActiveCameraId { get; init; }

        /// <summary>ブレンド遷移中か。</summary>
        public bool IsBlending { get; init; }

        /// <summary>ブレンド先 Snapshot（先読み）を保持しているか。</summary>
        public bool HasIncomingSnapshot { get; init; }

        /// <summary>RenderTexture 出力 View か。</summary>
        public bool IsRenderTextureView { get; init; }
    }

    /// <summary>
    /// CameraSystem 全体の観測スナップショット。View 数・ブレンド中 View 数と、各 View 要約の配列を持つ。
    /// ある時点の状態コピーであり、後続の状態変化から独立している（配列は複製済み）。
    /// </summary>
    public readonly struct CameraSystemTelemetrySnapshot
    {
        /// <summary>MainView を含む総 View 数。</summary>
        public int TotalViewCount { get; init; }

        /// <summary>MainView を除く追加 View 数。</summary>
        public int AdditionalViewCount { get; init; }

        /// <summary>ブレンド遷移中の View 数。</summary>
        public int BlendingViewCount { get; init; }

        /// <summary>各 View の要約（収集時点のコピー）。</summary>
        public CameraViewTelemetrySummary[] ViewSummaries { get; init; }
    }
}
