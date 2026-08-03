#nullable enable

namespace OneStarMaker.Foundation.Telemetry
{
    /// <summary>
    /// Telemetry Contract v3 の観測種別。
    /// 「何を測ったか」（<see cref="Core.TelemetryStartType"/>）とは直交し、
    /// 「どんな形の観測か」だけを表す。
    ///
    /// <para>
    /// 増やしてよいのは name（StartType）と payload 形だけ。
    /// kind 自体の増殖は禁止（計画 §8）。
    /// </para>
    /// </summary>
    public enum TelemetryKind : byte
    {
        /// <summary>開始〜終了がある処理。elapsedMs 必須。</summary>
        Span = 0,

        /// <summary>周期または状態スナップショット。elapsedMs キーを意味として持たない。</summary>
        Sample = 1,

        /// <summary>閾値超過・GC 等の発火。理由 tag + 関連値。</summary>
        Event = 2,
    }

    /// <summary>
    /// <see cref="TelemetryKind"/> と wire / export 用文字列の相互変換。
    /// hot path では enum のまま扱い、境界でのみ文字列化する。
    /// </summary>
    public static class TelemetryKindExtensions
    {
        /// <summary>wire / NDJSON で使う小文字 kind 名。</summary>
        public static string ToWireString(this TelemetryKind kind)
        {
            return kind switch
            {
                TelemetryKind.Sample => "sample",
                TelemetryKind.Event => "event",
                _ => "span",
            };
        }

        /// <summary>未知文字列は安全側で Span に落とす（旧 producer 互換）。</summary>
        public static TelemetryKind ParseWireString(string? value)
        {
            if (string.Equals(value, "sample", System.StringComparison.OrdinalIgnoreCase))
            {
                return TelemetryKind.Sample;
            }

            if (string.Equals(value, "event", System.StringComparison.OrdinalIgnoreCase))
            {
                return TelemetryKind.Event;
            }

            return TelemetryKind.Span;
        }
    }
}
