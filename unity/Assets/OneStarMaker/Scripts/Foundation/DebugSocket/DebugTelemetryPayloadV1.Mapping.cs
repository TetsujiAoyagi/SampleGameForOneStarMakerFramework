#nullable enable

using OneStarMaker.Foundation.Telemetry;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// 内部 TelemetryPayload → wire DTO の写像（手書き partial）。
    /// </summary>
    public sealed partial class DebugTelemetryPayloadV1
    {
        /// <summary>
        /// 内部 payload から wire DTO を構築する。Shape=None なら null（キー省略相当）。
        /// </summary>
        public static DebugTelemetryPayloadV1? FromPayload(in TelemetryPayload payload)
        {
            switch (payload.Shape)
            {
                case TelemetryPayloadShape.TimingMemory:
                    return new DebugTelemetryPayloadV1
                    {
                        Shape = (byte)TelemetryPayloadShape.TimingMemory,
                        TargetIdentity = payload.TargetIdentity,
                        Stage = payload.Stage,
                        ManagedBeforeBytes = payload.ManagedBeforeBytes,
                        NativeBeforeBytes = payload.NativeBeforeBytes,
                        ManagedAfterBytes = payload.ManagedAfterBytes,
                        NativeAfterBytes = payload.NativeAfterBytes,
                        ManagedDeltaBytes = payload.ManagedDeltaBytes,
                        NativeDeltaBytes = payload.NativeDeltaBytes,
                    };

                case TelemetryPayloadShape.Frame:
                    return new DebugTelemetryPayloadV1
                    {
                        Shape = (byte)TelemetryPayloadShape.Frame,
                        Fps = payload.Fps,
                        CpuMs = payload.CpuMs,
                        // GPU 非対応時はキーごと省略（0 埋め禁止）
                        GpuMs = payload.GpuAvailable ? payload.GpuMs : null,
                        GpuAvailable = payload.GpuAvailable,
                        ManagedBytes = payload.ManagedBytes,
                        NativeBytes = payload.NativeBytes,
                    };

                case TelemetryPayloadShape.EventDetail:
                    return new DebugTelemetryPayloadV1
                    {
                        Shape = (byte)TelemetryPayloadShape.EventDetail,
                        // UiCost 等で Gc 差分が無いときはキー省略（0 を根拠値と誤読させない）
                        GcGen0Delta = payload.GcGen0Delta > 0 ? payload.GcGen0Delta : null,
                        UnityFrame = payload.UnityFrame,
                    };

                case TelemetryPayloadShape.CameraCounters:
                    return new DebugTelemetryPayloadV1
                    {
                        Shape = (byte)TelemetryPayloadShape.CameraCounters,
                        CameraTotalViewCount = payload.CameraTotalViewCount,
                        CameraAdditionalViewCount = payload.CameraAdditionalViewCount,
                        CameraBlendingViewCount = payload.CameraBlendingViewCount,
                        CameraMaxStackDepthTotal = payload.CameraMaxStackDepthTotal,
                    };

                default:
                    return null;
            }
        }
    }
}
