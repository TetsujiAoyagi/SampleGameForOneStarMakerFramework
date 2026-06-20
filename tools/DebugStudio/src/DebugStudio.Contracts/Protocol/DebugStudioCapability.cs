#nullable enable

using System;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// DebugStudio v1 が認識する capability 群。
///
/// <para>
/// wire 上は <see cref="long"/> の bit flag として流し、
/// handshake では「どの種類の送受信が双方で理解できるか」を粗く握る。
/// hierarchy / inspector のような今後拡張される領域も、
/// まずはこの粒度で合意してから個別 DTO をやり取りする想定。
/// </para>
/// </summary>
[Flags]
public enum DebugStudioCapability : long
{
    None = 0,
    CapabilityNegotiation = 1L << 0,
    LogStream = 1L << 1,
    TelemetryStream = 1L << 2,
    ServiceStatusStream = 1L << 3,
    DebugCommand = 1L << 4,
    CommandResult = 1L << 5,
    HierarchySnapshot = 1L << 6,
    HierarchyDelta = 1L << 7,
    InspectorQuery = 1L << 8,
    InspectorDetail = 1L << 9,
}
