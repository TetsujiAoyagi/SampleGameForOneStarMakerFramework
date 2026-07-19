#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// retained telemetry を Elastic へ送る直前のユーザー確認を抽象化する。
/// ViewModel は WPF の MessageBox に依存せず、テストでは Cancel を決定的に再現できる。
/// </summary>
public interface IElasticPushConfirmation
{
    Task<bool> ConfirmPushAsync(ElasticTelemetryPushPreview preview, CancellationToken cancellationToken = default);
}
