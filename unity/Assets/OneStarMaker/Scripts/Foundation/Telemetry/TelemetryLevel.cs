#nullable enable

namespace OneStarMaker.Foundation.Telemetry
{
    /// <summary>
    /// テレメトリ出力の粒度レベル。
    /// <see cref="AppTelemetry.Level"/> で設定し、
    /// <see cref="ITelemetrySink"/> 実装がフィルタリングに利用する。
    /// </summary>
    public enum TelemetryLevel
    {
        /// <summary>全フェーズをリアルタイム出力。DEBUG ビルドのみ推奨。</summary>
        Verbose = 0,

        /// <summary>遷移完了時のサマリのみ出力。Release ビルドのデフォルト。</summary>
        Summary = 1,

        /// <summary>テレメトリ無効。</summary>
        Off = 2,
    }
}
