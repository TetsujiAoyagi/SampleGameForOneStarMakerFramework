#nullable enable

using System;
using MessagePack;

namespace DebugStudio.Contracts.Protocol;

[MessagePackObject]
public sealed class DebugSocketEnvelopeV1
{
    [Key(0)]
    public int SchemaVersion { get; set; } = 1;

    [Key(1)]
    public int MessageType { get; set; }

    [Key(2)]
    public string? RequestId { get; set; }

    [Key(3)]
    public byte[] Payload { get; set; } = Array.Empty<byte>();
}
