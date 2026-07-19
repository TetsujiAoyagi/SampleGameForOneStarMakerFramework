#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Logging;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using ZLogger;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// Telemetry / Log の frame・session・sequence・trace 相関契約を検証する。
    /// </summary>
    [TestFixture]
    public sealed class TelemetryLogCorrelationTests
    {
        private const string ApplicationName = "CorrelationTestApp";

        [SetUp]
        public void SetUp()
        {
            UnitySessionCorrelationContext.ResetForTests();
            UnityPlayerLoopFrameObservation.ResetForTests();
            UnitySessionCorrelationContext.ResetForNewPlayerSession();
        }

        [TearDown]
        public void TearDown()
        {
            UnitySessionCorrelationContext.ResetForTests();
            UnityPlayerLoopFrameObservation.ResetForTests();
        }

        [Test]
        public void FinishSpan_MainThread観測時はstartとendFrameを保持する()
        {
            var observedFrame = 42;
            UnityPlayerLoopFrameObservation.Register(() => observedFrame);
            var sink = new FakeTelemetrySink();
            var originalLevel = AppTelemetry.Level;

            try
            {
                AppTelemetry.Level = TelemetryLevel.Verbose;
                AppTelemetry.AddSink(sink);

                var span = AppTelemetry.StartSpan(TelemetryStartType.SceneLoad, tags: null);
                observedFrame = 45;
                AppTelemetry.FinishSpan(span, metadata: default, isSuccess: true, level: TelemetryLevel.Verbose);

                Assert.AreEqual(1, sink.Records.Count);
                Assert.AreEqual(42, sink.Records[0].UnityFrameAtStart);
                Assert.AreEqual(45, sink.Records[0].UnityFrameAtEnd);
                Assert.AreEqual(UnitySessionCorrelationContext.SessionId, sink.Records[0].SessionId);
                Assert.AreEqual(1, sink.Records[0].ProducerSequence);
            }
            finally
            {
                AppTelemetry.RemoveSink(sink);
                AppTelemetry.Level = originalLevel;
            }
        }

        [Test]
        public void FinishSpan_非MainThread観測時はframeをnullにする()
        {
            UnityPlayerLoopFrameObservation.Register(() => null);
            var sink = new FakeTelemetrySink();
            var originalLevel = AppTelemetry.Level;

            try
            {
                AppTelemetry.Level = TelemetryLevel.Verbose;
                AppTelemetry.AddSink(sink);

                var span = AppTelemetry.StartSpan(TelemetryStartType.SceneLoad, tags: null);
                AppTelemetry.FinishSpan(span, metadata: default, isSuccess: true, level: TelemetryLevel.Verbose);

                Assert.AreEqual(1, sink.Records.Count);
                Assert.IsNull(sink.Records[0].UnityFrameAtStart);
                Assert.IsNull(sink.Records[0].UnityFrameAtEnd);
            }
            finally
            {
                AppTelemetry.RemoveSink(sink);
                AppTelemetry.Level = originalLevel;
            }
        }

        [Test]
        public void LogAndTelemetry_共有sequenceで1_2_3と採番される()
        {
            UnityPlayerLoopFrameObservation.Register(() => 10);
            var sink = new FakeTelemetrySink();
            var originalLevel = AppTelemetry.Level;

            using var stream = new MemoryStream();
            using var loggerFactory = CreateLoggerFactory(stream);

            try
            {
                AppTelemetry.Level = TelemetryLevel.Verbose;
                AppTelemetry.AddSink(sink);

                var span = AppTelemetry.StartSpan(TelemetryStartType.SceneLoad, tags: null);
                var logger = loggerFactory.CreateLogger("Correlation");
                logger.LogInformation("inside span");

                AppTelemetry.FinishSpan(span, metadata: default, isSuccess: true, level: TelemetryLevel.Verbose);
                logger.LogInformation("outside span");

                loggerFactory.Dispose();

                Assert.AreEqual(1, sink.Records.Count);
                Assert.AreEqual(2, sink.Records[0].ProducerSequence, "FinishSpan telemetry は span 内 log の後に wire 化される");

                var logPayloads = ParseLogPayloads(stream.ToArray());
                Assert.AreEqual(2, logPayloads.Count);
                Assert.AreEqual(1, logPayloads[0].ProducerSequence, "span 内 log は FinishSpan より先に format される");
                Assert.AreEqual(3, logPayloads[1].ProducerSequence, "span 外 log は sequence 3");
                Assert.AreEqual(UnitySessionCorrelationContext.SessionId, logPayloads[0].SessionId);
                Assert.AreEqual(UnitySessionCorrelationContext.SessionId, logPayloads[1].SessionId);
                Assert.IsNull(logPayloads[1].TraceId, "span 外 log は trace context を持たない");
                Assert.IsNull(logPayloads[1].SpanId, "span 外 log は span context を持たない");
            }
            finally
            {
                AppTelemetry.RemoveSink(sink);
                AppTelemetry.Level = originalLevel;
            }
        }

        [Test]
        public void LogInsideActiveSpan_TraceIdとSpanIdを持つ()
        {
            UnityPlayerLoopFrameObservation.Register(() => 7);
            var sink = new FakeTelemetrySink();
            var originalLevel = AppTelemetry.Level;

            using var stream = new MemoryStream();
            using var loggerFactory = CreateLoggerFactory(stream);

            try
            {
                AppTelemetry.Level = TelemetryLevel.Verbose;
                AppTelemetry.AddSink(sink);

                var span = AppTelemetry.StartSpan(TelemetryStartType.SceneLoad, tags: null);
                Assert.IsTrue(span.HasValue);

                var logger = loggerFactory.CreateLogger("SpanLog");
                logger.LogInformation("correlated");

                loggerFactory.Dispose();

                var logPayloads = ParseLogPayloads(stream.ToArray());
                Assert.AreEqual(1, logPayloads.Count);
                Assert.AreEqual(span!.Value.TraceId, logPayloads[0].TraceId);
                Assert.AreEqual(span.Value.SpanId, logPayloads[0].SpanId);
                Assert.AreEqual(7, logPayloads[0].UnityFrameAtEmit);
            }
            finally
            {
                AppTelemetry.RemoveSink(sink);
                AppTelemetry.Level = originalLevel;
            }
        }

        [Test]
        public void TelemetryWireRoundtrip_相関fieldを保持する()
        {
            UnityPlayerLoopFrameObservation.Register(() => 100);
            var record = new TelemetryRecord(
                traceId: 1,
                spanId: 2,
                parentSpanId: -1,
                name: TelemetryStartType.SceneTransition,
                startTimestampUtcTicks: 638_000_000_000_000_000,
                endTimestampUtcTicks: 638_000_000_001_000_000,
                elapsedMs: 10,
                isSuccess: true,
                tags: null,
                level: TelemetryLevel.Verbose,
                metadata: default,
                sessionId: "abc123",
                producerSequence: 5,
                unityFrameAtStart: 100,
                unityFrameAtEnd: 105);

            var envelope = DebugTelemetryEnvelopeV1.FromRecord(record);
            var framed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Telemetry, envelope);

            Assert.IsTrue(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var decodedEnvelope));
            Assert.IsTrue(DebugSocketProtocol.TryDeserializePayload(decodedEnvelope!, out DebugTelemetryEnvelopeV1? payload));

            Assert.AreEqual("abc123", payload!.SessionId);
            Assert.AreEqual(5, payload.ProducerSequence);
            Assert.AreEqual(100, payload.UnityFrameAtStart);
            Assert.AreEqual(105, payload.UnityFrameAtEnd);
        }

        [Test]
        public void WriteRecord_空sessionでsequenceだけを持つrecordは現在producerのcontextで補完する()
        {
            UnityPlayerLoopFrameObservation.Register(() => 21);
            var sink = new FakeTelemetrySink();
            var originalLevel = AppTelemetry.Level;

            try
            {
                AppTelemetry.Level = TelemetryLevel.Verbose;
                AppTelemetry.AddSink(sink);
                var incomplete = CreateDirectRecord(
                    sessionId: string.Empty,
                    producerSequence: 99,
                    unityFrameAtStart: null,
                    unityFrameAtEnd: null);

                AppTelemetry.WriteRecord(incomplete);

                Assert.AreEqual(1, sink.Records.Count);
                Assert.AreEqual(UnitySessionCorrelationContext.SessionId, sink.Records[0].SessionId);
                Assert.AreEqual(1, sink.Records[0].ProducerSequence);
                Assert.AreEqual(21, sink.Records[0].UnityFrameAtStart);
                Assert.AreEqual(21, sink.Records[0].UnityFrameAtEnd);
            }
            finally
            {
                AppTelemetry.RemoveSink(sink);
                AppTelemetry.Level = originalLevel;
            }
        }

        [Test]
        public void WriteRecord_現在sessionとsequenceを持つrecordはproducer値を保持する()
        {
            UnityPlayerLoopFrameObservation.Register(() => null);
            var sink = new FakeTelemetrySink();
            var originalLevel = AppTelemetry.Level;

            try
            {
                AppTelemetry.Level = TelemetryLevel.Verbose;
                AppTelemetry.AddSink(sink);
                var complete = CreateDirectRecord(
                    sessionId: UnitySessionCorrelationContext.SessionId,
                    producerSequence: 88,
                    unityFrameAtStart: null,
                    unityFrameAtEnd: null);

                AppTelemetry.WriteRecord(complete);

                Assert.AreEqual(1, sink.Records.Count);
                Assert.AreEqual(UnitySessionCorrelationContext.SessionId, sink.Records[0].SessionId);
                Assert.AreEqual(88, sink.Records[0].ProducerSequence);
                Assert.IsNull(sink.Records[0].UnityFrameAtStart);
                Assert.IsNull(sink.Records[0].UnityFrameAtEnd);
            }
            finally
            {
                AppTelemetry.RemoveSink(sink);
                AppTelemetry.Level = originalLevel;
            }
        }

        private static TelemetryRecord CreateDirectRecord(
            string sessionId,
            long producerSequence,
            int? unityFrameAtStart,
            int? unityFrameAtEnd)
        {
            return new TelemetryRecord(
                traceId: 1,
                spanId: 2,
                parentSpanId: -1,
                name: TelemetryStartType.SceneLoad,
                startTimestampUtcTicks: DateTime.UtcNow.Ticks,
                endTimestampUtcTicks: DateTime.UtcNow.Ticks,
                elapsedMs: 1,
                isSuccess: true,
                tags: null,
                level: TelemetryLevel.Verbose,
                metadata: default,
                sessionId: sessionId,
                producerSequence: producerSequence,
                unityFrameAtStart: unityFrameAtStart,
                unityFrameAtEnd: unityFrameAtEnd);
        }

        private static ILoggerFactory CreateLoggerFactory(Stream stream)
        {
            return LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddZLoggerStream(
                    stream,
                    options => options.UseFormatter(() => new MessagePackZLoggerFormatter(ApplicationName)));
            });
        }

        private static List<LogEnvelopeV1> ParseLogPayloads(byte[] data)
        {
            var result = new List<LogEnvelopeV1>();
            var offset = 0;

            while (offset < data.Length)
            {
                var payloadLength = BitConverter.ToInt32(data, offset);
                var frameLength = sizeof(int) + payloadLength;
                var framed = new ReadOnlyMemory<byte>(data, offset, frameLength);

                Assert.IsTrue(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
                Assert.IsTrue(DebugSocketProtocol.TryDeserializePayload(envelope!, out LogEnvelopeV1? logPayload));
                result.Add(logPayload!);

                offset += frameLength;
            }

            return result;
        }
    }
}
