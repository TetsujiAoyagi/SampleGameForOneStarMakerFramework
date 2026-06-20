#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// transport 送信と requestId 相関を束ねる共通 sender。
/// WPF の CommandService からも将来の CLI からも同じ経路を再利用できるようにする。
/// </summary>
public sealed class TransportCommandSender : ICommandSender
{
    private readonly SessionService _sessionService;
    private readonly CapabilityStateStore _capabilityStateStore;
    private readonly CommandStore _commandStore;

    public TransportCommandSender(
        SessionService sessionService,
        CapabilityStateStore capabilityStateStore,
        CommandStore commandStore)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _capabilityStateStore = capabilityStateStore ?? throw new ArgumentNullException(nameof(capabilityStateStore));
        _commandStore = commandStore ?? throw new ArgumentNullException(nameof(commandStore));
    }

    public bool CanSendCommands => _capabilityStateStore.Supports(DebugStudioCapability.DebugCommand);

    /// <summary>
    /// raw command から requestId を生成して送信する。
    /// correlation key をここで必ず通すことで、UI ごとに生成規則が分岐しないようにする。
    /// </summary>
    public async Task<string> SendAsync(
        string commandType,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandType))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(commandType));
        }

        var command = new DebugCommandEnvelopeV1
        {
            RequestId = CreateRequestId(commandType),
            CommandType = commandType,
            PayloadJson = payloadJson ?? string.Empty,
        };

        await SendAsync(command, cancellationToken).ConfigureAwait(false);
        return command.RequestId;
    }

    public async Task SendAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!CanSendCommands)
        {
            throw new InvalidOperationException("Unity sender has not advertised command dispatch support yet.");
        }

        if (string.IsNullOrWhiteSpace(command.RequestId))
        {
            command.RequestId = CreateRequestId(command.CommandType);
        }

        _commandStore.TrackPending(command, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        try
        {
            await _sessionService.SendCommandAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _commandStore.MarkDispatchFailed(
                command.RequestId,
                ex.Message,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            throw;
        }
    }

    /// <summary>
    /// pending request の timeout 判定を実行する。
    /// timeout の起動元は UI / CLI で別でも、状態更新ロジック自体はこの sender へ寄せる。
    /// </summary>
    public void SweepTimedOutCommands(TimeSpan timeout)
    {
        _commandStore.ExpirePending(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            (long)timeout.TotalMilliseconds);
    }

    private static string CreateRequestId(string commandType)
    {
        var normalizedCommandType = string.IsNullOrWhiteSpace(commandType) ? "command" : commandType.Trim();
        return $"{normalizedCommandType}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}";
    }
}
