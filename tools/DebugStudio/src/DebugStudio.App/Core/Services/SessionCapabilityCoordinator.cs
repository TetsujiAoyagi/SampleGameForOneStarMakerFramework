#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// capability hello 送信と、送信結果の capability store 更新を一体に扱う coordinator。
///
/// <para>
/// connect 後「hello 送る」「送信成功/失敗を store に反映」が SessionService へ直書きされると、
/// capability 交渉フローを追いかけにくい。
/// この coordinator は hello 送信と事後処理をカプセル化し、capability 関連ロジックをまとめる。
/// </para>
/// </summary>
public sealed class SessionCapabilityCoordinator
{
    private readonly ISessionTransport _session;
    private readonly CapabilityHandshakeService _capabilityHandshakeService;
    private readonly CapabilityStateStore _capabilityStateStore;

    public SessionCapabilityCoordinator(
        ISessionTransport session,
        CapabilityHandshakeService capabilityHandshakeService,
        CapabilityStateStore capabilityStateStore)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _capabilityHandshakeService = capabilityHandshakeService ?? throw new ArgumentNullException(nameof(capabilityHandshakeService));
        _capabilityStateStore = capabilityStateStore ?? throw new ArgumentNullException(nameof(capabilityStateStore));
    }

    public async Task SendCapabilityHelloAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _session.SendMessageAsync(
                    DebugSocketMessageType.CapabilityHello,
                    _capabilityHandshakeService.CreateHello(),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            _capabilityStateStore.MarkHelloSent();
        }
        catch (Exception ex)
        {
            _capabilityStateStore.MarkHandshakeFaulted(ex.Message);
        }
    }
}
