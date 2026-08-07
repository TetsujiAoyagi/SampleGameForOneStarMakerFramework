#nullable enable

using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Telemetry;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// 内部 TelemetryRecord → wire DTO の写像（手書き partial）。
    /// 生成クラス本体は YAML 正本から出力される。
    /// </summary>
    public sealed partial class DebugTelemetryEnvelopeV1
    {
        public static DebugTelemetryEnvelopeV1 FromRecord(in TelemetryRecord record)
        {
            return new DebugTelemetryEnvelopeV1
            {
                SchemaVersion = 3,
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
                // 旧フラット欄は段階移行のため併記（新規意味づけは Payload 側）
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
                Kind = record.Kind.ToWireString(),
                Payload = DebugTelemetryPayloadV1.FromPayload(record.Payload),
            };
        }
    }
}
