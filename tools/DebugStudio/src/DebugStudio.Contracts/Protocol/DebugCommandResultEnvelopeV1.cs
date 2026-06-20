#nullable enable

using MessagePack;

namespace DebugStudio.Contracts.Protocol;

[MessagePackObject]
public sealed class DebugCommandResultEnvelopeV1
{
    [Key(0)]
    public string RequestId { get; set; } = string.Empty;

    [Key(1)]
    public bool Success { get; set; }

    [Key(2)]
    public string Message { get; set; } = string.Empty;

    [Key(3)]
    public string PayloadJson { get; set; } = string.Empty;
}
