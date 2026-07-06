#nullable enable

using System.Collections.Generic;
using OneStarMaker.Foundation.Telemetry;

namespace OneStarMaker.Tests.SceneSystem.TestDoubles
{
    /// <summary>
    /// ITelemetrySink のテスト用実装。
    /// 受信した <see cref="TelemetryRecord"/> をリストに蓄積する。
    /// </summary>
    public sealed class FakeTelemetrySink : ITelemetrySink
    {
        private readonly object _lock = new();
        private readonly List<TelemetryRecord> _records = new();

        /// <summary>受信した全レコードのスナップショット。</summary>
        public IReadOnlyList<TelemetryRecord> Records
        {
            get
            {
                lock (_lock)
                {
                    return _records.ToArray();
                }
            }
        }

        /// <inheritdoc />
        public void Write(in TelemetryRecord record)
        {
            lock (_lock)
            {
                _records.Add(record);
            }
        }

        /// <inheritdoc />
        public void Flush()
        {
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
