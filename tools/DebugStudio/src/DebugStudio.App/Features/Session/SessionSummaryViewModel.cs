#nullable enable


using DebugStudio.App.Core.Mvvm;
using System.Collections.ObjectModel;
using DebugStudio.App.Core.Formatting;
using DebugStudio.App.Core.Stores;

namespace DebugStudio.App.Features.Session;

/// <summary>
/// Session window 専用の要約 ViewModel。
/// MainWindowViewModel から dashboard 的な文字列群をここへ逃がし、
/// shell coordinator は接続イベントのルーティングに専念させる。
/// </summary>
public sealed class SessionSummaryViewModel : ObservableObject
{
    private readonly int _maxActivityItems;
    private string _connectionState = "Disconnected";
    private string _connectionDetail = "Ready.";
    private string _sessionLabel = "No Unity session attached.";
    private string _capabilityStatus = "Idle";
    private string _capabilityDetail = "Connect to a Unity session to negotiate capabilities.";
    private string _negotiatedCapabilitiesText = "Negotiated: None";
    private string _activityHeadline = "No activity yet.";
    private string _latestLog = "No log frames yet.";
    private string _latestTelemetry = "No telemetry frames yet.";
    private string _latestServiceStatus = "No service status frames yet.";
    private string _latestCommandResult = "No command results yet.";
    private string _latestHierarchy = "No hierarchy payloads yet.";
    private string _latestInspector = "No inspector payloads yet.";
    private string _logSummary = "0 retained / 0 total";
    private string _hierarchySummary = "Hierarchy snapshot not received yet.";
    private string _inspectorSummary = "Inspector is idle.";
    private long _logCount;
    private int _hierarchyNodeCount;
    private int _inspectorPropertyCount;
    private int _telemetryCount;
    private int _serviceStatusCount;
    private int _commandResultCount;

    public SessionSummaryViewModel(int maxActivityItems)
    {
        _maxActivityItems = maxActivityItems;
        RecentActivity = new ObservableCollection<string>();
    }

    public ObservableCollection<string> RecentActivity { get; }

    public string ConnectionState
    {
        get => _connectionState;
        private set => SetProperty(ref _connectionState, value);
    }

    public string ConnectionDetail
    {
        get => _connectionDetail;
        private set => SetProperty(ref _connectionDetail, value);
    }

    public string SessionLabel
    {
        get => _sessionLabel;
        private set => SetProperty(ref _sessionLabel, value);
    }

    public string CapabilityStatus
    {
        get => _capabilityStatus;
        private set => SetProperty(ref _capabilityStatus, value);
    }

    public string CapabilityDetail
    {
        get => _capabilityDetail;
        private set => SetProperty(ref _capabilityDetail, value);
    }

    public string NegotiatedCapabilitiesText
    {
        get => _negotiatedCapabilitiesText;
        private set => SetProperty(ref _negotiatedCapabilitiesText, value);
    }

    public string ActivityHeadline
    {
        get => _activityHeadline;
        private set => SetProperty(ref _activityHeadline, value);
    }

    public string LatestLog
    {
        get => _latestLog;
        private set => SetProperty(ref _latestLog, value);
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

    public string LatestCommandResult
    {
        get => _latestCommandResult;
        private set => SetProperty(ref _latestCommandResult, value);
    }

    public string LatestHierarchy
    {
        get => _latestHierarchy;
        private set => SetProperty(ref _latestHierarchy, value);
    }

    public string LatestInspector
    {
        get => _latestInspector;
        private set => SetProperty(ref _latestInspector, value);
    }

    public string LogSummary
    {
        get => _logSummary;
        private set => SetProperty(ref _logSummary, value);
    }

    public string HierarchySummary
    {
        get => _hierarchySummary;
        private set => SetProperty(ref _hierarchySummary, value);
    }

    public string InspectorSummary
    {
        get => _inspectorSummary;
        private set => SetProperty(ref _inspectorSummary, value);
    }

    public long LogCount
    {
        get => _logCount;
        private set => SetProperty(ref _logCount, value);
    }

    public int HierarchyNodeCount
    {
        get => _hierarchyNodeCount;
        private set => SetProperty(ref _hierarchyNodeCount, value);
    }

    public int InspectorPropertyCount
    {
        get => _inspectorPropertyCount;
        private set => SetProperty(ref _inspectorPropertyCount, value);
    }

    public int TelemetryCount
    {
        get => _telemetryCount;
        private set => SetProperty(ref _telemetryCount, value);
    }

    public int ServiceStatusCount
    {
        get => _serviceStatusCount;
        private set => SetProperty(ref _serviceStatusCount, value);
    }

    public int CommandResultCount
    {
        get => _commandResultCount;
        private set => SetProperty(ref _commandResultCount, value);
    }

    public void UpdateConnection(string state, string detail)
    {
        ConnectionState = state;
        ConnectionDetail = detail;
    }

    public void SetConnectionDetail(string detail)
    {
        ConnectionDetail = detail;
    }

    public void UpdateCapability(CapabilityStateSnapshot snapshot)
    {
        CapabilityStatus = snapshot.HandshakeState;
        CapabilityDetail = snapshot.Detail;
        NegotiatedCapabilitiesText =
            $"Negotiated: {DebugStudioTextFormatter.FormatCapabilities(snapshot.NegotiatedCapabilities)}";
        SessionLabel = BuildSessionLabel(snapshot);
    }

    public void UpdateLogMetrics(long totalReceived, string latestLog, string filterSummary)
    {
        LogCount = totalReceived;
        LatestLog = latestLog;
        LogSummary = filterSummary;
    }

    public void UpdateHierarchyMetrics(int nodeCount, string summary)
    {
        HierarchyNodeCount = nodeCount;
        HierarchySummary = summary;
    }

    public void UpdateInspectorMetrics(int propertyCount, string summary)
    {
        InspectorPropertyCount = propertyCount;
        InspectorSummary = summary;
    }

    public void RecordTelemetry(string formattedTelemetry)
    {
        TelemetryCount++;
        LatestTelemetry = formattedTelemetry;
    }

    public void RecordServiceStatus(string formattedStatus)
    {
        ServiceStatusCount++;
        LatestServiceStatus = formattedStatus;
    }

    public void RecordCommandResult(string formattedCommandResult)
    {
        CommandResultCount++;
        LatestCommandResult = formattedCommandResult;
    }

    public void RecordHierarchy(string formattedHierarchy)
    {
        LatestHierarchy = formattedHierarchy;
    }

    public void RecordInspector(string formattedInspector)
    {
        LatestInspector = formattedInspector;
    }

    public void AppendActivity(string line)
    {
        RecentActivity.Insert(0, line);
        ActivityHeadline = line;

        while (RecentActivity.Count > _maxActivityItems)
        {
            RecentActivity.RemoveAt(RecentActivity.Count - 1);
        }
    }

    private static string BuildSessionLabel(CapabilityStateSnapshot snapshot)
    {
        var remoteName = string.IsNullOrWhiteSpace(snapshot.RemoteName)
            ? "Unity sender"
            : snapshot.RemoteName;

        if (!string.IsNullOrWhiteSpace(snapshot.SessionId))
        {
            return $"{remoteName} / session {snapshot.SessionId}";
        }

        return snapshot.HandshakeState == "Negotiated"
            ? $"{remoteName} / session ready"
            : "No Unity session attached.";
    }
}
