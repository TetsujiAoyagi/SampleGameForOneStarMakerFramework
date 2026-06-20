#nullable enable


using DebugStudio.App.Core.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using DebugStudio.App.Core.Formatting;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Features.Hierarchy;

/// <summary>
/// hierarchy パネル専用 ViewModel。
///
/// <para>
/// hierarchy の保持は store、問い合わせ発行は inspector query service に委譲し、
/// この ViewModel 自身は「現在見せるノード一覧と選択状態」の整形に集中する。
/// </para>
/// </summary>
public sealed class HierarchyViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly HierarchyStore _hierarchyStore;
    private readonly CapabilityStateStore _capabilityStateStore;
    private readonly InspectorQueryService _inspectorQueryService;
    private readonly HierarchyExportService? _hierarchyExportService;
    private HierarchyNodeItemViewModel? _selectedNode;
    private string _summary = "Hierarchy snapshot not received yet.";
    private string _emptyState = "Connect to Unity to start hierarchy negotiation.";
    private string _selectionSummary = "No hierarchy node selected.";
    private string _exportPath;
    private string _exportStatus = "Hierarchy NDJSON export is ready.";
    private int _nodeCount;
    private bool _suppressSelectionCallback;

    public HierarchyViewModel(
        Dispatcher dispatcher,
        HierarchyStore hierarchyStore,
        CapabilityStateStore capabilityStateStore,
        InspectorQueryService inspectorQueryService)
        : this(dispatcher, hierarchyStore, capabilityStateStore, inspectorQueryService, hierarchyExportService: null, pathPolicy: null)
    {
    }

    public HierarchyViewModel(
        Dispatcher dispatcher,
        HierarchyStore hierarchyStore,
        CapabilityStateStore capabilityStateStore,
        InspectorQueryService inspectorQueryService,
        HierarchyExportService? hierarchyExportService,
        HierarchyExportPathPolicy? pathPolicy)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _hierarchyStore = hierarchyStore ?? throw new ArgumentNullException(nameof(hierarchyStore));
        _capabilityStateStore = capabilityStateStore ?? throw new ArgumentNullException(nameof(capabilityStateStore));
        _inspectorQueryService = inspectorQueryService ?? throw new ArgumentNullException(nameof(inspectorQueryService));
        _hierarchyExportService = hierarchyExportService;

        Nodes = new ObservableCollection<HierarchyNodeItemViewModel>();
        ExportCommand = new AsyncRelayCommand(ExportAsync, CanExport);
        _exportPath = (pathPolicy ?? new HierarchyExportPathPolicy()).CreateDefaultPath();

        _hierarchyStore.Changed += OnHierarchyChanged;
        _capabilityStateStore.Changed += OnCapabilityChanged;
        Refresh();
    }

    public ObservableCollection<HierarchyNodeItemViewModel> Nodes { get; }

    public AsyncRelayCommand ExportCommand { get; }

    public HierarchyNodeItemViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                SelectionSummary = value == null
                    ? "No hierarchy node selected."
                    : $"Selected #{value.NodeId} {value.Name}";

                if (_suppressSelectionCallback)
                {
                    return;
                }

                _hierarchyStore.SetSelectedNodeId(value?.NodeId);
                if (value != null)
                {
                    _ = RequestInspectorAsync(value);
                }
            }
        }
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string EmptyState
    {
        get => _emptyState;
        private set => SetProperty(ref _emptyState, value);
    }

    public string SelectionSummary
    {
        get => _selectionSummary;
        private set => SetProperty(ref _selectionSummary, value);
    }

    public int NodeCount
    {
        get => _nodeCount;
        private set => SetProperty(ref _nodeCount, value);
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
        _hierarchyStore.Changed -= OnHierarchyChanged;
        _capabilityStateStore.Changed -= OnCapabilityChanged;
    }

    private void OnHierarchyChanged(HierarchyStoreSnapshot _)
    {
        Refresh();
    }

    private void OnCapabilityChanged(CapabilityStateSnapshot _)
    {
        Refresh();
    }

    private void Refresh()
    {
        var snapshot = _hierarchyStore.GetSnapshotState();
        var nodes = _hierarchyStore.GetSnapshot();
        var capability = _capabilityStateStore.GetSnapshot();
        var desiredSelectedId = snapshot.SelectedNodeId ?? SelectedNode?.NodeId;

        UpdateOnUiThread(() =>
        {
            NodeCount = snapshot.NodeCount;
            Summary = snapshot.NodeCount == 0
                ? "Hierarchy snapshot not received yet."
                : $"{snapshot.ScopeName} / rev {snapshot.Revision} / {snapshot.NodeCount} nodes";
            EmptyState = BuildEmptyState(snapshot, capability);
            ExportCommand.RaiseCanExecuteChanged();

            Nodes.Clear();
            foreach (var node in nodes.Select(HierarchyNodeItemViewModel.FromRecord))
            {
                Nodes.Add(node);
            }

            _suppressSelectionCallback = true;
            try
            {
                SelectedNode = desiredSelectedId.HasValue
                    ? Nodes.FirstOrDefault(node => node.NodeId == desiredSelectedId.Value)
                    : null;
            }
            finally
            {
                _suppressSelectionCallback = false;
            }
        });
    }

    private bool CanExport()
    {
        return _hierarchyExportService != null &&
            !string.IsNullOrWhiteSpace(ExportPath) &&
            NodeCount > 0;
    }

    private async Task ExportAsync()
    {
        if (_hierarchyExportService == null)
        {
            ExportStatus = "Hierarchy export service is not available.";
            return;
        }

        try
        {
            await _hierarchyExportService.ExportAsync(ExportPath).ConfigureAwait(false);
            UpdateOnUiThread(() => ExportStatus = $"Exported hierarchy NDJSON to {ExportPath}");
        }
        catch (Exception ex)
        {
            UpdateOnUiThread(() => ExportStatus = ex.Message);
        }
    }

    private async Task RequestInspectorAsync(HierarchyNodeItemViewModel node)
    {
        await _inspectorQueryService.RequestDetailsAsync(node.NodeId, node.Name, node.TypeLabel).ConfigureAwait(false);
    }

    private static string BuildEmptyState(HierarchyStoreSnapshot snapshot, CapabilityStateSnapshot capability)
    {
        if (snapshot.NodeCount > 0)
        {
            return DebugStudioTextFormatter.FormatHierarchySummary(snapshot.ScopeName, snapshot.Revision, snapshot.NodeCount);
        }

        if (capability.HandshakeState == "Negotiated" &&
            (capability.NegotiatedCapabilities & DebugStudioCapability.HierarchySnapshot) == 0)
        {
            return "Unity sender has not advertised hierarchy snapshot support yet.";
        }

        return capability.HandshakeState == "Negotiating"
            ? "Capability negotiation is in progress."
            : "Waiting for the first hierarchy snapshot.";
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
