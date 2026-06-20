#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// hierarchy 選択を inspector query へ橋渡しする service。
///
/// <para>
/// ViewModel から transport 呼び出しを直接行うと、
/// 「選択更新」と「問い合わせ状態管理」が混ざりやすい。
/// そこで query 発行と unsupported/faulted へのフォールバックをここへ閉じ込める。
/// </para>
/// </summary>
public sealed class InspectorQueryService
{
    private readonly SessionService _sessionService;
    private readonly CapabilityStateStore _capabilityStateStore;
    private readonly InspectorStore _inspectorStore;

    public InspectorQueryService(
        SessionService sessionService,
        CapabilityStateStore capabilityStateStore,
        InspectorStore inspectorStore)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _capabilityStateStore = capabilityStateStore ?? throw new ArgumentNullException(nameof(capabilityStateStore));
        _inspectorStore = inspectorStore ?? throw new ArgumentNullException(nameof(inspectorStore));
    }

    public async Task RequestDetailsAsync(
        long targetId,
        string targetName,
        string? targetTypeName,
        CancellationToken cancellationToken = default)
    {
        if (targetId <= 0)
        {
            _inspectorStore.SetFaulted(targetId, targetName, targetTypeName, "A valid hierarchy node id is required.");
            return;
        }

        _inspectorStore.BeginQuery(targetId, targetName, targetTypeName);

        var capabilityReady =
            _capabilityStateStore.Supports(DebugStudioCapability.InspectorQuery) &&
            _capabilityStateStore.Supports(DebugStudioCapability.InspectorDetail);

        if (!capabilityReady)
        {
            _inspectorStore.SetUnsupported(
                targetId,
                targetName,
                targetTypeName,
                "Unity sender has not negotiated inspector query/detail yet.");
            return;
        }

        try
        {
            await _sessionService.SendProtocolMessageAsync(
                DebugSocketMessageType.InspectorQuery,
                new InspectorQueryEnvelopeV1
                {
                    TargetId = targetId,
                    QueryFlags =
                        InspectorQueryFlags.IncludeMetadata |
                        InspectorQueryFlags.IncludeComponents |
                        InspectorQueryFlags.IncludeProperties |
                        InspectorQueryFlags.IncludeRawValues,
                },
                requestId: $"inspector-{targetId}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _inspectorStore.SetFaulted(targetId, targetName, targetTypeName, ex.Message);
        }
    }
}
