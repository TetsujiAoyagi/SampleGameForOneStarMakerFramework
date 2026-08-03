#nullable enable

using MessagePack;
using OneStarMaker.Foundation.Telemetry;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// Contract v3 の用途固有 payload を wire へ載せる DTO。
    ///
    /// <para>
    /// Unity 内部の <see cref="TelemetryPayload"/> はゼロアロ struct のまま保ち、
    /// Serialize 境界（DebugSocket）でのみ参照型 DTO へ写す。
    /// 未設定フィールドは null のままにし、0 埋め欠測を作らない。
    /// DebugStudio.Contracts 側と Key 番号を手動同期すること。
    /// </para>
    /// </summary>
    [MessagePackObject]
    public sealed class DebugTelemetryPayloadV1
    {
        /// <summary><see cref="TelemetryPayloadShape"/> の数値。None=0 のときは envelope.Payload 自体を null にする。</summary>
        [Key(0)]
        public byte Shape { get; set; }

        [Key(1)]
        public string? TargetIdentity { get; set; }

        [Key(2)]
        public string? Stage { get; set; }

        [Key(3)]
        public long? ManagedBeforeBytes { get; set; }

        [Key(4)]
        public long? NativeBeforeBytes { get; set; }

        [Key(5)]
        public long? ManagedAfterBytes { get; set; }

        [Key(6)]
        public long? NativeAfterBytes { get; set; }

        [Key(7)]
        public long? ManagedDeltaBytes { get; set; }

        [Key(8)]
        public long? NativeDeltaBytes { get; set; }

        [Key(9)]
        public float? Fps { get; set; }

        [Key(10)]
        public float? CpuMs { get; set; }

        [Key(11)]
        public float? GpuMs { get; set; }

        [Key(12)]
        public bool? GpuAvailable { get; set; }

        [Key(13)]
        public long? ManagedBytes { get; set; }

        [Key(14)]
        public long? NativeBytes { get; set; }

        [Key(15)]
        public int? GcGen0Delta { get; set; }

        [Key(16)]
        public int? UnityFrame { get; set; }

        [Key(17)]
        public int? CameraTotalViewCount { get; set; }

        [Key(18)]
        public int? CameraAdditionalViewCount { get; set; }

        [Key(19)]
        public int? CameraBlendingViewCount { get; set; }

        [Key(20)]
        public int? CameraMaxStackDepthTotal { get; set; }

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
