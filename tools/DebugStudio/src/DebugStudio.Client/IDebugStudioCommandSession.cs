#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Client;

public interface IDebugStudioCommandSession : IAsyncDisposable
{
    event Action<DebugSocketConnectionSnapshot>? ConnectionStateChanged;
    event Action<DebugCommandResultEnvelopeV1>? CommandResultReceived;

    Task ConnectAsync(DebugSocketClientOptions options, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task SendCommandAsync(DebugCommandEnvelopeV1 command, CancellationToken cancellationToken = default);
}
