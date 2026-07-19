#nullable enable


using DebugStudio.App.Core.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using DebugStudio.App.Core.Formatting;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Export.Models;

namespace DebugStudio.App.Features.Telemetry;

/// <summary>
/// Telemetry window/panel 専用 ViewModel。
/// telemetry/service status の表示責務を shell から切り離す。
///
/// <para>
/// R2 では count と latest だけでなく recent history も見せる。
/// これにより、別 window を開かなくても直近の失敗連鎖や status 推移を panel 上で追える。
/// </para>
/// </summary>
public sealed class TelemetryWindowViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly TelemetryStore _telemetryStore;
    private readonly CapabilityStateStore _capabilityStateStore;
    private readonly TelemetryExportService? _telemetryExportService;
    private readonly ElasticTelemetryPushService? _elasticTelemetryPushService;
    private readonly IElasticPushConfirmation? _elasticPushConfirmation;
    private readonly TelemetryExportState _exportState;
    private long _telemetryCount;
    private long _serviceStatusCount;
    private string _latestTelemetry = "No telemetry frames yet.";
    private string _latestServiceStatus = "No service status frames yet.";
    private string _telemetryStatus = "Connect to a Unity session to receive telemetry.";
    private string _serviceStatusState = "Service status frames will appear after the session starts reporting.";
    private string _elasticConfigurationSummary = "Elastic L1 Verify is not available.";
    private string _elasticPreflightStatus = "Run preflight to verify localhost Elastic connectivity.";
    private string _elasticPreviewSummary = "Retained telemetry preview is unavailable.";
    private string _elasticPushStatus = string.Empty;
    private bool _elasticPreflightSucceeded;

    public TelemetryWindowViewModel(
        Dispatcher dispatcher,
        TelemetryStore telemetryStore,
        CapabilityStateStore capabilityStateStore)
        : this(dispatcher, telemetryStore, capabilityStateStore, telemetryExportService: null, pathPolicy: null)
    {
    }

    public TelemetryWindowViewModel(
        Dispatcher dispatcher,
        TelemetryStore telemetryStore,
        CapabilityStateStore capabilityStateStore,
        TelemetryExportService? telemetryExportService,
        TelemetryExportPathPolicy? pathPolicy)
        : this(
            dispatcher,
            telemetryStore,
            capabilityStateStore,
            telemetryExportService,
            pathPolicy,
            elasticTelemetryPushService: null,
            elasticPushConfirmation: null)
    {
    }

    public TelemetryWindowViewModel(
        Dispatcher dispatcher,
        TelemetryStore telemetryStore,
        CapabilityStateStore capabilityStateStore,
        TelemetryExportService? telemetryExportService,
        TelemetryExportPathPolicy? pathPolicy,
        ElasticTelemetryPushService? elasticTelemetryPushService,
        IElasticPushConfirmation? elasticPushConfirmation = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _telemetryStore = telemetryStore ?? throw new ArgumentNullException(nameof(telemetryStore));
        _capabilityStateStore = capabilityStateStore ?? throw new ArgumentNullException(nameof(capabilityStateStore));
        _telemetryExportService = telemetryExportService;
        _elasticTelemetryPushService = elasticTelemetryPushService;
        _elasticPushConfirmation = elasticPushConfirmation;
        _exportState = new TelemetryExportState(pathPolicy);

        RecentTelemetry = new ObservableCollection<string>();
        RecentServiceStatuses = new ObservableCollection<string>();
        ExportCommand = new AsyncRelayCommand(ExportAsync, CanExport);
        ElasticPreflightCommand = new AsyncRelayCommand(PreflightElasticAsync, CanRunElasticPreflight);
        ElasticPushCommand = new AsyncRelayCommand(PushElasticAsync, CanPushElastic);
        _exportState.ExportPathChanged += OnExportStateChanged;
        _exportState.ExportFormatChanged += OnExportStateChanged;

        _telemetryStore.Changed += OnTelemetryChanged;
        _capabilityStateStore.Changed += OnCapabilityChanged;
        RefreshElasticConfigurationSummary();
        RefreshElasticPreview();
        Refresh();
    }

    public long TelemetryCount
    {
        get => _telemetryCount;
        private set => SetProperty(ref _telemetryCount, value);
    }

    public long ServiceStatusCount
    {
        get => _serviceStatusCount;
        private set => SetProperty(ref _serviceStatusCount, value);
    }

    public string LatestTelemetry
    {
        get => _latestTelemetry;
        private set => SetProperty(ref _latestTelemetry, value);
    }

    public string LatestServiceStatus
    {
        get => _latestServiceStatus;
        private set => SetProperty(ref _latestServiceStatus, value);
    }

    public string TelemetryStatus
    {
        get => _telemetryStatus;
        private set => SetProperty(ref _telemetryStatus, value);
    }

    public string ServiceStatusState
    {
        get => _serviceStatusState;
        private set => SetProperty(ref _serviceStatusState, value);
    }

    public string ExportPath
    {
        get => _exportState.ExportPath;
        set => _exportState.ExportPath = value;
    }

    public string ExportStatus
    {
        get => _exportState.ExportStatus;
        private set => _exportState.ExportStatus = value;
    }

    public ObservableCollection<string> RecentTelemetry { get; }

    public ObservableCollection<string> RecentServiceStatuses { get; }

    public System.Collections.Generic.IReadOnlyList<TelemetryExportFormatOption> ExportFormats => _exportState.ExportFormats;

    public TelemetryExportFormatOption SelectedExportFormat
    {
        get => _exportState.SelectedExportFormat;
        set => _exportState.SelectedExportFormat = value;
    }

    public string ExportButtonLabel => SelectedExportFormat.Format switch
    {
        TelemetryExportFormat.ElasticBulk => "Export Telemetry Elastic Bulk",
        _ => "Export Telemetry NDJSON",
    };

    public AsyncRelayCommand ExportCommand { get; }

    /// <summary>
    /// localhost Elastic への preflight。成功後に preview を更新し push を有効化する。
    /// </summary>
    public AsyncRelayCommand ElasticPreflightCommand { get; }

    /// <summary>
    /// bootstrap + `_bulk` による retained telemetry の明示投入。
    /// </summary>
    public AsyncRelayCommand ElasticPushCommand { get; }

    public string ElasticConfigurationSummary
    {
        get => _elasticConfigurationSummary;
        private set => SetProperty(ref _elasticConfigurationSummary, value);
    }

    public string ElasticPreflightStatus
    {
        get => _elasticPreflightStatus;
        private set => SetProperty(ref _elasticPreflightStatus, value);
    }

    /// <summary>
    /// retained snapshot（最大 256 件の current-session 近似）の件数と概算サイズ。
    /// </summary>
    public string ElasticPreviewSummary
    {
        get => _elasticPreviewSummary;
        private set => SetProperty(ref _elasticPreviewSummary, value);
    }

    public string ElasticPushStatus
    {
        get => _elasticPushStatus;
        private set => SetProperty(ref _elasticPushStatus, value);
    }

    public void Dispose()
    {
        _telemetryStore.Changed -= OnTelemetryChanged;
        _capabilityStateStore.Changed -= OnCapabilityChanged;
        _exportState.ExportPathChanged -= OnExportStateChanged;
        _exportState.ExportFormatChanged -= OnExportStateChanged;
    }

    private void OnTelemetryChanged(TelemetryStoreSnapshot _)
    {
        Refresh();
    }

    private void OnCapabilityChanged(CapabilityStateSnapshot _)
    {
        Refresh();
    }

    private void OnExportStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(ExportPath));
        OnPropertyChanged(nameof(ExportStatus));
        OnPropertyChanged(nameof(SelectedExportFormat));
        OnPropertyChanged(nameof(ExportButtonLabel));
        ExportCommand.RaiseCanExecuteChanged();
    }

    private void Refresh()
    {
        var snapshot = _telemetryStore.GetSnapshot();
        var capability = _capabilityStateStore.GetSnapshot();

        UpdateOnUiThread(() =>
        {
            TelemetryCount = snapshot.TelemetryCount;
            ServiceStatusCount = snapshot.ServiceStatusCount;
            LatestTelemetry = snapshot.LatestTelemetry == null
                ? "No telemetry frames yet."
                : DebugStudioTextFormatter.FormatTelemetry(snapshot.LatestTelemetry);
            LatestServiceStatus = snapshot.LatestServiceStatus == null
                ? "No service status frames yet."
                : DebugStudioTextFormatter.FormatServiceStatus(snapshot.LatestServiceStatus);
            TelemetryStatus = BuildTelemetryStatus(snapshot, capability);
            ServiceStatusState = BuildServiceStatusState(snapshot, capability);
            ExportCommand.RaiseCanExecuteChanged();
            ElasticPreflightCommand.RaiseCanExecuteChanged();
            ElasticPushCommand.RaiseCanExecuteChanged();
            RefreshElasticPreview();

            ReplaceCollection(
                RecentTelemetry,
                snapshot.RecentTelemetry,
                static telemetry => DebugStudioTextFormatter.FormatTelemetry(telemetry));
            ReplaceCollection(
                RecentServiceStatuses,
                snapshot.RecentServiceStatuses,
                static status => DebugStudioTextFormatter.FormatServiceStatus(status));
        });
    }

    private static string BuildTelemetryStatus(TelemetryStoreSnapshot snapshot, CapabilityStateSnapshot capability)
    {
        if (snapshot.TelemetryCount > 0)
        {
            return "Telemetry frames are arriving.";
        }

        if (capability.HandshakeState == "Negotiated" &&
            (capability.NegotiatedCapabilities & DebugStudioCapability.TelemetryStream) == 0)
        {
            return "Unity sender has not advertised telemetry stream support yet.";
        }

        return capability.HandshakeState == "Negotiating"
            ? "Capability negotiation is in progress."
            : "Waiting for the first telemetry frame.";
    }

    private static string BuildServiceStatusState(TelemetryStoreSnapshot snapshot, CapabilityStateSnapshot capability)
    {
        if (snapshot.ServiceStatusCount > 0)
        {
            return "Service status frames are arriving.";
        }

        if (capability.HandshakeState == "Negotiated" &&
            (capability.NegotiatedCapabilities & DebugStudioCapability.ServiceStatusStream) == 0)
        {
            return "Unity sender has not advertised service status stream support yet.";
        }

        return capability.HandshakeState == "Negotiating"
            ? "Capability negotiation is in progress."
            : "Waiting for the first service status frame.";
    }

    private void UpdateOnUiThread(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _dispatcher.Invoke(action);
    }

    private bool CanExport()
    {
        return _telemetryExportService != null &&
            _exportState.CanExport() &&
            (TelemetryCount > 0 || ServiceStatusCount > 0);
    }

    private async Task ExportAsync()
    {
        if (_telemetryExportService == null)
        {
            ExportStatus = "Telemetry export service is not available.";
            return;
        }

        try
        {
            await _telemetryExportService.ExportAsync(ExportPath, SelectedExportFormat.Format).ConfigureAwait(false);
            UpdateOnUiThread(() =>
            {
                ExportStatus = SelectedExportFormat.Format switch
                {
                    TelemetryExportFormat.ElasticBulk => $"Exported telemetry Elastic bulk to {ExportPath}",
                    _ => $"Exported telemetry NDJSON to {ExportPath}",
                };
                OnPropertyChanged(nameof(ExportStatus));
            });
        }
        catch (Exception ex)
        {
            UpdateOnUiThread(() =>
            {
                ExportStatus = ex.Message;
                OnPropertyChanged(nameof(ExportStatus));
            });
        }
    }

    private bool CanRunElasticPreflight()
    {
        return _elasticTelemetryPushService != null;
    }

    private bool CanPushElastic()
    {
        return _elasticTelemetryPushService != null &&
            _elasticPushConfirmation != null &&
            _elasticPreflightSucceeded &&
            TelemetryCount > 0;
    }

    private async Task PreflightElasticAsync()
    {
        if (_elasticTelemetryPushService == null)
        {
            UpdateOnUiThread(() => ElasticPreflightStatus = "Elastic L1 Verify service is not available.");
            return;
        }

        RefreshElasticConfigurationSummary();
        RefreshElasticPreview();

        try
        {
            var result = await _elasticTelemetryPushService.PreflightAsync().ConfigureAwait(false);
            UpdateOnUiThread(() =>
            {
                _elasticPreflightSucceeded = result.Success;
                ElasticPreflightStatus = result.Message;
                ElasticPushCommand.RaiseCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            UpdateOnUiThread(() =>
            {
                _elasticPreflightSucceeded = false;
                ElasticPreflightStatus = $"Elastic preflight failed safely: {ex.GetType().Name}.";
                ElasticPushCommand.RaiseCanExecuteChanged();
            });
        }
    }

    private async Task PushElasticAsync()
    {
        if (_elasticTelemetryPushService == null)
        {
            UpdateOnUiThread(() => ElasticPushStatus = "Elastic L1 Verify service is not available.");
            return;
        }

        try
        {
            var preview = _elasticTelemetryPushService.BuildPreview();
            UpdateOnUiThread(() => ElasticPreviewSummary = preview.DescribeForUi());

            if (_elasticPushConfirmation == null)
            {
                UpdateOnUiThread(() => ElasticPushStatus = "Elastic push confirmation is not available.");
                return;
            }

            if (!await _elasticPushConfirmation.ConfirmPushAsync(preview).ConfigureAwait(false))
            {
                UpdateOnUiThread(() => ElasticPushStatus = "Elastic push was canceled before bootstrap and bulk submission.");
                return;
            }

            var result = await _elasticTelemetryPushService.PushRetainedTelemetryAsync().ConfigureAwait(false);
            UpdateOnUiThread(() =>
            {
                ElasticPushStatus = result.Message;
                ElasticPushCommand.RaiseCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            UpdateOnUiThread(() =>
            {
                ElasticPushStatus = $"Elastic push failed safely: {ex.GetType().Name}.";
                ElasticPushCommand.RaiseCanExecuteChanged();
            });
        }
    }

    private void RefreshElasticConfigurationSummary()
    {
        if (_elasticTelemetryPushService == null)
        {
            ElasticConfigurationSummary = "Elastic L1 Verify is not available.";
            return;
        }

        if (!_elasticTelemetryPushService.TryCreateSettings(out var settings, out var errorMessage) ||
            settings == null)
        {
            ElasticConfigurationSummary = errorMessage;
            _elasticPreflightSucceeded = false;
            ElasticPushCommand.RaiseCanExecuteChanged();
            return;
        }

        ElasticConfigurationSummary = settings.DescribeConfigurationForUi();
    }

    private void RefreshElasticPreview()
    {
        if (_elasticTelemetryPushService == null)
        {
            ElasticPreviewSummary = "Retained telemetry preview is unavailable.";
            return;
        }

        ElasticPreviewSummary = _elasticTelemetryPushService.BuildPreview().DescribeForUi();
    }

    /// <summary>
    /// recent history を毎回 store snapshot と揃え直す。
    /// 件数は小さく固定しているため、差分更新より全入れ替えの方が処理も読みやすい。
    /// </summary>
    private static void ReplaceCollection<TEnvelope>(
        ObservableCollection<string> target,
        System.Collections.Generic.IReadOnlyList<TEnvelope> source,
        Func<TEnvelope, string> formatter)
    {
        target.Clear();
        for (var index = 0; index < source.Count; index++)
        {
            target.Add(formatter(source[index]));
        }
    }
}
