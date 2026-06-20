#nullable enable

using MessagePack;

namespace DebugStudio.Contracts.Protocol;

[MessagePackObject]
public sealed class DebugSocketServiceStatusEnvelopeV1
{
    [Key(0)]
    public string Status { get; set; } = string.Empty;

    [Key(1)]
    public string Message { get; set; } = string.Empty;

    [Key(2)]
    public long TimestampUnixTimeMilliseconds { get; set; }
}
