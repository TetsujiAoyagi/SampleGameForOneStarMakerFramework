#nullable enable

using System;
using MessagePack;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// Unity 側が hello へ応答して返す capability welcome。
///
/// <para>
/// 「サーバーが持つ capability」と「今回の接続で実際に使ってよい capability」を分けて返すことで、
/// 将来 sender / receiver の片側だけ先に進化しても UI が穏当に degradate できるようにする。
/// </para>
/// </summary>
[MessagePackObject]
public sealed class CapabilityHandshakeWelcomeEnvelopeV1
{
    [Key(0)]
    public int SchemaVersion { get; set; } = 1;

    [Key(1)]
    public string SessionId { get; set; } = string.Empty;

    [Key(2)]
    public string ServerName { get; set; } = string.Empty;

    [Key(3)]
    public int SelectedSchemaVersion { get; set; } = 1;

    [Key(4)]
    public DebugStudioCapability ServerCapabilities { get; set; } = DebugStudioCapability.None;

    [Key(5)]
    public DebugStudioCapability NegotiatedCapabilities { get; set; } = DebugStudioCapability.None;

    [Key(6)]
    public int[] SupportedMessageTypes { get; set; } = Array.Empty<int>();

    [Key(7)]
    public long TimestampUnixTimeMilliseconds { get; set; }

    [Key(8)]
    public string StatusMessage { get; set; } = string.Empty;
}
