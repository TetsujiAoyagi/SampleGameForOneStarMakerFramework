#nullable enable

using MessagePack;

namespace DebugStudio.Contracts.Protocol;

[MessagePackObject]
public sealed class DebugTelemetryEnvelopeV1
{
    /// <summary>Contract v3 以降は 3。Key 27/28 に Kind / Payload を追加。</summary>
    [Key(0)]
    public int SchemaVersion { get; set; } = 3;

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
    /// anomaly / auxiliary classification を表す bitset。
    /// 操作種別そのものは <see cref="Name"/> に残し、tag は補助分類だけを持つ。
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

    /// <summary>Contract v3: "span" / "sample" / "event"。</summary>
    [Key(27)]
    public string Kind { get; set; } = "span";

    /// <summary>Contract v3: 用途固有ペイロード。無い場合は null。</summary>
    [Key(28)]
    public DebugTelemetryPayloadV1? Payload { get; set; }
}
