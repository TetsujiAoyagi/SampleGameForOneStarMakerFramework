#nullable enable

using NUnit.Framework;
using OneStarMaker.Debug;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime;

namespace OneStarMaker.Tests.Profiler
{
    /// <summary>
    /// <see cref="ProfilerTelemetryRecordFactory"/> が組み立てる record の形を固定するテスト。
    ///
    /// <para>
    /// Kibana 側の検算ルール（V1〜V12）と _export 済みダッシュボードが、
    /// ここで作る record の kind / level / tags / センチネル値に依存している。
    /// ProfilerSummary の kind を span にしたり parentSpanId を 0 に戻したりすると、
    /// 出力先を見る前にここが赤くなる。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ProfilerTelemetryRecordFactoryTests
    {
        private const long FixedUtcTicks = 638_000_000_000_000_000L;

        [Test]
        public void サマリはkindがSampleである()
        {
            var record = CreateSummary(fps: 60f);

            Assert.AreEqual(TelemetryKind.Sample, record.Kind);
        }

        [Test]
        public void サマリはnameがProfilerSummaryでlevelがVerboseである()
        {
            var record = CreateSummary(fps: 60f);

            Assert.AreEqual(TelemetryStartType.ProfilerSummary, record.Name);
            Assert.AreEqual(TelemetryLevel.Verbose, record.Level);
        }

        [Test]
        public void GcSpikeとUiCostはkindがEventでlevelがSummaryである()
        {
            var gcSpike = ProfilerTelemetryRecordFactory.CreateGcSpike(
                gcGen0Delta: 3, unityFrame: 120, utcTicks: FixedUtcTicks);
            var uiCost = ProfilerTelemetryRecordFactory.CreateUiCost(
                unityFrame: 120, utcTicks: FixedUtcTicks);

            Assert.AreEqual(TelemetryKind.Event, gcSpike.Kind);
            Assert.AreEqual(TelemetryStartType.GcSpike, gcSpike.Name);
            Assert.AreEqual(TelemetryLevel.Summary, gcSpike.Level);

            Assert.AreEqual(TelemetryKind.Event, uiCost.Kind);
            Assert.AreEqual(TelemetryStartType.UiCost, uiCost.Name);
            Assert.AreEqual(TelemetryLevel.Summary, uiCost.Level);
        }

        /// <summary>
        /// 親なしのセンチネルは -1 に統一する（0 と混在させない）。
        /// </summary>
        [Test]
        public void 全種別でparentSpanIdが0ではなくマイナス1である()
        {
            foreach (var record in CreateAll())
            {
                Assert.AreEqual(-1L, record.ParentSpanId, record.Name.ToString());
            }
        }

        /// <summary>
        /// sample / event はいずれも瞬間の観測であり、区間を持たない。
        /// start と end は引数の ticks に一致し、elapsedMs は 0 のプレースホルダに留める。
        /// </summary>
        [Test]
        public void 全種別で開始と終了が引数のticksと一致しelapsedMsは0である()
        {
            foreach (var record in CreateAll())
            {
                Assert.AreEqual(FixedUtcTicks, record.StartTimestampUtcTicks, record.Name.ToString());
                Assert.AreEqual(FixedUtcTicks, record.EndTimestampUtcTicks, record.Name.ToString());
                Assert.AreEqual(0d, record.ElapsedMs, record.Name.ToString());
            }
        }

        [Test]
        public void GcSpikeのpayloadに渡したGC差分が載っている()
        {
            var record = ProfilerTelemetryRecordFactory.CreateGcSpike(
                gcGen0Delta: 7, unityFrame: 42, utcTicks: FixedUtcTicks);

            Assert.IsTrue(record.Payload.HasEventDetail);
            Assert.AreEqual(7, record.Payload.GcGen0Delta);
            Assert.AreEqual(42, record.Payload.UnityFrame);
        }

        [Test]
        public void サマリのtagsはfpsのClassifyFrameRateと一致する()
        {
            // 30fps 未満は FrameRateDrop、それ以外はタグなし。
            var slow = CreateSummary(fps: 20f);
            var fast = CreateSummary(fps: 60f);

            Assert.AreEqual(RuntimeTelemetryMetadataFactory.ClassifyFrameRate(20f), slow.Tags);
            Assert.AreEqual(TelemetryTagType.FrameRateDrop, slow.Tags);

            Assert.AreEqual(RuntimeTelemetryMetadataFactory.ClassifyFrameRate(60f), fast.Tags);
            Assert.IsNull(fast.Tags);
        }

        // ── ヘルパ ──

        private static TelemetryRecord CreateSummary(float fps)
            => ProfilerTelemetryRecordFactory.CreateSummary(
                fps: fps,
                cpuAvgMs: 16.6f,
                gpuAvgMs: 8.3f,
                gpuAvailable: true,
                utcTicks: FixedUtcTicks);

        private static TelemetryRecord[] CreateAll()
            => new[]
            {
                CreateSummary(fps: 60f),
                ProfilerTelemetryRecordFactory.CreateGcSpike(
                    gcGen0Delta: 3, unityFrame: 120, utcTicks: FixedUtcTicks),
                ProfilerTelemetryRecordFactory.CreateUiCost(
                    unityFrame: 120, utcTicks: FixedUtcTicks),
            };
    }
}
