#nullable enable

using OneStarMaker.Foundation.Config;

namespace OneStarMaker.Foundation.Telemetry
{
    /// <summary>
    /// テレメトリのボトルネック検出閾値を <see cref="AppConfig"/> から読み出す。
    /// immutable。生成後は変更不可。AppConfig のキーは "telemetry:thresholds:*"。
    /// </summary>
    public sealed class TelemetryThresholds
    {
        // ── シーン ──

        /// <summary>シーンロード全体の警告閾値 (ms)。デフォルト: 500。</summary>
        public long SceneLoadMs { get; }

        /// <summary>個別フェーズ（PreLoad/Load/Init/ViewIn 等）の警告閾値 (ms)。デフォルト: 200。</summary>
        public long ScenePhaseMs { get; }

        // ── App 起動 ──

        /// <summary>起動フェーズ（BeforeSceneLoad/AfterSceneLoad）の警告閾値 (ms)。デフォルト: 1000。</summary>
        public long AppStartupPhaseMs { get; }

        // ── メモリ ──

        /// <summary>シーン遷移時のメモリ増分の警告閾値 (MB)。デフォルト: 50。</summary>
        public double MemoryDeltaMb { get; }

        // ── GC ──

        /// <summary>1 フレーム中の GC 発生回数の警告閾値。デフォルト: 1。</summary>
        public int GcPerFrame { get; }

        // ── UI ──

        /// <summary>Canvas Rebuild 回数の警告閾値（1 秒あたり）。デフォルト: 5。</summary>
        public int CanvasRebuildPerFrame { get; }

        /// <summary>描画バッチ数の警告閾値。デフォルト: 100。</summary>
        public int BatchCount { get; }

        /// <summary>
        /// <see cref="AppConfig"/> から閾値を読み出す。
        /// 未設定のキーにはデフォルト値が適用される。
        /// </summary>
        public TelemetryThresholds(AppConfig config)
        {
            SceneLoadMs           = config.GetInt("telemetry:thresholds:sceneLoadMs", 500);
            ScenePhaseMs          = config.GetInt("telemetry:thresholds:scenePhaseMs", 200);
            AppStartupPhaseMs     = config.GetInt("telemetry:thresholds:appStartupPhaseMs", 1000);
            MemoryDeltaMb         = config.GetFloat("telemetry:thresholds:memoryDeltaMb", 50f);
            GcPerFrame            = config.GetInt("telemetry:thresholds:gcPerFrame", 1);
            CanvasRebuildPerFrame = config.GetInt("telemetry:thresholds:canvasRebuildPerFrame", 5);
            BatchCount            = config.GetInt("telemetry:thresholds:batchCount", 100);
        }
    }
}
