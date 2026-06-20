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
    private readonly TelemetryExportState _exportState;
    private long _telemetryCount;
    private long _serviceStatusCount;
    private string _latestTelemetry = "No telemetry frames yet.";
    private string _latestServiceStatus = "No service status frames yet.";
    private string _telemetryStatus = "Connect to a Unity session to receive telemetry.";
    private string _serviceStatusState = "Service status frames will appear after the session starts reporting.";

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
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _telemetryStore = telemetryStore ?? throw new ArgumentNullException(nameof(telemetryStore));
        _capabilityStateStore = capabilityStateStore ?? throw new ArgumentNullException(nameof(capabilityStateStore));
        _telemetryExportService = telemetryExportService;
        _exportState = new TelemetryExportState(pathPolicy);

        RecentTelemetry = new ObservableCollection<string>();
        RecentServiceStatuses = new ObservableCollection<string>();
        ExportCommand = new AsyncRelayCommand(ExportAsync, CanExport);
        _exportState.ExportPathChanged += OnExportStateChanged;
        _exportState.ExportFormatChanged += OnExportStateChanged;

        _telemetryStore.Changed += OnTelemetryChanged;
        _capabilityStateStore.Changed += OnCapabilityChanged;
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
