#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// command authoring UI の将来実装に備えた薄い dispatch 境界。
/// WPF 側には既存の facade を残しつつ、実処理は再利用可能な sender へ委譲する。
/// </summary>
public sealed class CommandService : ICommandSender
{
    private readonly ICommandSender _commandSender;

    public CommandService(
        SessionService sessionService,
        CapabilityStateStore capabilityStateStore,
        CommandStore commandStore)
        : this(new TransportCommandSender(sessionService, capabilityStateStore, commandStore))
    {
    }

    public CommandService(ICommandSender commandSender)
    {
        _commandSender = commandSender ?? throw new ArgumentNullException(nameof(commandSender));
    }

    public bool CanSendCommands => _commandSender.CanSendCommands;

    public Task<string> SendAsync(
        string commandType,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        return _commandSender.SendAsync(commandType, payloadJson, cancellationToken);
    }

    public Task SendAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default)
    {
        return _commandSender.SendAsync(command, cancellationToken);
    }

    public void SweepTimedOutCommands(TimeSpan timeout)
    {
        _commandSender.SweepTimedOutCommands(timeout);
    }
}
