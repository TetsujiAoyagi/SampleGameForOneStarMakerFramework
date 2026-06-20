#nullable enable

using System;
using MessagePack;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// DebugStudio 側から Unity 側へ送る capability hello。
///
/// <para>
/// 接続直後に「こちらは何を理解できるか」を明示するための最小 DTO。
/// v1 では schema version の許容範囲と capability bitset、補助的に message type 一覧だけを流し、
/// 交渉ロジックを過剰に複雑化しない。
/// </para>
/// </summary>
[MessagePackObject]
public sealed class CapabilityHandshakeHelloEnvelopeV1
{
    [Key(0)]
    public int SchemaVersion { get; set; } = 1;

    [Key(1)]
    public string ClientName { get; set; } = string.Empty;

    [Key(2)]
    public string ClientInstanceId { get; set; } = string.Empty;

    [Key(3)]
    public int MinSchemaVersion { get; set; } = 1;

    [Key(4)]
    public int MaxSchemaVersion { get; set; } = 1;

    [Key(5)]
    public DebugStudioCapability SupportedCapabilities { get; set; } = DebugStudioCapability.None;

    [Key(6)]
    public int[] SupportedMessageTypes { get; set; } = Array.Empty<int>();
}
