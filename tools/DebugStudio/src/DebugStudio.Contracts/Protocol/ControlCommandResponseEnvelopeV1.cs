#nullable enable

using MessagePack;

namespace DebugStudio.Contracts.Protocol;

[MessagePackObject]
public sealed class ControlCommandResponseEnvelopeV1
{
    [Key(0)]
    public string RequestId { get; set; } = string.Empty;

    [Key(1)]
    public ControlCommandRoundtripStatus Status { get; set; } = ControlCommandRoundtripStatus.Failed;

    [Key(2)]
    public string Detail { get; set; } = string.Empty;

    [Key(3)]
    public DebugCommandResultEnvelopeV1? CommandResult { get; set; }
}
