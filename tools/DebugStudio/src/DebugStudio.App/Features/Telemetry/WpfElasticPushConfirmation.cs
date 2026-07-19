#nullable enable

using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DebugStudio.App.Core.Services;

namespace DebugStudio.App.Features.Telemetry;

/// <summary>
/// WPF の確認ダイアログ実装。
/// API key や telemetry payload は表示せず、preview の安全な件数・概算サイズだけを示す。
/// </summary>
public sealed class WpfElasticPushConfirmation : IElasticPushConfirmation
{
    public Task<bool> ConfirmPushAsync(ElasticTelemetryPushPreview preview, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = MessageBox.Show(
            $"{preview.DescribeForUi()}\n\nBootstrap and push this retained telemetry to local Elastic?",
            "Confirm Elastic Push",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }
}
