#nullable enable

using MessagePack;
using NUnit.Framework;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Logging;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// additive MessagePack key の forward / backward 互換を固定する。
    /// 既存 key を再利用せず、旧 payload は新 field が null/default になるだけであることを確認する。
    /// </summary>
    [TestFixture]
    public sealed class ProtocolBackwardCompatibilityTests
    {
        [MessagePackObject]
        internal sealed class LegacyLogEnvelopeV1
        {
            [Key(0)] public int SchemaVersion { get; set; } = 1;
            [Key(1)] public string ApplicationName { get; set; } = string.Empty;
            [Key(2)] public long TimestampUnixTimeMilliseconds { get; set; }
            [Key(3)] public string Category { get; set; } = string.Empty;
            [Key(4)] public int LogLevel { get; set; }
            [Key(5)] public int EventId { get; set; }
            [Key(6)] public string? EventName { get; set; }
            [Key(7)] public string Message { get; set; } = string.Empty;
            [Key(8)] public string? Exception { get; set; }
            [Key(9)] public int ThreadId { get; set; }
            [Key(10)] public string? ThreadName { get; set; }
            [Key(11)] public string? MemberName { get; set; }
            [Key(12)] public string? FilePath { get; set; }
            [Key(13)] public int LineNumber { get; set; }
        }

        [MessagePackObject]
        internal sealed class LegacyTelemetryEnvelopeV1
        {
            [Key(0)] public int SchemaVersion { get; set; } = 1;
            [Key(1)] public long TraceId { get; set; }
            [Key(2)] public long SpanId { get; set; }
            [Key(3)] public long ParentSpanId { get; set; }
            [Key(4)] public string Name { get; set; } = string.Empty;
            [Key(5)] public long StartTimestampUtcTicks { get; set; }
            [Key(6)] public long EndTimestampUtcTicks { get; set; }
            [Key(7)] public double ElapsedMs { get; set; }
            [Key(8)] public bool IsSuccess { get; set; }
            [Key(9)] public int Level { get; set; }
            [Key(10)] public int? TagBits { get; set; }
            [Key(11)] public float CpuTime { get; set; }
            [Key(12)] public float GpuTime { get; set; }
            [Key(13)] public long ManagedMem { get; set; }
            [Key(14)] public long NativeMem { get; set; }
            [Key(15)] public int SceneFrom { get; set; } = -1;
            [Key(16)] public int SceneTo { get; set; } = -1;
            [Key(17)] public int CameraTotalViewCount { get; set; } = -1;
            [Key(18)] public int CameraAdditionalViewCount { get; set; } = -1;
            [Key(19)] public int CameraBlendingViewCount { get; set; } = -1;
            [Key(20)] public int CameraMaxStackDepthTotal { get; set; } = -1;
            [Key(21)] public int CameraViewId { get; set; } = -1;
            [Key(22)] public int CameraActiveCameraHash { get; set; } = -1;
        }

        [Test]
        public void LogEnvelope_旧payloadは相関fieldがdefaultで読める()
        {
            var legacy = new LegacyLogEnvelopeV1
            {
                ApplicationName = "LegacyApp",
                Message = "legacy log",
                LogLevel = 2,
                ThreadId = 1,
            };

            var bytes = MessagePackSerializer.Serialize(legacy);
            var current = MessagePackSerializer.Deserialize<LogEnvelopeV1>(bytes);

            Assert.AreEqual("LegacyApp", current.ApplicationName);
            Assert.AreEqual("legacy log", current.Message);
            Assert.AreEqual(string.Empty, current.SessionId);
            Assert.AreEqual(0, current.ProducerSequence);
            Assert.IsNull(current.UnityFrameAtEmit);
            Assert.IsNull(current.TraceId);
            Assert.IsNull(current.SpanId);
        }

        [Test]
        public void TelemetryEnvelope_旧payloadは相関fieldがdefaultで読める()
        {
            var legacy = new LegacyTelemetryEnvelopeV1
            {
                Name = "LegacySpan",
                TraceId = 99,
                SpanId = 100,
                ElapsedMs = 1.5,
                IsSuccess = true,
            };

            var bytes = MessagePackSerializer.Serialize(legacy);
            var current = MessagePackSerializer.Deserialize<DebugTelemetryEnvelopeV1>(bytes);

            Assert.AreEqual("LegacySpan", current.Name);
            Assert.AreEqual(99, current.TraceId);
            Assert.AreEqual(string.Empty, current.SessionId);
            Assert.AreEqual(0, current.ProducerSequence);
            Assert.IsNull(current.UnityFrameAtStart);
            Assert.IsNull(current.UnityFrameAtEnd);
        }
    }
}
