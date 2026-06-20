#nullable enable


using DebugStudio.App.Core.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Features.Inspector;

/// <summary>
/// inspector パネル専用 ViewModel。
///
/// <para>
/// store に積まれた最新 document を flatten して表に流し込むだけに責務を限定し、
/// query 発行や protocol 判定は別 service/store へ委譲する。
/// </para>
/// </summary>
public sealed class InspectorViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly InspectorStore _inspectorStore;
    private readonly CapabilityStateStore _capabilityStateStore;
    private readonly InspectorExportService? _inspectorExportService;
    private string _targetTitle = "No selection";
    private string _summary = "Inspector is idle.";
    private string _statusMessage = "Select a hierarchy node to request inspector details.";
    private string _exportPath;
    private string _exportStatus = "Inspector NDJSON export is ready.";
    private int _propertyCount;

    public InspectorViewModel(
        Dispatcher dispatcher,
        InspectorStore inspectorStore,
        CapabilityStateStore capabilityStateStore)
        : this(dispatcher, inspectorStore, capabilityStateStore, inspectorExportService: null, pathPolicy: null)
    {
    }

    public InspectorViewModel(
        Dispatcher dispatcher,
        InspectorStore inspectorStore,
        CapabilityStateStore capabilityStateStore,
        InspectorExportService? inspectorExportService,
        InspectorExportPathPolicy? pathPolicy)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _inspectorStore = inspectorStore ?? throw new ArgumentNullException(nameof(inspectorStore));
        _capabilityStateStore = capabilityStateStore ?? throw new ArgumentNullException(nameof(capabilityStateStore));
        _inspectorExportService = inspectorExportService;

        Properties = new ObservableCollection<InspectorPropertyItemViewModel>();
        ExportCommand = new AsyncRelayCommand(ExportAsync, CanExport);
        _exportPath = (pathPolicy ?? new InspectorExportPathPolicy()).CreateDefaultPath();

        _inspectorStore.Changed += OnInspectorChanged;
        _capabilityStateStore.Changed += OnCapabilityChanged;
        Refresh();
    }

    public ObservableCollection<InspectorPropertyItemViewModel> Properties { get; }

    public AsyncRelayCommand ExportCommand { get; }

    public string TargetTitle
    {
        get => _targetTitle;
        private set => SetProperty(ref _targetTitle, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int PropertyCount
    {
        get => _propertyCount;
        private set => SetProperty(ref _propertyCount, value);
    }

    public string ExportPath
    {
        get => _exportPath;
        set
        {
            if (SetProperty(ref _exportPath, value))
            {
                ExportCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ExportStatus
    {
        get => _exportStatus;
        private set => SetProperty(ref _exportStatus, value);
    }

    public void Dispose()
    {
        _inspectorStore.Changed -= OnInspectorChanged;
        _capabilityStateStore.Changed -= OnCapabilityChanged;
    }

    private void OnInspectorChanged(InspectorStoreSnapshot _)
    {
        Refresh();
    }

    private void OnCapabilityChanged(CapabilityStateSnapshot _)
    {
        Refresh();
    }

    private void Refresh()
    {
        var snapshot = _inspectorStore.GetSnapshotState();
        var document = _inspectorStore.GetDocument();
        var capability = _capabilityStateStore.GetSnapshot();

        UpdateOnUiThread(() =>
        {
            PropertyCount = snapshot.PropertyCount;
            TargetTitle = snapshot.TargetId == 0
                ? "No selection"
                : string.IsNullOrWhiteSpace(snapshot.TargetTypeName)
                    ? $"{snapshot.TargetName} (#{snapshot.TargetId})"
                    : $"{snapshot.TargetName} ({snapshot.TargetTypeName})";
            Summary = snapshot.TargetId == 0
                ? "Inspector is idle."
                : $"{snapshot.DetailState} / rev {snapshot.Revision} / {snapshot.PropertyCount} properties";
            StatusMessage = BuildStatusMessage(snapshot, capability);
            ExportCommand.RaiseCanExecuteChanged();

            Properties.Clear();
            if (document != null)
            {
                foreach (var section in document.Sections)
                {
                    foreach (var property in section.Properties)
                    {
                        Properties.Add(InspectorPropertyItemViewModel.FromRecord(section.DisplayName, property));
                    }
                }
            }
        });
    }

    private bool CanExport()
    {
        return _inspectorExportService != null &&
            !string.IsNullOrWhiteSpace(ExportPath) &&
            (_inspectorStore.GetSnapshotState().TargetId != 0);
    }

    private async Task ExportAsync()
    {
        if (_inspectorExportService == null)
        {
            ExportStatus = "Inspector export service is not available.";
            return;
        }

        try
        {
            await _inspectorExportService.ExportAsync(ExportPath).ConfigureAwait(false);
            UpdateOnUiThread(() => ExportStatus = $"Exported inspector NDJSON to {ExportPath}");
        }
        catch (Exception ex)
        {
            UpdateOnUiThread(() => ExportStatus = ex.Message);
        }
    }

    private static string BuildStatusMessage(InspectorStoreSnapshot snapshot, CapabilityStateSnapshot capability)
    {
        if (snapshot.TargetId != 0)
        {
            return snapshot.Message;
        }

        if (capability.HandshakeState == "Negotiated" &&
            (capability.NegotiatedCapabilities & DebugStudioCapability.InspectorDetail) == 0)
        {
            return "Unity sender has not advertised inspector detail support yet.";
        }

        return capability.HandshakeState == "Negotiating"
            ? "Capability negotiation is in progress."
            : "Select a hierarchy node to request inspector details.";
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
}
