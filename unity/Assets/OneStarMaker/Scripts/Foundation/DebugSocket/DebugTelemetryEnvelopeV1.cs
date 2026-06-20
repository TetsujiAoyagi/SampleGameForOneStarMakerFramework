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
            };
        }
    }
}
