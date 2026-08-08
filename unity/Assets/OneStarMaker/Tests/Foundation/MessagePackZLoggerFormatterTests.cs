#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Logging;
using OneStarMaker.Foundation.Telemetry;
using ZLogger;

namespace OneStarMaker.Tests.Foundation
{
    [TestFixture]
    public sealed class MessagePackZLoggerFormatterTests
    {
        private const string ApplicationName = "TestApp";

        [Test]
        public void NormalLog_EmitsSingleLogFrame()
        {
            using var stream = new MemoryStream();
            using var loggerFactory = CreateLoggerFactory(stream);

            var logger = loggerFactory.CreateLogger("TestCategory");
            logger.LogInformation("Hello test message");

            loggerFactory.Dispose();

            var frames = ParseAllFrames(stream.ToArray());
            Assert.AreEqual(1, frames.Count, "通常ログは 1 フレームのみ");

            var envelope = frames[0];
            Assert.AreEqual((int)DebugSocketMessageType.Log, envelope.MessageType);

            Assert.IsTrue(
                DebugSocketProtocol.TryDeserializePayload(envelope, out LogEnvelopeV1? logPayload),
                "Log payload の復号に失敗");
            Assert.NotNull(logPayload);
            Assert.AreEqual(ApplicationName, logPayload!.ApplicationName);
            Assert.AreEqual("TestCategory", logPayload.Category);
            Assert.AreEqual("Hello test message", logPayload.Message);
            Assert.AreEqual((int)LogLevel.Information, logPayload.LogLevel);
        }

        [Test]
        public void StructuredTemplateLog_MessageIsFullyFormatted()
        {
            using var stream = new MemoryStream();
            using var loggerFactory = CreateLoggerFactory(stream);

            var logger = loggerFactory.CreateLogger("TemplateCategory");
            // MEL 標準のテンプレート呼び出し。receiver へは整形済み文字列が届くべきで、
            // 生テンプレート "[{Component}] type={Type}" のまま送ってはいけない。
            logger.LogInformation("[{Component}] type={Type}", "Foo", "Bar");

            loggerFactory.Dispose();

            var frames = ParseAllFrames(stream.ToArray());
            Assert.AreEqual(1, frames.Count, "テンプレートログも 1 フレームのみ");
            Assert.AreEqual((int)DebugSocketMessageType.Log, frames[0].MessageType);

            Assert.IsTrue(
                DebugSocketProtocol.TryDeserializePayload(frames[0], out LogEnvelopeV1? logPayload),
                "Log payload の復号に失敗");
            Assert.NotNull(logPayload);
            Assert.AreEqual("[Foo] type=Bar", logPayload!.Message);
        }

        [Test]
        public void TelemetryEntry_IsSuppressedFromRealtimeStream()
        {
            using var stream = new MemoryStream();
            using var loggerFactory = CreateLoggerFactory(stream);

            var telemetrySink = new JsonFileTelemetrySink(loggerFactory);
            var logger = loggerFactory.CreateLogger("OtherCategory");

            telemetrySink.Write(new TelemetryRecord(
                traceId: 100,
                spanId: 200,
                parentSpanId: -1,
                name: TelemetryStartType.SceneLoad,
                startTimestampUtcTicks: DateTime.UtcNow.Ticks,
                endTimestampUtcTicks: DateTime.UtcNow.Ticks,
                elapsedMs: 1.0,
                isSuccess: true,
                tags: null,
                level: TelemetryLevel.Verbose,
                metadata: default));

            logger.LogInformation("companion log line");

            loggerFactory.Dispose();

            var frames = ParseAllFrames(stream.ToArray());
            Assert.AreEqual(1, frames.Count, "telemetry は捨て、通常ログだけ残る");

            foreach (var frame in frames)
            {
                Assert.AreNotEqual(
                    (int)DebugSocketMessageType.Telemetry,
                    frame.MessageType,
                    "realtime stream に Telemetry フレームを出してはいけない");
            }

            Assert.AreEqual((int)DebugSocketMessageType.Log, frames[0].MessageType);
            Assert.IsTrue(
                DebugSocketProtocol.TryDeserializePayload(frames[0], out LogEnvelopeV1? logPayload));
            Assert.NotNull(logPayload);
            Assert.AreEqual("companion log line", logPayload!.Message);
        }

        [Test]
        public void TelemetryEntry_L0Jsonには相関値をstructuredPropertiesとして出力する()
        {
            using var stream = new MemoryStream();
            using var loggerFactory = CreateJsonLoggerFactory(stream);
            using var telemetrySink = new JsonFileTelemetrySink(loggerFactory);

            telemetrySink.Write(new TelemetryRecord(
                traceId: 100,
                spanId: 200,
                parentSpanId: -1,
                name: TelemetryStartType.SceneLoad,
                startTimestampUtcTicks: DateTime.UtcNow.Ticks,
                endTimestampUtcTicks: DateTime.UtcNow.Ticks,
                elapsedMs: 1.0,
                isSuccess: true,
                tags: null,
                level: TelemetryLevel.Verbose,
                metadata: default,
                sessionId: "l0-session",
                producerSequence: 12,
                unityFrameAtStart: 90,
                unityFrameAtEnd: 93));

            loggerFactory.Dispose();

            using var document = JsonDocument.Parse(stream.ToArray());
            var root = document.RootElement;
            Assert.AreEqual("l0-session", root.GetProperty("SessionId").GetString());
            Assert.AreEqual(12, root.GetProperty("ProducerSequence").GetInt64());
            Assert.AreEqual(90, root.GetProperty("UnityFrameAtStart").GetInt32());
            Assert.AreEqual(93, root.GetProperty("UnityFrameAtEnd").GetInt32());
        }

        /// <summary>
        /// 本番の <c>AppLoggerFactory</c> と同じ配線で realtime MessagePack 出力を組む。
        /// provider 設定と factory デコレータは相関値を運ぶために対で必要。
        /// </summary>
        private static ILoggerFactory CreateLoggerFactory(Stream stream)
        {
            return new ProducerCorrelationLoggerFactory(LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddZLoggerStream(
                    stream,
                    options => MessagePackZLoggerFormatter.Configure(options, ApplicationName));
            }));
        }

        private static ILoggerFactory CreateJsonLoggerFactory(Stream stream)
        {
            return LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddZLoggerStream(
                    stream,
                    options => options.UseJsonFormatter(formatter =>
                    {
                        formatter.UseUtcTimestamp = true;
                        formatter.IncludeProperties = IncludeProperties.All;
                    }));
            });
        }

        /// <summary>
        /// stream 全体を strict に frame 分解する。
        /// 抑制テストの本質は「余計なバイトが 1 バイトも出ていないこと」の証明なので、
        /// 不正 frame や末尾ゴミを黙って読み飛ばすと検証が成立しない。
        /// 異常を見つけたら即テスト失敗させ、最後に全バイト消費を確認する。
        /// </summary>
        private static List<DebugSocketEnvelopeV1> ParseAllFrames(byte[] data)
        {
            var frames = new List<DebugSocketEnvelopeV1>();
            var offset = 0;

            while (offset < data.Length)
            {
                if (offset + sizeof(int) > data.Length)
                {
                    Assert.Fail(
                        $"offset {offset} に length prefix 未満の端数バイトが残っている (total {data.Length} bytes)");
                }

                var payloadLength = BitConverter.ToInt32(data, offset);
                if (payloadLength <= 0)
                {
                    Assert.Fail(
                        $"offset {offset} の length prefix が不正 ({payloadLength})。空 frame か破損 frame が混入している");
                }

                var frameLength = sizeof(int) + payloadLength;
                if (offset + frameLength > data.Length)
                {
                    Assert.Fail(
                        $"offset {offset} の frame (length {payloadLength}) が stream 末尾を超える。frame が途中で切れている");
                }

                var framed = new ReadOnlyMemory<byte>(data, offset, frameLength);
                if (!DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope) || envelope == null)
                {
                    Assert.Fail($"offset {offset} の frame を envelope として復号できない");
                }

                frames.Add(envelope!);
                offset += frameLength;
            }

            Assert.AreEqual(
                data.Length,
                offset,
                "stream 全体が frame として消費されるべき。末尾に余計なバイトがある");

            return frames;
        }
    }
}
