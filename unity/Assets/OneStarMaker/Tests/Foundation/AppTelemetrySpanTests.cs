#nullable enable

using NUnit.Framework;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Tests.SceneSystem.TestDoubles;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// span 終了時の副作用契約を検証する。
    ///
    /// <para>
    /// level フィルタで record を書かない場合でも current span は必ず解除する。
    /// ここを外すと、出力されないだけの span が以降のログすべてに誤った trace を付ける。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class AppTelemetrySpanTests
    {
        [Test]
        public void FinishSpan_FilteredLevel_ClearsCurrentSpanWithoutWriting()
        {
            var sink = new FakeTelemetrySink();
            var originalLevel = AppTelemetry.Level;

            try
            {
                AppTelemetry.Level = TelemetryLevel.Summary;
                AppTelemetry.AddSink(sink);

                var span = AppTelemetry.StartSpan(TelemetryStartType.SceneTransition, tags: null);
                Assert.IsTrue(span.HasValue);
                Assert.NotNull(AppTelemetry.CurrentSpanId);
                Assert.NotNull(AppTelemetry.CurrentTraceId);

                AppTelemetry.FinishSpan(
                    span,
                    metadata: default,
                    isSuccess: true,
                    level: TelemetryLevel.Verbose);

                Assert.AreEqual(0, sink.Records.Count, "Summary 設定下では Verbose レコードを書かない");
                Assert.IsNull(AppTelemetry.CurrentSpanId, "フィルタ後も current span を残してはいけない");
                Assert.IsNull(AppTelemetry.CurrentTraceId);
            }
            finally
            {
                AppTelemetry.RemoveSink(sink);
                AppTelemetry.Level = originalLevel;
            }
        }

        [Test]
        public void FinishSpan_VerboseLevel_WritesRecordAndClearsCurrentSpan()
        {
            var sink = new FakeTelemetrySink();
            var originalLevel = AppTelemetry.Level;

            try
            {
                AppTelemetry.Level = TelemetryLevel.Verbose;
                AppTelemetry.AddSink(sink);

                var span = AppTelemetry.StartSpan(TelemetryStartType.SceneLoad, tags: null);
                Assert.IsTrue(span.HasValue);

                var expectedTraceId = span!.Value.TraceId;
                var expectedSpanId = span.Value.SpanId;

                AppTelemetry.FinishSpan(
                    span,
                    metadata: default,
                    isSuccess: true,
                    level: TelemetryLevel.Verbose);

                Assert.AreEqual(1, sink.Records.Count);
                Assert.AreEqual(expectedTraceId, sink.Records[0].TraceId);
                Assert.AreEqual(expectedSpanId, sink.Records[0].SpanId);
                Assert.IsNull(AppTelemetry.CurrentSpanId);
            }
            finally
            {
                AppTelemetry.RemoveSink(sink);
                AppTelemetry.Level = originalLevel;
            }
        }
    }
}
