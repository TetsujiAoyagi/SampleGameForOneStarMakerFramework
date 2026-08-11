#nullable enable

using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using OneStarMaker.Debug;
using OneStarMaker.Foundation.Config;
using OneStarMaker.Foundation.Telemetry;

namespace OneStarMaker.Tests.Profiler
{
    /// <summary>
    /// <see cref="ProfilerTelemetryPolicy"/> の閾値判定を固定するテスト。
    ///
    /// <para>
    /// 守りたいのは 2 点。
    /// 1 つ目は「テレメトリ無効時・閾値未設定時に一切送出しない」こと。
    /// 2 つ目は「閾値比較が厳密な &gt; であって &gt;= ではない」こと。
    /// 後者を <c>&gt;=</c> に書き換えると
    /// <c>GC差分が閾値ちょうどならGcSpikeは立たない</c> が赤くなる
    /// （gcPerFrame の既定値 1 のまま毎フレーム GcSpike が出る事故を、ここで止める）。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ProfilerTelemetryPolicyTests
    {
        private const int GcPerFrame = 2;
        private const int CanvasRebuildPerFrame = 5;
        private const int BatchCount = 100;

        [Test]
        public void テレメトリが無効なら全ての入力で何も送出しない()
        {
            var input = new ProfilerFrameInput(
                summaryUpdated: true,
                gcGen0Delta: GcPerFrame + 100,
                uiCostAvailable: true,
                canvasRebuildCount: CanvasRebuildPerFrame + 100,
                batchCount: BatchCount + 100);

            var emission = ProfilerTelemetryPolicy.Decide(in input, CreateThresholds(), telemetryEnabled: false);

            Assert.AreEqual(ProfilerTelemetryEmission.None, emission);
        }

        [Test]
        public void 閾値が未設定なら全ての入力で何も送出しない()
        {
            var input = new ProfilerFrameInput(
                summaryUpdated: true,
                gcGen0Delta: GcPerFrame + 100,
                uiCostAvailable: true,
                canvasRebuildCount: CanvasRebuildPerFrame + 100,
                batchCount: BatchCount + 100);

            var emission = ProfilerTelemetryPolicy.Decide(in input, thresholds: null, telemetryEnabled: true);

            Assert.AreEqual(ProfilerTelemetryEmission.None, emission);
        }

        [Test]
        public void サマリ更新フラグが立っているときだけSummaryが立つ()
        {
            var updated = CreateInput(summaryUpdated: true);
            var notUpdated = CreateInput(summaryUpdated: false);

            Assert.AreEqual(
                ProfilerTelemetryEmission.Summary,
                Decide(in updated));
            Assert.AreEqual(
                ProfilerTelemetryEmission.None,
                Decide(in notUpdated));
        }

        /// <summary>
        /// 境界テスト。閾値ちょうどでは発火しない。
        /// <see cref="ProfilerTelemetryPolicy.Decide"/> の <c>&gt;</c> を <c>&gt;=</c> に
        /// 書き換えるとこのテストが赤くなる。
        /// </summary>
        [Test]
        public void GC差分が閾値ちょうどならGcSpikeは立たない()
        {
            var input = CreateInput(gcGen0Delta: GcPerFrame);

            Assert.AreEqual(ProfilerTelemetryEmission.None, Decide(in input));
        }

        [Test]
        public void GC差分が閾値を1超えるとGcSpikeが立つ()
        {
            var input = CreateInput(gcGen0Delta: GcPerFrame + 1);

            Assert.AreEqual(ProfilerTelemetryEmission.GcSpike, Decide(in input));
        }

        [Test]
        public void GC差分が0以下ならGcSpikeは立たない()
        {
            var zero = CreateInput(gcGen0Delta: 0);
            var negative = CreateInput(gcGen0Delta: -1);

            Assert.AreEqual(ProfilerTelemetryEmission.None, Decide(in zero));
            Assert.AreEqual(ProfilerTelemetryEmission.None, Decide(in negative));
        }

        [Test]
        public void UIコストが計測不能ならrebuildもbatchも超過していてもUiCostは立たない()
        {
            var input = CreateInput(
                uiCostAvailable: false,
                canvasRebuildCount: CanvasRebuildPerFrame + 1000,
                batchCount: BatchCount + 1000);

            Assert.AreEqual(ProfilerTelemetryEmission.None, Decide(in input));
        }

        [Test]
        public void rebuildだけ超過してもbatchだけ超過してもUiCostが立つ()
        {
            var rebuildOnly = CreateInput(
                uiCostAvailable: true,
                canvasRebuildCount: CanvasRebuildPerFrame + 1,
                batchCount: BatchCount);

            var batchOnly = CreateInput(
                uiCostAvailable: true,
                canvasRebuildCount: CanvasRebuildPerFrame,
                batchCount: BatchCount + 1);

            var neither = CreateInput(
                uiCostAvailable: true,
                canvasRebuildCount: CanvasRebuildPerFrame,
                batchCount: BatchCount);

            Assert.AreEqual(ProfilerTelemetryEmission.UiCost, Decide(in rebuildOnly));
            Assert.AreEqual(ProfilerTelemetryEmission.UiCost, Decide(in batchOnly));
            Assert.AreEqual(ProfilerTelemetryEmission.None, Decide(in neither));
        }

        [Test]
        public void 全種別の条件を同時に満たすとフラグが全て立つ()
        {
            var input = new ProfilerFrameInput(
                summaryUpdated: true,
                gcGen0Delta: GcPerFrame + 1,
                uiCostAvailable: true,
                canvasRebuildCount: CanvasRebuildPerFrame + 1,
                batchCount: BatchCount + 1);

            var emission = Decide(in input);

            Assert.AreEqual(
                ProfilerTelemetryEmission.Summary
                | ProfilerTelemetryEmission.GcSpike
                | ProfilerTelemetryEmission.UiCost,
                emission);
        }

        // ── ヘルパ ──

        private static ProfilerTelemetryEmission Decide(in ProfilerFrameInput input)
            => ProfilerTelemetryPolicy.Decide(in input, CreateThresholds(), telemetryEnabled: true);

        /// <summary>
        /// 既定値と区別できるよう gcPerFrame だけ既定（1）から動かした閾値を作る。
        /// </summary>
        private static TelemetryThresholds CreateThresholds()
        {
            var provider = new InMemoryConfigProvider(new Dictionary<string, string>
            {
                ["telemetry:thresholds:gcPerFrame"] = GcPerFrame.ToString(CultureInfo.InvariantCulture),
                ["telemetry:thresholds:canvasRebuildPerFrame"] = CanvasRebuildPerFrame.ToString(CultureInfo.InvariantCulture),
                ["telemetry:thresholds:batchCount"] = BatchCount.ToString(CultureInfo.InvariantCulture),
            });

            return new TelemetryThresholds(new AppConfig(new IConfigProvider[] { provider }));
        }

        /// <summary>条件を 1 つずつ動かすための既定入力（どの条件も満たさない状態）。</summary>
        private static ProfilerFrameInput CreateInput(
            bool summaryUpdated = false,
            int gcGen0Delta = 0,
            bool uiCostAvailable = false,
            long canvasRebuildCount = 0,
            long batchCount = 0)
            => new(summaryUpdated, gcGen0Delta, uiCostAvailable, canvasRebuildCount, batchCount);

        private sealed class InMemoryConfigProvider : IConfigProvider
        {
            private readonly Dictionary<string, string> _values;

            public InMemoryConfigProvider(Dictionary<string, string> values)
            {
                _values = values;
            }

            public void Load(Dictionary<string, string> store)
            {
                foreach (var pair in _values)
                {
                    store[pair.Key] = pair.Value;
                }
            }
        }
    }
}
