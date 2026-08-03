#nullable enable

namespace DebugStudio.Export.Models;

/// <summary>
/// Contract v3 の用途固有 payload（export / NDJSON / Elastic 向け）。
/// 未設定フィールドは null のまま JSON 省略される想定。
/// </summary>
public sealed class TelemetryExportPayload
{
    /// <summary>TimingMemory / Frame / EventDetail 等の shape 名。</summary>
    public string? Shape { get; init; }

    public string? TargetIdentity { get; init; }

    public string? Stage { get; init; }

    public long? ManagedBeforeBytes { get; init; }

    public long? NativeBeforeBytes { get; init; }

    public long? ManagedAfterBytes { get; init; }

    public long? NativeAfterBytes { get; init; }

    public long? ManagedDeltaBytes { get; init; }

    public long? NativeDeltaBytes { get; init; }

    public float? Fps { get; init; }

    public float? CpuMs { get; init; }

    public float? GpuMs { get; init; }

    public bool? GpuAvailable { get; init; }

    public long? ManagedBytes { get; init; }

    public long? NativeBytes { get; init; }

    public int? GcGen0Delta { get; init; }

    public int? UnityFrame { get; init; }

    public int? CameraTotalViewCount { get; init; }

    public int? CameraAdditionalViewCount { get; init; }

    public int? CameraBlendingViewCount { get; init; }

    public int? CameraMaxStackDepthTotal { get; init; }
}
