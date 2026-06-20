#nullable enable

namespace DebugStudio.Contracts.Schema;

/// <summary>
/// Unity 側 <c>TelemetryTagType</c> を DebugStudio/Export 側で解釈するための共有 tag 定義。
///
/// <para>
/// ここで扱うのは「何の処理か」ではなく、「異常/補助分類として何が付与されたか」だけ。
/// 操作種別そのものは telemetry envelope の <c>Name</c> に残し、
/// tag は anomaly / auxiliary classification に限定する。
/// </para>
/// </summary>
[System.Flags]
public enum DebugTelemetryTagBits
{
    None = 0,
    Bottleneck = 1 << 0,
    CpuTimeOver = 1 << 1,
    GpuTimeOver = 1 << 2,
    ManagedMemoryOver = 1 << 3,
    NativeMemoryOver = 1 << 4,
    FrameRateDrop = 1 << 5,
    AllocSpike = 1 << 6,
    InputLatency = 1 << 7,
    NetworkIssue = 1 << 8,
    FatalError = 1 << 9,
}
