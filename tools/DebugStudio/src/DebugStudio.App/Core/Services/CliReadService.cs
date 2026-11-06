#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Stores;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Services;

/// <summary>保持済み階層と要求単位の Inspector 購読を使い、GUI の選択を変更せずに読み取る。</summary>
public sealed class CliReadService
{
    private readonly SessionService _session;
    private readonly HierarchyStore _hierarchy;
    private readonly CapabilityStateStore _capabilities;
    private readonly TimeProvider _time;

    public CliReadService(SessionService sessionService, HierarchyStore hierarchyStore,
        CapabilityStateStore capabilityStateStore, TimeProvider? timeProvider = null)
    {
        _session = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _hierarchy = hierarchyStore ?? throw new ArgumentNullException(nameof(hierarchyStore));
        _capabilities = capabilityStateStore ?? throw new ArgumentNullException(nameof(capabilityStateStore));
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<ControlReadResponseEnvelopeV1> ReadAsync(ControlReadRequestEnvelopeV1 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ControlReadResponseEnvelopeV1 Error(string reason) => new() { RequestId = request.RequestId, Reason = reason };
        // 外部 control 接続から長時間の待ちや曖昧な操作を持ち込ませない。
        // 範囲検査は listener 側で応答を作る前に行い、要求単位の期限を有限に保つ。
        if (request.TimeoutMilliseconds is <= 0 or > 60000 ||
            request.Operation is not ("hierarchy" or "inspect") ||
            (request.Operation == "inspect" && request.NodeId <= 0)) return Error("invalid-request");
        if (cancellationToken.IsCancellationRequested) return Error("cancelled");
        if (request.Operation == "hierarchy")
        {
            // GUI 選択や表示中の Inspector を動かさず、受信済みの retained state をそのまま返す。
            // Clear 後の空 snapshot と、まだ一度も受信していない初期状態は revision で区別する。
            var retained = _hierarchy.GetRetainedSnapshot();
            var data = new ControlHierarchySnapshotV1
            {
                HasSnapshot = retained.State.Revision > 0,
                Revision = retained.State.Revision,
                CapturedAtUnixTimeMilliseconds = retained.State.CapturedAtUnixTimeMilliseconds,
                ConnectionState = _session.State.ToString(),
                HandshakeState = _capabilities.GetSnapshot().HandshakeState,
                Nodes = retained.Nodes.Select(n => new HierarchyNodeDtoV1
                {
                    NodeId = n.NodeId, ParentId = n.ParentId, TypeId = n.TypeId, Flags = n.Flags,
                    Depth = n.Depth, SiblingIndex = n.SiblingIndex, ChildCount = n.ChildCount,
                    TraversalIndex = n.TraversalIndex, Name = n.Name, TypeName = n.TypeName,
                }).ToArray(),
            };
            return new() { RequestId = request.RequestId, Status = data.HasSnapshot ? "ok" : "error",
                Reason = data.HasSnapshot ? "retained-snapshot" : "no-snapshot", Hierarchy = data };
        }
        if (_session.State != DebugSocketConnectionState.Connected) return Error("disconnected");
        if (!_capabilities.Supports(DebugStudioCapability.InspectorQuery | DebugStudioCapability.InspectorDetail))
            return Error("unsupported");

        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(request.TimeoutMilliseconds), _time);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var completion = new TaskCompletionSource<ControlReadResponseEnvelopeV1>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnDetail(InspectorDetailEnvelopeV1 detail)
        {
            if (detail.TargetId != request.NodeId) return;
            // wire に要求 ID がないため、厳密な応答相関はできない。
            // 同じ TargetId の最初の応答を best-effort で返し、GUI が同時に取得した値も許容する。
            completion.TrySetResult(new() { RequestId = request.RequestId, Inspector = detail,
                Status = detail.State == InspectorDetailState.Ready ? "ok" : "error",
                Reason = detail.State switch { InspectorDetailState.Ready => "ready",
                    InspectorDetailState.NotFound => "not-found", InspectorDetailState.Unsupported => "unsupported", _ => "faulted" } });
        }
        void OnConnection(DebugSocketConnectionSnapshot snapshot)
        {
            // すでに開始済みの callback は購読解除と競合し得るため、ここでは thread-safe な
            // completion だけを更新する。operation CTS や要求ごとの resource を参照しない。
            if (snapshot.State != DebugSocketConnectionState.Connected)
            {
                completion.TrySetResult(Error("disconnected"));
                disconnected.TrySetResult();
            }
        }
        _session.InspectorDetailReceived += OnDetail;
        _session.ConnectionStateChanged += OnConnection;
        // 購読を送信より先に行い、Inspector の即時応答を取りこぼさない。
        using var cancellation = operation.Token.Register(() => completion.TrySetCanceled(operation.Token));
        try
        {
            if (_session.State != DebugSocketConnectionState.Connected) return Error("disconnected");
            var send = _session.SendProtocolMessageAsync(DebugSocketMessageType.InspectorQuery,
                new InspectorQueryEnvelopeV1 { TargetId = request.NodeId,
                    QueryFlags = InspectorQueryFlags.IncludeMetadata | InspectorQueryFlags.IncludeComponents |
                        InspectorQueryFlags.IncludeProperties | InspectorQueryFlags.IncludeRawValues },
                requestId: $"cli-inspector-{Guid.NewGuid():N}", cancellationToken: operation.Token).WaitAsync(operation.Token);
            // 送信が停滞していても切断を先に返し、finally で送信を取り消す。
            if (await Task.WhenAny(send, disconnected.Task).ConfigureAwait(false) == send)
                await send.ConfigureAwait(false);
            return await completion.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return completion.Task.IsCompletedSuccessfully ? completion.Task.Result : Error(cancellationToken.IsCancellationRequested ? "cancelled" : "timeout"); }
        catch (Exception) { return Error("faulted"); }
        finally
        {
            // 共有 SessionService の event 購読を要求寿命で閉じてから、送信待ちを取り消す。
            // すでに実行中の callback は completion.TrySetResult のみを行い、破棄済み CTS を参照しない。
            _session.InspectorDetailReceived -= OnDetail;
            _session.ConnectionStateChanged -= OnConnection;
            operation.Cancel();
        }
    }
}
