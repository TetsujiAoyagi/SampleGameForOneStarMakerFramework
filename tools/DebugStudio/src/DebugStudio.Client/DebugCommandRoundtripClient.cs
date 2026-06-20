#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Client;

public sealed class DebugCommandRoundtripClient
{
    private readonly IDebugStudioCommandSession _session;

    public DebugCommandRoundtripClient(IDebugStudioCommandSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async Task<DebugCommandRoundtripResult> SendAsync(
        DebugCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ServerUri is null)
        {
            throw new ArgumentException("A server URI is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.CommandType))
        {
            throw new ArgumentException("A command type is required.", nameof(request));
        }

        if (request.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Timeout must be greater than zero.");
        }

        var command = new DebugCommandEnvelopeV1
        {
            RequestId = string.IsNullOrWhiteSpace(request.RequestId)
                ? DebugCommandRequestIdFactory.Create(request.CommandType)
                : request.RequestId,
            CommandType = request.CommandType.Trim(),
            PayloadJson = request.PayloadJson ?? "{}",
        };

        var completionSource = new TaskCompletionSource<DebugCommandResultEnvelopeV1>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnCommandResultReceived(DebugCommandResultEnvelopeV1 result)
        {
            // 同じ接続上で他 request の結果が流れてくる可能性があるため、
            // CLI は自分が送った requestId と一致した result だけを完了条件にする。
            if (string.Equals(result.RequestId, command.RequestId, StringComparison.Ordinal))
            {
                completionSource.TrySetResult(result);
            }
        }

        void OnConnectionStateChanged(DebugSocketConnectionSnapshot snapshot)
        {
            if (snapshot.State is DebugSocketConnectionState.Disconnected or DebugSocketConnectionState.Faulted)
            {
                var detail = string.IsNullOrWhiteSpace(snapshot.Detail)
                    ? "The DebugSocket connection closed before a matching CommandResult was received."
                    : snapshot.Detail;
                completionSource.TrySetException(new InvalidOperationException(detail));
            }
        }

        _session.CommandResultReceived += OnCommandResultReceived;
        _session.ConnectionStateChanged += OnConnectionStateChanged;

        using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationCts.CancelAfter(request.Timeout);

        try
        {
            // connect / send / wait を同じ timeout に束ねることで、
            // どこか一段で固まっても CLI 全体が帰ってこない状態を避ける。
            await _session.ConnectAsync(
                    new DebugSocketClientOptions
                    {
                        ServerUri = request.ServerUri,
                    },
                    operationCts.Token)
                .ConfigureAwait(false);

            await _session.SendCommandAsync(command, operationCts.Token).ConfigureAwait(false);

            var commandResult = await completionSource.Task.WaitAsync(operationCts.Token).ConfigureAwait(false);
            return DebugCommandRoundtripResult.Completed(command, commandResult);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && operationCts.IsCancellationRequested)
        {
            return DebugCommandRoundtripResult.TimedOut(
                command,
                $"Timed out after {request.Timeout.TotalSeconds:0.###} second(s) waiting for CommandResult for request '{command.RequestId}'.");
        }
        catch (Exception ex)
        {
            return DebugCommandRoundtripResult.Failed(command, ex.Message);
        }
        finally
        {
            _session.CommandResultReceived -= OnCommandResultReceived;
            _session.ConnectionStateChanged -= OnConnectionStateChanged;

            try
            {
                await _session.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }
}
