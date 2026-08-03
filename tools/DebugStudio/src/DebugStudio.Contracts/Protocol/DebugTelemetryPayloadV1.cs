#nullable enable

using MessagePack;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// Unity 側 <c>DebugTelemetryPayloadV1</c> の手動同期コピー。
/// Key 番号・型を変更したら Unity Foundation 側も同時更新すること。
/// </summary>
[MessagePackObject]
public sealed class DebugTelemetryPayloadV1
{
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
}
