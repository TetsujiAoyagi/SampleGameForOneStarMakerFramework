#nullable enable

using Microsoft.Extensions.Logging;

namespace OneStarMaker.Foundation.Telemetry
{
    /// <summary>
    /// ZLogger 経由で telemetry を流すときの共通定数。
    /// formatter と sink の間で EventId を共有し、通常ログと telemetry ログを判別する。
    /// </summary>
    internal static class TelemetryZLoggerConstants
    {
        /// <summary>
        /// telemetry entry を識別する EventId。
        /// Id/Name の両方を固定し、将来 formatter 側で厳密判定しやすくする。
        /// </summary>
        public static readonly EventId EventId = new(41001, "Telemetry");
    }
}
