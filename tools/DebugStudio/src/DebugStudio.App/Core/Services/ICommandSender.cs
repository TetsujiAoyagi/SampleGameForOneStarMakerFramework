#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// WPF / CLI などのフロントエンドから共通利用する command dispatch 境界。
/// requestId の払い出しと pending/result 相関を 1 本化し、
/// 送信経路ごとのズレを避ける。
/// </summary>
public interface ICommandSender
{
    bool CanSendCommands { get; }

    Task<string> SendAsync(
        string commandType,
        string payloadJson,
        CancellationToken cancellationToken = default);

    Task SendAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default);

    void SweepTimedOutCommands(TimeSpan timeout);
}
