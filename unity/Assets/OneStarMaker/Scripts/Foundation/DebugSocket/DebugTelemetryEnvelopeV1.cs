#nullable enable

using MessagePack;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Telemetry;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// DebugSocket へ流すテレメトリ DTO。
    /// TelemetryRecord の内部表現をそのまま漏らさず、受信側が扱いやすい形へ写像する。
    /// </summary>
    [MessagePackObject]
    public sealed class DebugTelemetryEnvelopeV1
    {
        [Key(0)]
        public int SchemaVersion { get; set; } = 1;

        [Key(1)]
        public long TraceId { get; set; }

        [Key(2)]
        public long SpanId { get; set; }

        [Key(3)]
        public long ParentSpanId { get; set; }

        [Key(4)]
        public string Name { get; set; } = string.Empty;

        [Key(5)]
        public long StartTimestampUtcTicks { get; set; }

        [Key(6)]
        public long EndTimestampUtcTicks { get; set; }

        [Key(7)]
        public double ElapsedMs { get; set; }

        [Key(8)]
        public bool IsSuccess { get; set; }

        [Key(9)]
        public int Level { get; set; }

        /// <summary>
        /// 異常/補助分類だけを持つ tag bitset。
        /// 操作種別は <see cref="Name"/> に保持し、ここへは入れない。
        /// </summary>
        [Key(10)]
        public int? TagBits { get; set; }

        [Key(11)]
        public float CpuTime { get; set; }

        [Key(12)]
        public float GpuTime { get; set; }

        [Key(13)]
        public long ManagedMem { get; set; }

        [Key(14)]
        public long NativeMem { get; set; }

        [Key(15)]
        public int SceneFrom { get; set; } = -1;

        [Key(16)]
        public int SceneTo { get; set; } = -1;

        [Key(17)]
        public int CameraTotalViewCount { get; set; } = -1;

        [Key(18)]
        public int CameraAdditionalViewCount { get; set; } = -1;

        [Key(19)]
        public int CameraBlendingViewCount { get; set; } = -1;

        [Key(20)]
        public int CameraMaxStackDepthTotal { get; set; } = -1;

        [Key(21)]
        public int CameraViewId { get; set; } = -1;

        [Key(22)]
        public int CameraActiveCameraHash { get; set; } = -1;

        /// <summary>
        /// Unity 起動単位の session ID。handshake Welcome と同一。export 時の後付けは行わない。
        /// </summary>
        [Key(23)]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// session 内で Log / Telemetry が共有する producer 順序。receiver 受信順とは別。
        /// </summary>
        [Key(24)]
        public long ProducerSequence { get; set; }

        /// <summary>
        /// span 開始時の Unity player-loop frame。非 main thread では null。
        /// </summary>
        [Key(25)]
        public int? UnityFrameAtStart { get; set; }

        /// <summary>
        /// span 終了時の Unity player-loop frame。非 main thread では null。
        /// </summary>
        [Key(26)]
        public int? UnityFrameAtEnd { get; set; }

        public static DebugTelemetryEnvelopeV1 FromRecord(in TelemetryRecord record)
        {
            return new DebugTelemetryEnvelopeV1
            {
                TraceId = record.TraceId,
                SpanId = record.SpanId,
                ParentSpanId = record.ParentSpanId,
                Name = record.Name.ToStartTypeString(),
                StartTimestampUtcTicks = record.StartTimestampUtcTicks,
                EndTimestampUtcTicks = record.EndTimestampUtcTicks,
                ElapsedMs = record.ElapsedMs,
                IsSuccess = record.IsSuccess,
                Level = (int)record.Level,
                TagBits = record.Tags.HasValue ? (int)record.Tags.Value : null,
                CpuTime = record.MetadataValue.CpuTime,
                GpuTime = record.MetadataValue.GpuTime,
                ManagedMem = record.MetadataValue.ManagedMem,
                NativeMem = record.MetadataValue.NativeMem,
                SceneFrom = record.MetadataValue.SceneFrom,
                SceneTo = record.MetadataValue.SceneTo,
                CameraTotalViewCount = record.MetadataValue.CameraTotalViewCount,
                CameraAdditionalViewCount = record.MetadataValue.CameraAdditionalViewCount,
                CameraBlendingViewCount = record.MetadataValue.CameraBlendingViewCount,
                CameraMaxStackDepthTotal = record.MetadataValue.CameraMaxStackDepthTotal,
                CameraViewId = record.MetadataValue.CameraViewId,
                CameraActiveCameraHash = record.MetadataValue.CameraActiveCameraHash,
                SessionId = record.SessionId,
                ProducerSequence = record.ProducerSequence,
                UnityFrameAtStart = record.UnityFrameAtStart,
                UnityFrameAtEnd = record.UnityFrameAtEnd,
            };
        }
    }
}
