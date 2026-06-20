#nullable enable

using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Client;

public sealed class DebugCommandRoundtripResult
{
    private DebugCommandRoundtripResult(
        DebugCommandRoundtripStatus status,
        DebugCommandEnvelopeV1 command,
        DebugCommandResultEnvelopeV1? commandResult,
        string detail)
    {
        Status = status;
        Command = command;
        CommandResult = commandResult;
        Detail = detail;
    }

    public DebugCommandRoundtripStatus Status { get; }

    public DebugCommandEnvelopeV1 Command { get; }

    public DebugCommandResultEnvelopeV1? CommandResult { get; }

    public string Detail { get; }

    public bool HasCommandResult => CommandResult != null;

    public static DebugCommandRoundtripResult Completed(
        DebugCommandEnvelopeV1 command,
        DebugCommandResultEnvelopeV1 commandResult)
    {
        return new DebugCommandRoundtripResult(DebugCommandRoundtripStatus.Completed, command, commandResult, commandResult.Message);
    }

    public static DebugCommandRoundtripResult TimedOut(DebugCommandEnvelopeV1 command, string detail)
    {
        return new DebugCommandRoundtripResult(DebugCommandRoundtripStatus.TimedOut, command, null, detail);
    }

    public static DebugCommandRoundtripResult Failed(DebugCommandEnvelopeV1 command, string detail)
    {
        return new DebugCommandRoundtripResult(DebugCommandRoundtripStatus.Failed, command, null, detail);
    }
}
