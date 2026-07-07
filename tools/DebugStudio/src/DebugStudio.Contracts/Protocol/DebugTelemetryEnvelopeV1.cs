#nullable enable

using MessagePack;

namespace DebugStudio.Contracts.Protocol;

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
}
