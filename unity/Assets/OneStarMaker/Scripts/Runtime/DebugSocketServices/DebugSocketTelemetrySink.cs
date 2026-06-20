#nullable enable

using OneStarMaker.Foundation.Telemetry;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// AppTelemetry から DebugSocketService へレコードを橋渡しする軽量 sink。
    ///
    /// <para>
    /// サービス本体を ITelemetrySink として直接登録すると、
    /// 「socket の寿命」と「telemetry sink の寿命」が強く結び付いてしまう。
    /// そこで橋渡し専用の薄い sink を挟み、責務を分離する。
    /// </para>
    /// </summary>
    internal sealed class DebugSocketTelemetrySink : ITelemetrySink
    {
        private readonly DebugSocketService _service;

        public DebugSocketTelemetrySink(DebugSocketService service)
        {
            _service = service;
        }

        public void Write(in TelemetryRecord record)
        {
            _service.EnqueueTelemetry(record);
        }

        public void Flush()
        {
            // 実際のデータは service の送信キューに積まれるだけなので、ここでの flush は不要。
        }

        public void Dispose()
        {
        }
    }
}
