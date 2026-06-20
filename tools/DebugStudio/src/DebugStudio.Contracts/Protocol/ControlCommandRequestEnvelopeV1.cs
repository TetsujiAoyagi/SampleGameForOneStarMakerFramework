#nullable enable

using MessagePack;

namespace DebugStudio.Contracts.Protocol;

[MessagePackObject]
public sealed class ControlCommandRequestEnvelopeV1
{
    [Key(0)]
    public string RequestId { get; set; } = string.Empty;

    [Key(1)]
    public string CommandType { get; set; } = string.Empty;

    [Key(2)]
    public string PayloadJson { get; set; } = "{}";

    [Key(3)]
    public int TimeoutMilliseconds { get; set; } = 15000;
}
