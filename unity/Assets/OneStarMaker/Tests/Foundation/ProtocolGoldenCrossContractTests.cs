#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MessagePack;
using NUnit.Framework;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Logging;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// PROTO-00: DebugStudio と同一の golden hex fixture を Unity 側でも検証する。
    /// fixture はリポジトリ直下 protocol/debugsocket/fixtures/proto00/ を正とする。
    /// </summary>
    [TestFixture]
    public sealed class ProtocolGoldenCrossContractTests
    {
        private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard;

        [Test]
        public void LogEnvelopeV1_Serialize_MatchesGoldenBytes()
        {
            var expected = LoadHex("log_envelope_v1.hex");
            var actual = MessagePackSerializer.Serialize(CreateGoldenLog(), Options);
            Assert.AreEqual(ToHex(expected), ToHex(actual));
        }

        [Test]
        public void LogEnvelopeV1_Deserialize_GoldenBytes_PreservesFields()
        {
            var bytes = LoadHex("log_envelope_v1.hex");
            var decoded = MessagePackSerializer.Deserialize<LogEnvelopeV1>(bytes, Options);
            var expected = CreateGoldenLog();

            Assert.AreEqual(expected.SchemaVersion, decoded.SchemaVersion);
            Assert.AreEqual(expected.ApplicationName, decoded.ApplicationName);
            Assert.AreEqual(expected.TimestampUnixTimeMilliseconds, decoded.TimestampUnixTimeMilliseconds);
            Assert.AreEqual(expected.Category, decoded.Category);
            Assert.AreEqual(expected.LogLevel, decoded.LogLevel);
            Assert.AreEqual(expected.EventId, decoded.EventId);
            Assert.AreEqual(expected.EventName, decoded.EventName);
            Assert.AreEqual(expected.Message, decoded.Message);
            Assert.IsNull(decoded.Exception);
            Assert.AreEqual(expected.ThreadId, decoded.ThreadId);
            Assert.AreEqual(expected.ThreadName, decoded.ThreadName);
            Assert.AreEqual(expected.MemberName, decoded.MemberName);
            Assert.AreEqual(expected.FilePath, decoded.FilePath);
            Assert.AreEqual(expected.LineNumber, decoded.LineNumber);
            Assert.AreEqual(expected.SessionId, decoded.SessionId);
            Assert.AreEqual(expected.ProducerSequence, decoded.ProducerSequence);
            Assert.AreEqual(expected.UnityFrameAtEmit, decoded.UnityFrameAtEmit);
            Assert.AreEqual(expected.TraceId, decoded.TraceId);
            Assert.AreEqual(expected.SpanId, decoded.SpanId);
        }

        [Test]
        public void DebugTelemetryPayloadV1_Serialize_MatchesGoldenBytes()
        {
            var expected = LoadHex("debug_telemetry_payload_v1.hex");
            var actual = MessagePackSerializer.Serialize(CreateGoldenPayload(), Options);
            Assert.AreEqual(ToHex(expected), ToHex(actual));
        }

        [Test]
        public void DebugTelemetryEnvelopeV1_Serialize_MatchesGoldenBytes()
        {
            var expected = LoadHex("debug_telemetry_envelope_v1.hex");
            var actual = MessagePackSerializer.Serialize(CreateGoldenTelemetry(), Options);
            Assert.AreEqual(ToHex(expected), ToHex(actual));
        }

        [Test]
        public void DebugTelemetryEnvelopeV1_Deserialize_GoldenBytes_PreservesNestedPayload()
        {
            var bytes = LoadHex("debug_telemetry_envelope_v1.hex");
            var decoded = MessagePackSerializer.Deserialize<DebugTelemetryEnvelopeV1>(bytes, Options);
            var expected = CreateGoldenTelemetry();

            Assert.AreEqual(expected.SchemaVersion, decoded.SchemaVersion);
            Assert.AreEqual(expected.TraceId, decoded.TraceId);
            Assert.AreEqual(expected.SpanId, decoded.SpanId);
            Assert.AreEqual(expected.ParentSpanId, decoded.ParentSpanId);
            Assert.AreEqual(expected.Name, decoded.Name);
            Assert.AreEqual(expected.ElapsedMs, decoded.ElapsedMs);
            Assert.AreEqual(expected.IsSuccess, decoded.IsSuccess);
            Assert.IsNull(decoded.TagBits);
            Assert.AreEqual(expected.SceneFrom, decoded.SceneFrom);
            Assert.AreEqual(expected.SceneTo, decoded.SceneTo);
            Assert.AreEqual(-1, decoded.CameraTotalViewCount);
            Assert.AreEqual(expected.SessionId, decoded.SessionId);
            Assert.AreEqual(expected.Kind, decoded.Kind);
            Assert.IsNotNull(decoded.Payload);
            Assert.AreEqual(expected.Payload!.Shape, decoded.Payload!.Shape);
            Assert.AreEqual(expected.Payload.TargetIdentity, decoded.Payload.TargetIdentity);
            Assert.AreEqual(expected.Payload.ManagedBeforeBytes, decoded.Payload.ManagedBeforeBytes);
            Assert.IsNull(decoded.Payload.Fps);
        }

        [Test]
        public void DebugSocketEnvelopeV1_FramedLog_MatchesGoldenBytes()
        {
            var expected = LoadHex("framed_log_envelope_v1.hex");
            var actual = DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.Log,
                CreateGoldenLog(),
                requestId: "req-proto00");
            Assert.AreEqual(ToHex(expected), ToHex(actual));
        }

        [Test]
        public void DebugSocketEnvelopeV1_FramedTelemetry_MatchesGoldenBytes()
        {
            var expected = LoadHex("framed_telemetry_envelope_v1.hex");
            var actual = DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.Telemetry,
                CreateGoldenTelemetry(),
                requestId: null);
            Assert.AreEqual(ToHex(expected), ToHex(actual));
        }

        [Test]
        public void FramedLogGolden_RoundtripsThroughProtocolHelpers()
        {
            var framed = LoadHex("framed_log_envelope_v1.hex");
            Assert.IsTrue(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
            Assert.IsNotNull(envelope);
            Assert.AreEqual(1, envelope!.SchemaVersion);
            Assert.AreEqual((int)DebugSocketMessageType.Log, envelope.MessageType);
            Assert.AreEqual("req-proto00", envelope.RequestId);
            Assert.IsTrue(DebugSocketProtocol.TryDeserializePayload(envelope, out LogEnvelopeV1? log));
            Assert.IsNotNull(log);
            Assert.AreEqual("golden-log", log!.Message);
            Assert.AreEqual(1000L, log.TraceId);
        }

        private static LogEnvelopeV1 CreateGoldenLog() => new LogEnvelopeV1
        {
            SchemaVersion = 1,
            ApplicationName = "OneStarMaker",
            TimestampUnixTimeMilliseconds = 1722902400123,
            Category = "Foundation.DebugSocket",
            LogLevel = 2,
            EventId = 42,
            EventName = "Proto00",
            Message = "golden-log",
            Exception = null,
            ThreadId = 1,
            ThreadName = "Main",
            MemberName = "Emit",
            FilePath = "Assets/Log.cs",
            LineNumber = 10,
            SessionId = "sess-proto00",
            ProducerSequence = 7,
            UnityFrameAtEmit = 120,
            TraceId = 1000,
            SpanId = 2000,
        };

        private static DebugTelemetryPayloadV1 CreateGoldenPayload() => new DebugTelemetryPayloadV1
        {
            Shape = 1,
            TargetIdentity = "Cell_0_0",
            Stage = "BeforeSceneLoad",
            ManagedBeforeBytes = 100,
            NativeBeforeBytes = 200,
            ManagedAfterBytes = 150,
            NativeAfterBytes = 250,
            ManagedDeltaBytes = 50,
            NativeDeltaBytes = 50,
        };

        private static DebugTelemetryEnvelopeV1 CreateGoldenTelemetry() => new DebugTelemetryEnvelopeV1
        {
            SchemaVersion = 3,
            TraceId = unchecked((long)0x1234567890ABCDEF),
            SpanId = unchecked((long)0xFEDCBA0987654321),
            ParentSpanId = unchecked((long)0x1111222233334444),
            Name = "SceneLoad",
            StartTimestampUtcTicks = 638000000000000000,
            EndTimestampUtcTicks = 638000000001000000,
            ElapsedMs = 42.75,
            IsSuccess = true,
            Level = 0,
            TagBits = null,
            CpuTime = 12.5f,
            GpuTime = 3.25f,
            ManagedMem = 1048576,
            NativeMem = 2097152,
            SceneFrom = 10,
            SceneTo = 20,
            CameraTotalViewCount = -1,
            CameraAdditionalViewCount = -1,
            CameraBlendingViewCount = -1,
            CameraMaxStackDepthTotal = -1,
            CameraViewId = -1,
            CameraActiveCameraHash = -1,
            SessionId = "sess-proto00",
            ProducerSequence = 3,
            UnityFrameAtStart = 50,
            UnityFrameAtEnd = 55,
            Kind = "span",
            Payload = CreateGoldenPayload(),
        };

        private static byte[] LoadHex(string fileName)
        {
            var path = ResolveFixturePath(fileName);
            var hex = new string(File.ReadAllText(path)
                .Where(c => !char.IsWhiteSpace(c))
                .ToArray());
            return ParseHex(hex);
        }

        // Convert.ToHexString / Convert.FromHexString は .NET 5 以降の API で
        // netstandard2.1（Unity の API Compatibility Level）には存在しない。
        // DebugStudio 側（net8.0）とは違い、Unity 側は自前で持つ必要がある。
        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static byte[] ParseHex(string hex)
        {
            if (hex.Length % 2 != 0)
            {
                throw new FormatException($"hex fixture length must be even but was {hex.Length}.");
            }

            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(
                    hex.Substring(i * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        private static string ResolveFixturePath(string fileName)
        {
            // Assets → unity → repo root
            var repoRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ".."));
            var candidate = Path.Combine(repoRoot, "protocol", "debugsocket", "fixtures", "proto00", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            // EditMode / CI で dataPath が想定外でも辿れるよう、カレントから探索する。
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                candidate = Path.Combine(dir.FullName, "protocol", "debugsocket", "fixtures", "proto00", fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            throw new FileNotFoundException(
                $"PROTO-00 fixture not found: {fileName}. Expected under protocol/debugsocket/fixtures/proto00/");
        }
    }
}
