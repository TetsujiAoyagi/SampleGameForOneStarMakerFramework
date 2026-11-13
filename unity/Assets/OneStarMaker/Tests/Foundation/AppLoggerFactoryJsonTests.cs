#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Logging;
using OneStarMaker.Foundation.Telemetry;
using UnityEngine;

namespace OneStarMaker.Tests.Foundation
{
    [TestFixture]
    public sealed class AppLoggerFactoryJsonTests
    {
        private string _root = null!;

        [SetUp]
        public void SetUp() => _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "osm-json-" + Guid.NewGuid().ToString("N"))).FullName;

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [Test]
        public void BothProviders_PreserveVectorPrimitiveExceptionAndFollowingLines()
        {
            var realtime = Path.Combine(_root, "realtime.jsonl");
            using (var factory = new AppLoggerFactory(_root, File.Create(realtime), rollingSizeKb: 16384, realtimeLogFormat: RealtimeLogFormat.Json))
            {
                var logger = factory.CreateLogger("VectorCategory");
                logger.LogWarning(new EventId(17, "vector"), new InvalidOperationException("normal exception"),
                    "{Id} {Position} {Count} {Text}", "A", new Vector3(-1.25f, 0, 3.5f), 42, "日本語\"<>");
                // メッセージと先頭 property の双方が大きく、変換が途中確定した後に失敗する入力。
                logger.LogInformation("{Id} {Large} {Cycle}", "B", new string('x', 70000), new Cycle());
                logger.LogInformation("{Id} {Position}", "N", new Vector3(float.NaN, 1, 2));
                logger.LogInformation("{Id} {Position}", "I", new Vector3(1, float.PositiveInfinity, 2));
                logger.LogInformation("{Id} {Position}", "M", new Vector3(1, 2, float.NegativeInfinity));
                logger.LogInformation("{Id} {Position}", "C", Vector3.zero);
            }

            foreach (var path in DataFiles(realtime))
            {
                var rows = ParseLines(path);
                try
                {
                    CollectionAssert.AreEqual(new[] { "A", "C" }, rows.Select(d => d.RootElement.GetProperty("Id").GetString()).ToArray());
                    var first = rows[0].RootElement;
                    var vector = first.GetProperty("Position");
                    Assert.AreEqual(3, vector.EnumerateObject().Count());
                    Assert.AreEqual(JsonValueKind.Number, vector.GetProperty("x").ValueKind);
                    Assert.AreEqual(-1.25f, vector.GetProperty("x").GetSingle());
                    Assert.AreEqual(0f, vector.GetProperty("y").GetSingle());
                    Assert.AreEqual(3.5f, vector.GetProperty("z").GetSingle());
                    Assert.AreEqual(42, first.GetProperty("Count").GetInt32());
                    Assert.AreEqual("日本語\"<>", first.GetProperty("Text").GetString());
                    Assert.AreEqual("VectorCategory", first.GetProperty("Category").GetString());
                    Assert.AreEqual("Warning", first.GetProperty("LogLevel").GetString());
                    Assert.AreEqual(17, first.GetProperty("EventId").GetInt32());
                    StringAssert.Contains("normal exception", first.GetProperty("Exception").GetProperty("Message").GetString());
                    StringAssert.StartsWith("A ", first.GetProperty("Message").GetString());
                    Assert.AreEqual(0f, rows[1].RootElement.GetProperty("Position").GetProperty("z").GetSingle());
                }
                finally { foreach (var row in rows) row.Dispose(); }
            }
            var notices = Directory.GetFiles(_root, "logging-errors_*.txt").SelectMany(File.ReadAllLines).ToArray();
            Assert.AreEqual(8, notices.Length, "四つの失敗 × 二つの実 JSON provider。Dispose 後に全通知を観測する。");
            Assert.AreEqual(4, notices.Count(l => l.Contains("|rolling-json|provider-write-error|")));
            Assert.AreEqual(4, notices.Count(l => l.Contains("|realtime-json|provider-write-error|")));
        }

        [TestCase(300000)]
        [TestCase(1200000)]
        public void BothProviders_LargeNormalEntriesSurviveColdAndWarmBuffers(int characters)
        {
            var payload = new string('a', characters);
            var realtime = Path.Combine(_root, "realtime.jsonl");
            using (var factory = new AppLoggerFactory(_root, File.Create(realtime), rollingSizeKb: 16384, realtimeLogFormat: RealtimeLogFormat.Json))
            {
                var logger = factory.CreateLogger("Large");
                logger.LogInformation("{Id} {Large}", "cold", payload);
                logger.LogInformation("{Id}", "warmup");
                logger.LogInformation("{Id} {Large}", "warm", payload);
                logger.LogInformation("{Id}", "after");
            }
            foreach (var path in DataFiles(realtime))
            {
                var rows = ParseLines(path);
                try
                {
                    Assert.AreEqual(4, rows.Length);
                    Assert.AreEqual(payload, rows[0].RootElement.GetProperty("Large").GetString());
                    Assert.AreEqual(payload, rows[2].RootElement.GetProperty("Large").GetString());
                    Assert.AreEqual("cold " + payload, rows[0].RootElement.GetProperty("Message").GetString());
                    Assert.AreEqual("warm " + payload, rows[2].RootElement.GetProperty("Message").GetString());
                }
                finally { foreach (var row in rows) row.Dispose(); }
            }
            Assert.IsEmpty(Directory.GetFiles(_root, "logging-errors_*.txt"));
        }

        [Test]
        public void Factory_TwoBackgroundProducersDrainAndRotateNotices()
        {
            var realtime = Path.Combine(_root, "realtime.jsonl");
            using (var factory = new AppLoggerFactory(_root, File.Create(realtime), rollingSizeKb: 1, realtimeLogFormat: RealtimeLogFormat.Json))
            {
                var logger = factory.CreateLogger("Background");
                using var start = new ManualResetEventSlim();
                var producers = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
                {
                    start.Wait();
                    for (var i = 0; i < 40; i++) logger.LogInformation("{Cycle}", new Cycle());
                    logger.LogInformation("{Id}", "done");
                })).ToArray();
                start.Set();
                Task.WhenAll(producers).GetAwaiter().GetResult();
            }
            var files = Directory.GetFiles(_root, "logging-errors_*.txt");
            Assert.Greater(files.Length, 2);
            var lines = files.SelectMany(File.ReadAllLines).ToArray();
            Assert.AreEqual(80, lines.Count(l => l.Contains("|rolling-json|")));
            Assert.AreEqual(80, lines.Count(l => l.Contains("|realtime-json|")));
            foreach (var path in DataFiles(realtime))
            {
                var rows = ParseLines(path);
                try { Assert.AreEqual(2, rows.Length); }
                finally { foreach (var row in rows) row.Dispose(); }
            }
        }

        [Test]
        public void RollingNormalFiles_KeepSizeRotationAndEarlierGenerations()
        {
            using (var factory = new AppLoggerFactory(_root, rollingSizeKb: 1))
            {
                var logger = factory.CreateLogger("Rolling");
                for (var i = 0; i < 30; i++) logger.LogInformation("{Index} {Large}", i, new string('a', 20000));
            }
            var files = Directory.GetFiles(_root, "*.log");
            Assert.Greater(files.Length, 2);
            var indexes = files.SelectMany(path =>
            {
                var rows = ParseLines(path);
                try { return rows.Select(d => d.RootElement.GetProperty("Index").GetInt32()).ToArray(); }
                finally { foreach (var row in rows) row.Dispose(); }
            }).OrderBy(i => i).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(0, 30).ToArray(), indexes);
        }

        [Test]
        public void DefaultMessagePack_PreservesWireAndSuppressesTelemetryWhileRollingJsonKeepsFields()
        {
            var realtime = Path.Combine(_root, "realtime.bin");
            using (var factory = new AppLoggerFactory(_root, File.Create(realtime)))
            {
                using var telemetry = new JsonFileTelemetrySink(factory);
                telemetry.Write(new TelemetryRecord(
                    traceId: 100, spanId: 200, parentSpanId: -1,
                    name: TelemetryStartType.SceneLoad,
                    startTimestampUtcTicks: 1000, endTimestampUtcTicks: 2000,
                    elapsedMs: 1.25, isSuccess: true, tags: null,
                    level: TelemetryLevel.Verbose, metadata: default,
                    sessionId: "factory-session", producerSequence: 12,
                    unityFrameAtStart: 90, unityFrameAtEnd: 93));
                factory.CreateLogger("Companion").LogInformation("wire companion");
            }
            var bytes = File.ReadAllBytes(realtime);
            Assert.Greater(bytes.Length, 4);
            Assert.AreEqual(bytes.Length, BitConverter.ToInt32(bytes, 0) + 4, "通常ログ1 frame以外の余分なbytesを認めない。");
            Assert.IsTrue(DebugSocketProtocol.TryDeserializeEnvelope(bytes, out var envelope));
            Assert.NotNull(envelope);
            Assert.AreEqual((int)DebugSocketMessageType.Log, envelope!.MessageType);
            Assert.IsTrue(DebugSocketProtocol.TryDeserializePayload(envelope, out LogEnvelopeV1? log));
            Assert.AreEqual("wire companion", log!.Message);
            var rows = ParseLines(Directory.GetFiles(_root, "*.log").Single());
            try
            {
                var row = rows.Single(r => r.RootElement.GetProperty("Category").GetString() == "Telemetry").RootElement;
                Assert.AreEqual("factory-session", row.GetProperty("SessionId").GetString());
                Assert.AreEqual(12, row.GetProperty("ProducerSequence").GetInt64());
                Assert.AreEqual(90, row.GetProperty("UnityFrameAtStart").GetInt32());
                Assert.AreEqual(93, row.GetProperty("UnityFrameAtEnd").GetInt32());
                Assert.AreEqual(1.25, row.GetProperty("ElapsedMs").GetDouble());
                Assert.IsTrue(row.GetProperty("IsSuccess").GetBoolean());
            }
            finally { foreach (var row in rows) row.Dispose(); }
        }

        [Test]
        public void RealtimeIoFailure_IsNotReportedAsSerializationFailure()
        {
            using (var factory = new AppLoggerFactory(_root, new FailingStream(), realtimeLogFormat: RealtimeLogFormat.Json))
                factory.CreateLogger("IoFailure").LogInformation("normal input");
            var notices = Directory.GetFiles(_root, "logging-errors_*.txt").SelectMany(File.ReadAllLines).ToArray();
            Assert.IsTrue(notices.Any(l => l.Contains("|realtime-json|provider-write-error|System.IO.IOException")));
            Assert.IsFalse(notices.Any(l => l.Contains("serialization")));
        }

        [Test]
        public void Factory_CleansExpiredManagedFilesBeforeLoggingStarts()
        {
            var stale = Path.Combine(_root, "2000-01-01_1000.log");
            File.WriteAllText(stale, "expired");
            File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-10));
            using var factory = new AppLoggerFactory(_root);
            Assert.IsFalse(File.Exists(stale));
            // 起動後に古くした file は定期清掃しない。終了時の一括削除も行わない。
            File.WriteAllText(stale, "created while running");
            File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-10));
            factory.CreateLogger("Retention").LogInformation("normal");
            factory.Dispose();
            Assert.AreEqual("created while running", File.ReadAllText(stale));
        }

        private string[] DataFiles(string realtime)
            => Directory.GetFiles(_root, "*.log").Concat(new[] { realtime }).ToArray();

        private static JsonDocument[] ParseLines(string path)
            => File.ReadAllLines(path, Encoding.UTF8).Where(l => l.Length > 0).Select(l => JsonDocument.Parse(l)).ToArray();

        private sealed class Cycle
        {
            public Cycle Self => this;
            public override string ToString() => "cycle";
        }

        private sealed class FailingStream : MemoryStream
        {
            public override void Write(byte[] buffer, int offset, int count) => throw new IOException("secret I/O details");
            public override void Write(ReadOnlySpan<byte> buffer) => throw new IOException("secret I/O details");
        }
    }
}
